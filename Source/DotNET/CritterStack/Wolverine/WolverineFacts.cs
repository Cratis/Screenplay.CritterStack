// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Cratis.CritterStack.Screenplay.Marten;
using Cratis.Screenplay.Generation;
using Cratis.Screenplay.Generation.DotNet;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Cratis.CritterStack.Screenplay.Wolverine;

enum SagaBusEmissionKind
{
    OutgoingMessages,
    DirectBus
}

sealed record WolverineDiscoveryResult(
    IReadOnlyList<GenerationFact> Facts,
    IReadOnlyList<GenerationDiagnostic> Diagnostics,
    IReadOnlyList<CritterStackPlacementIntent>? Placements = null,
    IReadOnlySet<IMethodSymbol>? FailureStageMethods = null);

sealed record HttpEndpoint(IMethodSymbol Method, string Verb, string? Route)
{
    /// <summary>
    /// Gets whether the endpoint is analyzed as a query; other verbs keep their full effect analysis.
    /// </summary>
    public bool IsQuery => string.Equals(Verb, "GET", StringComparison.Ordinal) ||
        string.Equals(Verb, "QUERY", StringComparison.Ordinal);

    /// <summary>
    /// Gets whether the verb is safe but the handler is still analyzed for effects, so the verb alone proves no slice kind.
    /// </summary>
    public bool IsUnclassifiedSafeVerb => string.Equals(Verb, "HEAD", StringComparison.Ordinal) ||
        string.Equals(Verb, "OPTIONS", StringComparison.Ordinal);
}

sealed record WolverineOutgoingMessageConsequence(INamedTypeSymbol MessageType, bool Delayed);

internal static partial class WolverineFacts
{
    static readonly HashSet<string> _persistenceMethods =
    [
        "Append",
        "AppendMany",
        "AppendOne",
        "AppendOptimistic",
        "StartStream"
    ];

    static readonly HashSet<string> _documentPersistenceMethods = ["Delete", "DeleteWhere", "Insert", "Store", "Update"];

    static readonly HashSet<string> _handlerMethodNames =
    [
        "Handle",
        "HandleAsync",
        "Handles",
        "HandlesAsync",
        "Consume",
        "ConsumeAsync",
        "Consumes",
        "ConsumesAsync"
    ];

    static readonly string[] _infrastructureNamespacePrefixes =
    [
        "Marten.",
        "Wolverine.",
        "Microsoft.AspNetCore.",
        "Microsoft.Extensions."
    ];

    public static WolverineDiscoveryResult Discover(
        DotNetProjectCompilation project,
        DotNetAdapterOptions options,
        AdapterIdentity adapter,
        CritterStackSubjectResolver subjects,
        MartenBackendOwnershipResolver? ownership = null,
        AuthoredImplementationManifest? implementations = null)
    {
        if (project.Compilation.GetTypeByMetadataName(WellKnownTypes.WolverineOptions) is null)
        {
            return new([], []);
        }

        var facts = new List<GenerationFact>();
        var placements = new List<CritterStackPlacementIntent>();
        var middlewareRegistrations = WolverinePolicyDiscovery.DiscoverRegistrations(project);
        var discovery = WolverineHandlerDiscovery.Discover(project, subjects, middlewareRegistrations);
        var validationAuthorization = WolverineValidationAuthorizationDiscovery.Discover(project, subjects);
        var diagnostics = new List<GenerationDiagnostic>(discovery.Diagnostics);
        diagnostics.AddRange(validationAuthorization.Diagnostics);
        diagnostics.AddRange(WolverineEventForwardingDiscovery.Discover(project));
        var sagaDiscovery = WolverineSagaFacts.Discover(project, adapter, subjects, discovery.Policy, ownership);
        facts.AddRange(sagaDiscovery.Facts);
        diagnostics.AddRange(sagaDiscovery.Diagnostics);
        var scope = new WolverineFactScope
        {
            Project = project,
            Options = options,
            Adapter = adapter,
            Subjects = subjects,
            Resolver = ownership,
            Implementations = implementations,
            MiddlewareRegistrations = middlewareRegistrations,
            Facts = facts,
            Placements = placements,
            Diagnostics = diagnostics
        };

        var endpoints = AnalyzeHandlers(scope, discovery.Policy, validationAuthorization);
        AnalyzeEndpoints(scope, endpoints, validationAuthorization);
        return new(facts, diagnostics, placements, scope.FailureStageMethods);
    }

