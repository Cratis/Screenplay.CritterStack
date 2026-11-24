// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Cratis.Screenplay.Generation;
using Cratis.Screenplay.Generation.DotNet;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

using static Cratis.CritterStack.Screenplay.Marten.MartenDocumentMembers;
using static Cratis.CritterStack.Screenplay.Marten.MartenDocumentSource;

namespace Cratis.CritterStack.Screenplay.Marten;

static class MartenDocumentFacts
{
    static readonly HashSet<string> _configurationMethods = ["For", "RegisterDocumentType"];

    public static MartenDiscoveryResult Discover(
        DotNetProjectCompilation project,
        AdapterIdentity adapter,
        CritterStackSubjectResolver subjects,
        IReadOnlyList<MartenDocumentUsage> projectedDocuments,
        MartenBackendOwnershipResolver ownership,
        bool includeIntegrationApis = true,
        IReadOnlySet<IMethodSymbol>? failureStageMethods = null)
    {
        var state = new DiscoveryState(project, adapter, subjects, projectedDocuments, ownership);
        ObserveSource(state, includeIntegrationApis, failureStageMethods);

        foreach (var (key, observed) in state.Documents.OrderBy(_ => _.Key.Subject.Value, StringComparer.Ordinal))
        {
            AddDocumentFacts(state, key.Subject, observed);
        }

        state.Diagnostics.AddRange(state.Documents
            .OrderBy(_ => _.Key.Subject.Value, StringComparer.Ordinal)
            .Select(_ => OmissionDiagnostic(_.Key.Subject, _.Value)));

        return new(state.Facts, state.Diagnostics, []);
    }

    static GenerationDiagnostic OmissionDiagnostic(SubjectId subject, DocumentObservation observed) => new()
    {
        Code = MartenDiagnosticCodes.DocumentModelOmitted,
        Severity = GenerationDiagnosticSeverity.Information,
        Outcome = GenerationDiagnosticOutcome.Unsupported,
        Message = $"Marten document '{observed.Type.Name}' is persisted, queried, or explicitly configured, but the current Screenplay language has no ordinary document-state declaration",
        Source = observed.RegistrationEvidence.Source,
        Subject = subject
    };

    static void ObserveSource(DiscoveryState state, bool includeIntegrationApis, IReadOnlySet<IMethodSymbol>? failureStageMethods)
    {
        foreach (var tree in state.Project.AuthoredSyntaxTrees.Where(_ => !DotNetGeneratedSource.IsGenerated(_)))
        {
            ObserveTree(state, tree, includeIntegrationApis, failureStageMethods);
        }
    }

    static void ObserveTree(DiscoveryState state, SyntaxTree tree, bool includeIntegrationApis, IReadOnlySet<IMethodSymbol>? failureStageMethods)
    {
        var semanticModel = state.Project.Compilation.GetSemanticModel(tree);
        foreach (var invocation in DotNetSource.AuthoredInvocationsIn(tree.GetRoot(), state.Project))
        {
            if (!IsInFailureStage(invocation, semanticModel, failureStageMethods))
            {
                ObserveInvocation(state, invocation, semanticModel, includeIntegrationApis);
            }
        }
    }

    static bool IsInFailureStage(
        InvocationExpressionSyntax invocation,
        SemanticModel semanticModel,
        IReadOnlySet<IMethodSymbol>? failureStageMethods)
    {
        if (failureStageMethods is null || failureStageMethods.Count == 0)
        {
            return false;
        }

        return ContainingMethods(semanticModel.GetEnclosingSymbol(invocation.SpanStart)).Any(failureStageMethods.Contains);
    }

    static IEnumerable<IMethodSymbol> ContainingMethods(ISymbol? enclosing)
    {
        for (var symbol = enclosing; symbol is not null; symbol = symbol.ContainingSymbol)
        {
            if (symbol is IMethodSymbol method)
            {
                yield return method;
            }
        }
    }

