// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Cratis.Screenplay.Generation;
using Cratis.Screenplay.Generation.DotNet;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Cratis.CritterStack.Screenplay.Marten;

sealed class MartenStoreIndex
{
    readonly DotNetAnalysisContext _context;
    readonly IReadOnlyList<MartenStoreRegistration> _registrations;
    readonly HashSet<SubjectId> _configuredProjections;
    readonly CritterStackSubjectResolver _subjects;

    MartenStoreIndex(DotNetAnalysisContext context, AdapterIdentity adapter, CritterStackSubjectResolver subjects, IReadOnlyList<MartenStoreRegistration> registrations)
    {
        _context = context;
        _registrations = registrations;
        HasExplicitScopes = UsesExplicitScopes(context, registrations);
        _configuredProjections = ConfiguredProjections(context, adapter, subjects);
        _subjects = subjects;
        Diagnostics =
        [
            .. MartenUnboundStoreConfiguration.Discover(context, registrations),
            .. registrations.Where(registration => !registration.Ownership.CanEmit)
                .Select(registration => registration.Ownership.Diagnostic(
                    registration.Subject,
                    registration.Evidence.Source,
                    "Marten store registration identity or event-model configuration cannot be proved"))
        ];
    }

    public bool HasExplicitScopes { get; }

    public IReadOnlyList<GenerationFact> Facts => !HasExplicitScopes ? [] :
    [
        .. _registrations.Where(registration => registration.Ownership.Kind == BackendOwnershipKind.Exact)
            .Select(registration => new BackendScopeFact
            {
                Id = new() { Value = $"marten:backend-scope:{registration.Subject.Value}" },
                Subject = registration.Subject,
                Definition = new() { Key = registration.Ownership.Scope! },
                Evidence = registration.Evidence
            })
    ];

    public IReadOnlyList<GenerationDiagnostic> Diagnostics { get; }

    public static MartenStoreIndex Create(DotNetAnalysisContext context, AdapterIdentity adapter, CritterStackSubjectResolver subjects)
    {
        return new(context, adapter, subjects, MartenStoreRegistrations.Discover(context, adapter, subjects));
    }

    public bool IsConfiguredProjection(DotNetProjectCompilation project, INamedTypeSymbol projection) =>
        _configuredProjections.Contains(_subjects.SubjectForType(project, projection));

    public BackendOwnership Default() => Select(_registrations.Where(registration => registration.Marker is null));

    public BackendOwnership ForMarker(DotNetProjectCompilation project, INamedTypeSymbol marker) =>
        ForMarker(MartenStoreRegistrations.MarkerSubject(_context, _subjects, project, marker));

    public BackendOwnership ForMarker(SubjectId marker) => Select(_registrations.Where(registration => registration.Marker == marker));

    public BackendOwnership ForOptions(DotNetProjectCompilation project, InvocationExpressionSyntax invocation)
    {
        if (!HasExplicitScopes) return BackendOwnership.Legacy;
        var model = project.Compilation.GetSemanticModel(invocation.SyntaxTree);
        var receiver = invocation.Expression is MemberAccessExpressionSyntax access
            ? MartenStoreOptionsBinding.RootSymbol(access.Expression, model)
            : null;
        var registrations = _registrations.Where(registration => registration.Project == project &&
            receiver is not null && registration.Options.Any(option => SymbolEqualityComparer.Default.Equals(option, receiver)))
            .ToArray();
        return Select(registrations);
    }

    static bool UsesExplicitScopes(DotNetAnalysisContext context, IReadOnlyList<MartenStoreRegistration> registrations) =>
        registrations.Any(registration => registration.Marker is not null ||
            !registration.Ownership.CanEmit || registration.Ownership.Scope?.EventModelName is not null) ||
        registrations.Count(registration => registration.Marker is null) > 1 || HasExplicitConfiguration(context);

    static HashSet<SubjectId> ConfiguredProjections(DotNetAnalysisContext context, AdapterIdentity adapter, CritterStackSubjectResolver subjects) =>
    [
        .. context.Projects.SelectMany(project => project.AuthoredSyntaxTrees
                .Where(tree => !DotNetGeneratedSource.IsGenerated(tree))
                .SelectMany(tree => tree.GetRoot().DescendantNodes().OfType<InvocationExpressionSyntax>()
                    .Select(invocation => MartenProjectionDiscovery.RegistrationFrom(invocation, project.Compilation.GetSemanticModel(tree), project, adapter)))
                .OfType<ProjectionRegistration>()
                .Where(registration => registration.Projection is not null)
                .Select(registration => subjects.SubjectForType(project, registration.Projection!)))
    ];

    static bool HasExplicitConfiguration(DotNetAnalysisContext context) => context.Projects.Any(project =>
        project.Compilation.SyntaxTrees.Any(tree => project.AuthoredSyntaxTrees.Contains(tree) &&
            !DotNetGeneratedSource.IsGenerated(tree) && ContainsExplicitConfiguration(tree, project.Compilation.GetSemanticModel(tree))));

    static bool ContainsExplicitConfiguration(SyntaxTree tree, SemanticModel model) => tree.GetRoot().DescendantNodes().Any(node => node switch
    {
        AssignmentExpressionSyntax assignment => IsExplicitAssignment(assignment, model),
        AttributeSyntax attribute => model.GetSymbolInfo(attribute).Symbol is IMethodSymbol constructor &&
            (DotNetSubjectIds.MetadataName(constructor.ContainingType) == "Wolverine.Marten.MartenStoreAttribute" ||
             DotNetSubjectIds.MetadataName(constructor.ContainingType) == "Wolverine.Persistence.StorageAttribute"),
        InvocationExpressionSyntax invocation => model.GetSymbolInfo(invocation).Symbol is IMethodSymbol method &&
            method.ContainingNamespace.ToDisplayString() == "Wolverine" && method.Name.StartsWith("UseAncillaryStorageFrom", StringComparison.Ordinal),
        _ => false
    });

    static bool IsExplicitAssignment(AssignmentExpressionSyntax assignment, SemanticModel model) =>
        (MartenStoreOptionsBinding.IsNameAssignment(assignment, model) && model.GetConstantValue(assignment.Right) is not { HasValue: true, Value: null }) ||
        (model.GetSymbolInfo(assignment.Left).Symbol is IPropertySymbol property && property.Name == "AncillaryStoreType" &&
         property.ContainingNamespace.ToDisplayString().StartsWith("Wolverine", StringComparison.Ordinal));

    BackendOwnership Select(IEnumerable<MartenStoreRegistration> registrations)
    {
        if (!HasExplicitScopes) return BackendOwnership.Legacy;
        var candidates = registrations.ToArray();
        if (candidates.Length == 0 || candidates.Any(candidate => !candidate.Ownership.CanEmit)) return BackendOwnership.Unknown;
        return candidates.Length == 1 ? candidates[0].Ownership : BackendOwnership.Conflict;
    }
}
