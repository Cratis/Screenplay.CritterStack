// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Cratis.Screenplay.Generation;
using Cratis.Screenplay.Generation.DotNet;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Cratis.CritterStack.Screenplay.Marten;

sealed class MartenCompiledQueryFacts(
    DotNetProjectCompilation project,
    DotNetAnalysisContext context,
    AdapterIdentity adapter,
    MartenBackendOwnershipResolver ownership)
{
    readonly List<GenerationFact> _facts = [];

    public AdapterContribution Emit()
    {
        var sites = new DotNetArtifactCatalog(project.Compilation).Types
            .SelectMany(type => type.GetMembers().OfType<IMethodSymbol>())
            .Where(method => method.MethodKind == MethodKind.Ordinary)
            .SelectMany(Observe)
            .ToArray();
        foreach (var group in sites.GroupBy(site => (site.Declaration, site.Ownership)))
        {
            EmitDeclaration([.. group]);
        }

        return new() { Adapter = adapter, Facts = _facts, Diagnostics = [] };
    }

    IEnumerable<Site> Observe(IMethodSymbol method)
    {
        var observation = MartenCompiledQueryDiscovery.Observe(method, CritterStackMethodFacts.SubjectFor(project, method), project, adapter);
        var order = 0;
        foreach (var link in observation.Links)
        {
            yield return SiteFor(method, link.Plan, link.Invocation, link.Evidence) with { ExecutionOrder = order++ };
        }

        foreach (var flow in observation.UnprovenFlows)
        {
            var evidence = new Evidence
            {
                Adapter = adapter,
                Strength = EvidenceStrength.Exact,
                Source = flow.Diagnostic.Source,
                Explanation = "Marten compiled query declaration in an unproven executable flow"
            };
            yield return SiteFor(method, flow.Plan, flow.Invocation, evidence) with { Diagnostic = flow.Diagnostic };
        }
    }

    Site SiteFor(IMethodSymbol method, MartenCompiledQueryObservation plan, InvocationExpressionSyntax invocation, Evidence evidence)
    {
        var owner = ownership.ForInvocation(project, invocation, method);
        return new()
        {
            Method = method,
            Plan = plan,
            Declaration = DeclarationFor(plan),
            Document = context.SubjectForType(plan.DocumentType)!,
            Ownership = owner,
            Invocation = invocation,
            Evidence = evidence
        };
    }

    SubjectId DeclarationFor(MartenCompiledQueryObservation plan) => new()
    {
        // This library declaration role belongs to the exact plan/document pair, not to an endpoint.
        Value = $"{context.SubjectForType(plan.PlanType)!.Value}#compiled-query:{Uri.EscapeDataString(context.SubjectForType(plan.DocumentType)!.Value)}"
    };

    void EmitDeclaration(Site[] sites)
    {
        var site = sites[0];
        if (!site.Ownership.CanEmit)
        {
            EmitUnownedInputs(sites);
            return;
        }

        var prefix = BackendOccurrenceKeys.FactId($"{adapter.Id}:compiled-query:{site.Declaration.Value}", site.Target.Scope);
        var declaration = new ArtifactUsageDeclarationFact
        {
            Id = new() { Value = prefix },
            Subject = site.Declaration,
            Evidence = site.Evidence,
            Definition = new() { Declaration = site.Declaration, Artifact = site.Target }
        };
        var inputs = MartenCompiledQueryDiscovery.InputFacts(site.Plan.PlanType, new MartenCompiledQueryInputOwner(site.Declaration, site.Target.Scope), context, site.Evidence);
        var alternatives = sites.Select(item => CritterStackMethodFacts.Observe(project, item.Method, site.Target.Scope)).DistinctBy(alternative => alternative.Method).ToArray();
        foreach (var fact in new GenerationFact[] { declaration }.Concat(inputs))
        {
            _facts.Add(fact);
            _facts.Add(CritterStackMethodFacts.Support(fact, alternatives));
        }

        foreach (var item in sites)
        {
            if (item.Diagnostic is { } diagnostic)
            {
                EmitDiagnostic(item, declaration.Id, diagnostic);
            }
            else
            {
                EmitUsage(item);
            }
        }
    }

    void EmitUnownedInputs(Site[] sites)
    {
        var executions = sites.Where(site => site.Diagnostic is null).ToArray();
        if (executions.Length == 0)
        {
            return;
        }

        var site = executions[0];
        var inputs = MartenCompiledQueryDiscovery.InputFacts(site.Plan.PlanType, new MartenCompiledQueryInputOwner(site.Declaration), context, site.Evidence);
        var alternatives = executions.Select(item => CritterStackMethodFacts.Observe(project, item.Method)).DistinctBy(alternative => alternative.Method).ToArray();
        foreach (var fact in inputs)
        {
            _facts.Add(fact);
            _facts.Add(CritterStackMethodFacts.Support(fact, alternatives));
        }

        foreach (var execution in executions)
        {
            EmitInputExecution(execution);
        }
    }

    void EmitInputExecution(Site site)
    {
        var method = CritterStackMethodFacts.SubjectFor(project, site.Method);
        var fact = new MethodDeclarationExecutionFact
        {
            Id = new() { Value = $"{adapter.Id}:compiled-query-input-execution:{method.Value}:{site.Invocation.SpanStart}" },
            Subject = method,
            Evidence = site.Evidence,
            Definition = new()
            {
                Method = method,
                Declaration = site.Declaration,
                ExecutionOrder = site.ExecutionOrder
            }
        };
        _facts.Add(fact);
        _facts.Add(CritterStackMethodFacts.Support(fact, [CritterStackMethodFacts.Observe(project, site.Method)]));
    }

    void EmitUsage(Site site)
    {
        var method = CritterStackMethodFacts.SubjectFor(project, site.Method);
        var fact = new MethodArtifactUsageFact
        {
            Id = new() { Value = BackendOccurrenceKeys.FactId($"{adapter.Id}:compiled-query-execution:{method.Value}:{site.Invocation.SpanStart}", site.Target.Scope) },
            Subject = method,
            Evidence = site.Evidence,
            Definition = new()
            {
                Method = method,
                Declaration = site.Declaration,
                Kind = RelationshipKind.Reads,
                ExecutionOrder = site.ExecutionOrder,
                TargetScope = site.Target.Scope
            }
        };
        _facts.Add(fact);
        _facts.Add(CritterStackMethodFacts.Support(fact, [CritterStackMethodFacts.Observe(project, site.Method)]));
    }

    void EmitDiagnostic(Site site, FactId declaration, GenerationDiagnostic diagnostic)
    {
        var method = CritterStackMethodFacts.SubjectFor(project, site.Method);
        var fact = new MethodDiagnosticProjectionFact
        {
            Id = new() { Value = $"{adapter.Id}:compiled-query-flow:{method.Value}:{site.Invocation.SpanStart}" },
            Subject = method,
            Evidence = site.Evidence,
            Definition = new()
            {
                Method = method,
                EntryPointRole = ArtifactKind.Query,
                Mode = MethodDiagnosticProjectionMode.EntryPointArtifact,
                Diagnostic = diagnostic with { Facts = [declaration] }
            }
        };
        _facts.Add(fact);
        _facts.Add(CritterStackMethodFacts.Support(fact, [CritterStackMethodFacts.Observe(project, site.Method)]));
    }

    sealed record Site
    {
        public required IMethodSymbol Method { get; init; }
        public required MartenCompiledQueryObservation Plan { get; init; }
        public required SubjectId Declaration { get; init; }
        public required SubjectId Document { get; init; }
        public required BackendOwnership Ownership { get; init; }
        public ArtifactKey Target => BackendOccurrenceKeys.Artifact(Document, ArtifactKind.Document, Ownership);
        public required InvocationExpressionSyntax Invocation { get; init; }
        public required Evidence Evidence { get; init; }
        public int? ExecutionOrder { get; init; }
        public GenerationDiagnostic? Diagnostic { get; init; }
    }
}