    static void ObserveInvocation(
        DiscoveryState state,
        InvocationExpressionSyntax invocation,
        SemanticModel semanticModel,
        bool includeIntegrationApis)
    {
        if (DotNetInvocations.MethodFor(invocation, semanticModel) is not { } method || !IsMarten(method, includeIntegrationApis))
        {
            return;
        }

        if (IsIdentityConfiguration(method))
        {
            ObserveIdentityConfiguration(state, invocation, method, semanticModel);
        }
        else if (MartenCompiledQueryDiscovery.IsCompiledQueryExecution(method))
        {
            ObserveCompiledQueryExecution(state, invocation, method, semanticModel);
        }
        else
        {
            ObserveDocumentUsage(state, invocation, method, semanticModel);
        }
    }

    static void ObserveCompiledQueryExecution(
        DiscoveryState state,
        InvocationExpressionSyntax invocation,
        IMethodSymbol method,
        SemanticModel semanticModel)
    {
        if (!MartenCompiledQueryDiscovery.TryResolve(invocation, semanticModel, out var plan))
        {
            return;
        }

        var compiledEvidence = UsageEvidence(state.Project, state.Adapter, invocation, method.Name);
        var ownership = state.Ownership.ForInvocation(state.Project, invocation);
        if (!RequireOwnership(state, invocation, ownership))
        {
            return;
        }

        var key = BackendOccurrenceKeys.Artifact(state.Subjects.SubjectForType(state.Project, plan.DocumentType), ArtifactKind.Document, ownership);
        state.Documents.TryAdd(key, new(plan.DocumentType, compiledEvidence) { Ownership = ownership });
    }

    static void ObserveDocumentUsage(
        DiscoveryState state,
        InvocationExpressionSyntax invocation,
        IMethodSymbol method,
        SemanticModel semanticModel)
    {
        var kind = RelationshipKindFor(method.Name);
        if (!IsObservedUsage(kind, method, invocation, semanticModel))
        {
            return;
        }

        var documentType = DocumentTypeFrom(method, invocation, semanticModel);
        if (documentType is null || !IsSourceType(documentType))
        {
            return;
        }

        var evidence = UsageEvidence(state.Project, state.Adapter, invocation, method.Name);
        var documentSubject = state.Subjects.SubjectForType(state.Project, documentType);
        var ownership = state.Ownership.ForInvocation(state.Project, invocation);
        if (!RequireOwnership(state, invocation, ownership))
        {
            return;
        }

        var documentKey = BackendOccurrenceKeys.Artifact(documentSubject, ArtifactKind.Document, ownership);
        state.Documents.TryAdd(documentKey, new(documentType, evidence) { Ownership = ownership });
        if (_configurationMethods.Contains(method.Name) &&
            state.Documents[documentKey].RegistrationPreference == OmissionDiagnosticEvidencePreference.UseSite)
        {
            state.Documents[documentKey] = state.Documents[documentKey] with
            {
                RegistrationEvidence = evidence,
                RegistrationPreference = OmissionDiagnosticEvidencePreference.ExplicitRegistration
            };
        }

        AddHandlerFactsWhenInMethod(state, new(invocation, documentSubject, evidence, ownership), kind, semanticModel);
    }

    static void AddHandlerFactsWhenInMethod(
        DiscoveryState state,
        DocumentUsageSite site,
        RelationshipKind? kind,
        SemanticModel semanticModel)
    {
        if (kind is not null && semanticModel.GetEnclosingSymbol(site.Invocation.SpanStart) is IMethodSymbol containingMethod)
        {
            AddHandlerFacts(state, new(site.Invocation, containingMethod, site.DocumentSubject, site.Evidence, site.Ownership), kind.Value);
        }
    }

    static bool RequireOwnership(DiscoveryState state, InvocationExpressionSyntax invocation, BackendOwnership ownership)
    {
        if (ownership.CanEmit)
        {
            return true;
        }

        var model = state.Project.Compilation.GetSemanticModel(invocation.SyntaxTree);
        var subject = model.GetEnclosingSymbol(invocation.SpanStart) is IMethodSymbol method
            ? DotNetMethodIdentity.SubjectFor(state.Project, method)
            : new SubjectId { Value = state.Project.Name };
        state.Diagnostics.Add(ownership.Diagnostic(
            subject,
            CritterStackSource.RangeForProject(invocation.GetLocation(), state.Project),
            "Marten document operation ownership cannot be proved"));
        return false;
    }