    internal static bool IsSagaMessagePayloadType(ITypeSymbol type) => IsEventPayloadType(type);

    internal static IReadOnlyList<PropertyDefinition> AuthoredMessageProperties(
        INamedTypeSymbol messageType,
        DotNetProjectCompilation project) =>
    [
        .. messageType.GetMembers()
            .OfType<IPropertySymbol>()
            .Where(property =>
                !property.IsStatic &&
                !property.IsIndexer &&
                property.DeclaredAccessibility == Accessibility.Public &&
                property.GetMethod?.DeclaredAccessibility == Accessibility.Public &&
                property.Locations.Any(location => IsAuthoredSourceLocation(location, project)))
            .OrderBy(property => property.Locations.FirstOrDefault()?.SourceSpan.Start ?? int.MaxValue)
            .ThenBy(property => property.Name, StringComparer.Ordinal)
            .Select(property => new PropertyDefinition
            {
                Name = LowerFirst(property.Name),
                Type = DotNetTypeShapes.TypeReferenceFor(property.Type)
            })
    ];

    internal static void AddSagaReturnConsequences(
        WolverineSagaEmissionContext context,
        IReadOnlyList<WolverineReturnConsequence> consequences) => AddReturnConsequences(SagaEmissionFor(context), consequences);

    internal static List<WolverineOutgoingMessageConsequence> DiscoverSagaOutgoingMessages(
        IMethodSymbol method,
        DotNetProjectCompilation project) => DiscoverOutgoingMessages(method, project);

    internal static void AddSagaOutgoingMessages(
        WolverineSagaEmissionContext context,
        IMethodSymbol method,
        IReadOnlyList<WolverineOutgoingMessageConsequence> consequences,
        List<GenerationDiagnostic> diagnostics) =>
        AddSagaBusEmission(
            SagaBusEmissionKind.OutgoingMessages,
            SagaEmissionFor(context),
            method,
            consequences,
            diagnostics);

    internal static void AddSagaDirectBusConsequences(
        WolverineSagaEmissionContext context,
        IMethodSymbol method,
        List<GenerationDiagnostic> diagnostics) =>
        AddSagaBusEmission(
            SagaBusEmissionKind.DirectBus,
            SagaEmissionFor(context),
            method,
            [],
            diagnostics);

    static void AddSagaBusEmission(
        SagaBusEmissionKind kind,
        MessageEmission emission,
        IMethodSymbol method,
        IReadOnlyList<WolverineOutgoingMessageConsequence> consequences,
        List<GenerationDiagnostic> diagnostics)
    {
        if (kind == SagaBusEmissionKind.OutgoingMessages)
        {
            AddOutgoingMessages(emission, method, consequences, diagnostics);
        }
        else
        {
            AddDirectBusConsequences(emission, method, diagnostics);
        }
    }

    static List<HttpEndpoint> AnalyzeHandlers(
        WolverineFactScope scope,
        WolverineHandlerDiscoveryPolicy policy,
        WolverineValidationAuthorizationDiscoveryResult validationAuthorization)
    {
        var endpoints = new List<HttpEndpoint>();
        foreach (var entry in ObserveEntryPoints(scope.Project, policy))
        {
            if (entry.Endpoint is { } endpoint)
            {
                endpoints.Add(endpoint);
            }
            else
            {
                AnalyzeWithSlicePattern(scope, entry.Method, null, owned => AnalyzeHandler(owned, entry.Method, validationAuthorization));
            }
        }

        return endpoints;
    }

