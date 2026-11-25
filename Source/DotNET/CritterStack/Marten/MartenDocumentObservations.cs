// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Cratis.Screenplay.Generation;
using Cratis.Screenplay.Generation.DotNet;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

using static Cratis.CritterStack.Screenplay.Marten.MartenDocumentMembers;
using static Cratis.CritterStack.Screenplay.Marten.MartenDocumentSource;

namespace Cratis.CritterStack.Screenplay.Marten;

sealed record MartenDocumentObservation
{
    public required INamedTypeSymbol Type { get; init; }
    public required Evidence Evidence { get; init; }
    public required BackendOwnership Ownership { get; init; }
    public required string Site { get; init; }
    public required long Order { get; init; }
    public required OmissionDiagnosticEvidencePreference Preference { get; init; }
    public bool IsProjected { get; init; }
    public IMethodSymbol? Method { get; init; }
    public bool HasExplicitIdentity { get; init; }
    public ISymbol? IdentityMember { get; init; }
    public RelationshipKind? Relationship { get; init; }
    public BackendOwnership HandlerOwnership { get; init; } = BackendOwnership.Legacy;
}

sealed class MartenDocumentObservations(
    DotNetProjectCompilation project,
    AdapterIdentity adapter,
    MartenBackendOwnershipResolver ownership)
{
    public IReadOnlyList<MartenDocumentObservation> Discover(IReadOnlyList<ProjectionRegistration>? registrations = null)
    {
        var projected = (registrations ?? [])
            .Where(registration => registration.Kind == ProjectionKind.Event && registration.Ownership.CanEmit)
            .SelectMany(registration => MartenEventProjectionFacts.ObserveDocuments(project, adapter, registration))
            .Select((observation, order) => observation with { Order = order })
            .ToArray();
        return [.. projected, .. ObserveInvocations(projected.LongLength)];
    }

    static DocumentTarget TargetFor(InvocationExpressionSyntax invocation, SemanticModel model, IMethodSymbol method)
    {
        if (MartenCompiledQueryDiscovery.IsCompiledQueryExecution(method))
        {
            return new(MartenCompiledQueryDiscovery.TryResolve(invocation, model, out var plan) ? plan.DocumentType : null, Compiled: true);
        }

        var type = IsIdentityConfiguration(method)
            ? (method.ReducedFrom ?? method).ContainingType.TypeArguments.FirstOrDefault() as INamedTypeSymbol
            : DocumentTypeFrom(method, invocation, model);
        return new(type, Compiled: false);
    }

    static bool IsObservedTarget(DocumentTarget target, IMethodSymbol method, InvocationExpressionSyntax invocation, SemanticModel model) =>
        IsIdentityConfiguration(method) || target.Compiled || IsObservedUsage(RelationshipKindFor(method.Name), method, invocation, model);

    IEnumerable<MartenDocumentObservation> ObserveInvocations(long order)
    {
        foreach (var (invocation, model) in AuthoredInvocations())
        {
            if (Observe(invocation, model, order++) is { } observation)
            {
                yield return observation;
            }
        }
    }

    IEnumerable<(InvocationExpressionSyntax Invocation, SemanticModel Model)> AuthoredInvocations()
    {
        foreach (var tree in project.AuthoredSyntaxTrees.Where(tree => !DotNetGeneratedSource.IsGenerated(tree)))
        {
            var model = project.Compilation.GetSemanticModel(tree);
            foreach (var invocation in DotNetSource.AuthoredInvocationsIn(tree.GetRoot(), project))
            {
                yield return (invocation, model);
            }
        }
    }

    MartenDocumentObservation? Observe(InvocationExpressionSyntax invocation, SemanticModel model, long order)
    {
        if (DotNetInvocations.MethodFor(invocation, model) is not { } method || !IsMarten(method, includeIntegrationApis: false))
        {
            return null;
        }

        return CreateObservation(invocation, model, method, order);
    }

    MartenDocumentObservation? CreateObservation(InvocationExpressionSyntax invocation, SemanticModel model, IMethodSymbol method, long order)
    {
        var target = TargetFor(invocation, model, method);
        if (!IsObservedTarget(target, method, invocation, model))
        {
            return null;
        }

        if (target.Type is not { } type || !IsSourceType(type)) return null;
        var containingMethod = model.GetEnclosingSymbol(invocation.SpanStart) as IMethodSymbol;
        var identity = IsIdentityConfiguration(method);
        return new()
        {
            Type = type,
            Evidence = identity ? IdentityEvidence(project, adapter, invocation, type) : UsageEvidence(project, adapter, invocation, method.Name),
            Ownership = ownership.ForInvocation(project, invocation, containingMethod),
            Site = SiteFor(invocation, project),
            Order = order,
            Preference = PreferenceFor(method),
            Method = containingMethod,
            HasExplicitIdentity = identity,
            IdentityMember = identity ? ResolveIdentityMember(invocation, model, type) : null,
            Relationship = target.Compiled ? null : RelationshipKindFor(method.Name),
            HandlerOwnership = containingMethod is null ? BackendOwnership.Legacy : ownership.ForMethod(project, containingMethod)
        };
    }

    sealed record DocumentTarget(INamedTypeSymbol? Type, bool Compiled);
}