    static void AddHandlerFacts(DiscoveryState state, HandlerUsage usage, RelationshipKind kind)
    {
        var handlerOwnership = state.Ownership.ForMethod(state.Project, usage.ContainingMethod);
        if (!RequireOwnership(state, usage.Invocation, handlerOwnership)) return;
        var methodSubject = DotNetMethodIdentity.SubjectFor(state.Project, usage.ContainingMethod);
        state.Facts.Add(new MartenArtifactFact
        {
            Id = $"marten:handler:{methodSubject.Value}",
            Subject = methodSubject,
            Kind = ArtifactKind.Handler,
            Name = DotNetMethodIdentity.DisplayName(usage.ContainingMethod),
            File = MartenSourceFiles.FileOf(usage.ContainingMethod, state.Project),
            Evidence = usage.Evidence
        }.ToFact(handlerOwnership));
        state.Facts.Add(new MartenRelationshipFact
        {
            Id = $"marten:{kind}:{methodSubject.Value}:{usage.DocumentSubject.Value}:{usage.Invocation.SpanStart}",
            Source = methodSubject,
            Kind = kind,
            Target = usage.DocumentSubject,
            Evidence = usage.Evidence
        }.ToFact(handlerOwnership.Scope, usage.Ownership.Scope));
    }

    static void AddDocumentFacts(DiscoveryState state, SubjectId subject, DocumentObservation observed)
    {
        var identityMember = observed.IdentityConfigurationUnresolved
            ? null
            : observed.IdentityMember ?? ConventionalIdentityMember(observed.Type);
        var properties = DocumentPropertiesOf(observed.Type);
        var identityProperty = identityMember is null
            ? null
            : properties.SingleOrDefault(_ => SymbolEqualityComparer.Default.Equals(_.Member, identityMember));
        if (identityMember is not null && identityProperty is null)
        {
            state.Diagnostics.Add(UnrepresentedIdentityDiagnostic(subject, observed, identityMember));
        }

        state.Facts.Add(new MartenArtifactFact
        {
            Id = $"marten:document:{subject.Value}",
            Subject = subject,
            Kind = ArtifactKind.Document,
            Name = observed.Type.Name,
            File = MartenSourceFiles.FileOf(observed.Type, state.Project),
            Properties = [.. properties.Select(_ => _.Definition with { IsIdentifier = _ == identityProperty })],
            Evidence = observed.RegistrationEvidence
        }.ToFact(observed.Ownership));
    }

    static GenerationDiagnostic UnrepresentedIdentityDiagnostic(SubjectId subject, DocumentObservation observed, ISymbol identityMember) => new()
    {
        Code = MartenDiagnosticCodes.DocumentIdentityUnresolved,
        Severity = GenerationDiagnosticSeverity.Warning,
        Outcome = GenerationDiagnosticOutcome.Unsupported,
        Message = $"Marten identity member '{identityMember.ContainingType?.Name}.{identityMember.Name}' cannot be represented uniquely in the emitted '{observed.Type.Name}' document property shape",
        Source = observed.IdentityEvidence?.Source ?? observed.RegistrationEvidence.Source,
        Subject = subject
    };