    // Endpoints are analyzed after handlers so an endpoint that only forwards its command to a handler defers to it.
    static void AnalyzeEndpoints(
        WolverineFactScope scope,
        IReadOnlyList<HttpEndpoint> endpoints,
        WolverineValidationAuthorizationDiscoveryResult validationAuthorization)
    {
        var handledCommands = scope.Facts
            .OfType<ArtifactFact>()
            .Where(_ => _.Definition.Key.Kind == ArtifactKind.Command)
            .Select(_ => _.Definition.Key)
            .ToHashSet();
        foreach (var endpoint in endpoints)
        {
            AnalyzeWithSlicePattern(scope, endpoint.Method, endpoint, owned => AnalyzeEndpoint(owned, endpoint, validationAuthorization, handledCommands));
        }
    }

    static void AnalyzeWithSlicePattern(WolverineFactScope scope, IMethodSymbol method, HttpEndpoint? endpoint, Action<WolverineFactScope> analyze)
    {
        var request = RequestParameter(method, scope.Project);
        var slicePattern = WolverineSlicePatterns.Resolve(
            scope,
            method,
            request is not null ? MessageElementType(request.Type) : null,
            endpoint);
        var firstPlacement = scope.Placements.Count;
        var ownership = scope.Resolver?.ForMethod(scope.Project, method) ?? BackendOwnership.Legacy;
        analyze(scope.WithOwnership(ownership));
        WolverineSlicePatterns.Apply(slicePattern, scope, method, firstPlacement);
    }
}

sealed class WolverineSagaEmissionContext
{
    public BackendOwnership Ownership { get; init; } = BackendOwnership.Legacy;

    public required DotNetProjectCompilation Project { get; init; }

    public required CritterStackSubjectResolver Subjects { get; init; }

    public required SubjectId SourceSubject { get; init; }

    public required Evidence Evidence { get; init; }

    public required List<GenerationFact> Facts { get; init; }
}

/// <summary>
/// The shared state a Wolverine discovery pass reads from and writes facts, placements and diagnostics to.
/// </summary>
sealed class WolverineFactScope
{
    public required DotNetProjectCompilation Project { get; init; }

    public required DotNetAdapterOptions Options { get; init; }

    public required AdapterIdentity Adapter { get; init; }

    public required CritterStackSubjectResolver Subjects { get; init; }

    public AuthoredImplementationManifest? Implementations { get; init; }

    public required WolverineMiddlewareRegistrations MiddlewareRegistrations { get; init; }

    public required List<GenerationFact> Facts { get; init; }

    public required List<CritterStackPlacementIntent> Placements { get; init; }

    public required List<GenerationDiagnostic> Diagnostics { get; init; }

    public MartenBackendOwnershipResolver? Resolver { get; init; }

    public BackendOwnership Ownership { get; init; } = BackendOwnership.Legacy;

    public HashSet<IMethodSymbol> FailureStageMethods { get; init; } = [with(SymbolEqualityComparer.Default)];

    public WolverineFactScope WithOwnership(BackendOwnership ownership) => new()
    {
        Project = Project,
        Options = Options,
        Adapter = Adapter,
        Subjects = Subjects,
        Implementations = Implementations,
        Resolver = Resolver,
        Ownership = ownership,
        MiddlewareRegistrations = MiddlewareRegistrations,
        Facts = Facts,
        Placements = Placements,
        Diagnostics = Diagnostics,
        FailureStageMethods = FailureStageMethods
    };

    public ArtifactKey Key(SubjectId subject, ArtifactKind kind) => BackendOccurrenceKeys.Artifact(subject, kind, Ownership);

    public string PlacementId(string legacy) => BackendOccurrenceKeys.FactId(legacy, Ownership.Scope);

    public bool RequireOwnership(IMethodSymbol method)
    {
        if (Ownership.CanEmit)
        {
            return true;
        }

        Diagnostics.Add(Ownership.Diagnostic(
            DotNetMethodIdentity.SubjectFor(Project, method),
            CritterStackSource.EvidenceFor(method, Adapter, Project, EvidenceStrength.Exact).Source,
            "Marten-backed handler ownership cannot be proved"));
        return false;
    }
}