    static void ObserveIdentityConfiguration(
        DiscoveryState state,
        InvocationExpressionSyntax invocation,
        IMethodSymbol method,
        SemanticModel semanticModel)
    {
        var candidate = method.ReducedFrom ?? method;
        if (candidate.ContainingType.TypeArguments.FirstOrDefault() is not INamedTypeSymbol documentType ||
            !IsSourceType(documentType))
        {
            return;
        }

        var evidence = IdentityEvidence(state.Project, state.Adapter, invocation, documentType);
        var subject = state.Subjects.SubjectForType(state.Project, documentType);
        var ownership = state.Ownership.ForInvocation(state.Project, invocation);
        if (!RequireOwnership(state, invocation, ownership))
        {
            return;
        }

        var key = BackendOccurrenceKeys.Artifact(subject, ArtifactKind.Document, ownership);
        state.Documents.TryAdd(key, new(documentType, evidence) { Ownership = ownership });
        var identityMember = ResolveIdentityMember(invocation, semanticModel, documentType);
        state.Documents[key] = state.Documents[key] with
        {
            RegistrationEvidence = evidence,
            RegistrationPreference = OmissionDiagnosticEvidencePreference.IdentityConfiguration,
            IdentityMember = identityMember,
            IdentityEvidence = evidence,
            IdentityConfigurationUnresolved = identityMember is null
        };
        if (identityMember is null)
        {
            state.Diagnostics.Add(new GenerationDiagnostic
            {
                Code = MartenDiagnosticCodes.DocumentIdentityUnresolved,
                Severity = GenerationDiagnosticSeverity.Warning,
                Outcome = GenerationDiagnosticOutcome.Unknown,
                Message = $"Marten identity configuration for '{documentType.Name}' is not a direct member expression and cannot be resolved safely",
                Source = evidence.Source,
                Subject = subject
            });
        }
    }

    static IReadOnlyList<DocumentProperty> DocumentPropertiesOf(INamedTypeSymbol documentType) =>
    [
        .. PropertiesOf(documentType).Select(_ => new DocumentProperty(
            _,
            new PropertyDefinition
            {
                Name = LowerFirst(_.Name),
                Type = DotNetTypeShapes.TypeReferenceFor(_.Type)
            }))
    ];

    static string LowerFirst(string value) => value.Length == 0
        ? value
        : $"{char.ToLowerInvariant(value[0])}{value[1..]}";

    sealed class DiscoveryState(
        DotNetProjectCompilation project,
        AdapterIdentity adapter,
        CritterStackSubjectResolver subjects,
        IReadOnlyList<MartenDocumentUsage> projectedDocuments,
        MartenBackendOwnershipResolver ownership)
    {
        public DotNetProjectCompilation Project { get; } = project;

        public AdapterIdentity Adapter { get; } = adapter;

        public CritterStackSubjectResolver Subjects { get; } = subjects;

        public MartenBackendOwnershipResolver Ownership { get; } = ownership;

        public List<GenerationFact> Facts { get; } = [];

        public List<GenerationDiagnostic> Diagnostics { get; } = [];

        public Dictionary<ArtifactKey, DocumentObservation> Documents { get; } = projectedDocuments
            .GroupBy(usage => BackendOccurrenceKeys.Artifact(subjects.SubjectForType(project, usage.Type), ArtifactKind.Document, usage.Ownership))
            .ToDictionary(
                group => group.Key,
                group => new DocumentObservation(group.First().Type, group.First().Evidence) { Ownership = group.First().Ownership });
    }

    sealed record DocumentUsageSite(
        InvocationExpressionSyntax Invocation,
        SubjectId DocumentSubject,
        Evidence Evidence,
        BackendOwnership Ownership);

    sealed record HandlerUsage(
        InvocationExpressionSyntax Invocation,
        IMethodSymbol ContainingMethod,
        SubjectId DocumentSubject,
        Evidence Evidence,
        BackendOwnership Ownership);

    sealed record DocumentProperty(IPropertySymbol Member, PropertyDefinition Definition);

    sealed record DocumentObservation(
        INamedTypeSymbol Type,
        Evidence RegistrationEvidence,
        ISymbol? IdentityMember = null,
        Evidence? IdentityEvidence = null,
        bool IdentityConfigurationUnresolved = false)
    {
        public BackendOwnership Ownership { get; init; } = BackendOwnership.Legacy;

        public OmissionDiagnosticEvidencePreference RegistrationPreference { get; init; } = OmissionDiagnosticEvidencePreference.UseSite;
    }
}
