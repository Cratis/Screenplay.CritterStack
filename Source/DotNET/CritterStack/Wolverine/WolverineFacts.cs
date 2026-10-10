// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Cratis.CritterStack.Screenplay.Marten;
using Cratis.Screenplay.Generation;
using Cratis.Screenplay.Generation.DotNet;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Cratis.CritterStack.Screenplay.Wolverine;

sealed record WolverineDiscoveryResult(
    IReadOnlyList<GenerationFact> Facts,
    IReadOnlyList<GenerationDiagnostic> Diagnostics,
    IReadOnlyList<CritterStackPlacementIntent>? Placements = null);

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

/// <summary>
/// The shared state a Wolverine discovery pass reads from and writes facts, placements and diagnostics to.
/// </summary>
sealed class WolverineFactScope
{
    public required DotNetProjectCompilation Project { get; init; }

    public required DotNetAdapterOptions Options { get; init; }

    public required AdapterIdentity Adapter { get; init; }

    public required CritterStackSubjectResolver Subjects { get; init; }

    public required List<GenerationFact> Facts { get; init; }

    public required List<CritterStackPlacementIntent> Placements { get; init; }

    public required List<GenerationDiagnostic> Diagnostics { get; init; }
}

static partial class WolverineFacts
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

    public static WolverineDiscoveryResult Discover(
        DotNetProjectCompilation project,
        DotNetAdapterOptions options,
        AdapterIdentity adapter,
        CritterStackSubjectResolver subjects)
    {
        if (project.Compilation.GetTypeByMetadataName(WellKnownTypes.WolverineOptions) is null)
        {
            return new([], []);
        }

        var facts = new List<GenerationFact>();
        var placements = new List<CritterStackPlacementIntent>();
        var discovery = WolverineHandlerDiscovery.Discover(project, subjects);
        var validationAuthorization = WolverineValidationAuthorizationDiscovery.Discover(project, subjects);
        var diagnostics = new List<GenerationDiagnostic>(discovery.Diagnostics);
        diagnostics.AddRange(validationAuthorization.Diagnostics);
        var sagaDiscovery = WolverineSagaFacts.Discover(project, adapter, subjects, discovery.Policy);
        facts.AddRange(sagaDiscovery.Facts);
        diagnostics.AddRange(sagaDiscovery.Diagnostics);
        var scope = new WolverineFactScope
        {
            Project = project,
            Options = options,
            Adapter = adapter,
            Subjects = subjects,
            Facts = facts,
            Placements = placements,
            Diagnostics = diagnostics
        };

        var endpoints = AnalyzeHandlers(scope, discovery.Policy, validationAuthorization);
        AnalyzeEndpoints(scope, endpoints, validationAuthorization);
        return new(facts, diagnostics, placements);
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
        DotNetProjectCompilation project,
        CritterStackSubjectResolver subjects,
        SubjectId sourceSubject,
        IReadOnlyList<WolverineReturnConsequence> consequences,
        Evidence evidence,
        List<GenerationFact> facts) => AddReturnConsequences(project, subjects, sourceSubject, consequences, evidence, facts, sagaAnalysis: true);

    internal static List<WolverineOutgoingMessageConsequence> DiscoverSagaOutgoingMessages(
        IMethodSymbol method,
        DotNetProjectCompilation project) => DiscoverOutgoingMessages(method, project);

    internal static void AddSagaOutgoingMessages(
        DotNetProjectCompilation project,
        CritterStackSubjectResolver subjects,
        SubjectId sourceSubject,
        IMethodSymbol method,
        IReadOnlyList<WolverineOutgoingMessageConsequence> consequences,
        Evidence evidence,
        List<GenerationFact> facts,
        List<GenerationDiagnostic> diagnostics) =>
        AddOutgoingMessages(project, subjects, sourceSubject, method, consequences, evidence, facts, diagnostics, sagaAnalysis: true);

    internal static void AddSagaDirectBusConsequences(
        DotNetProjectCompilation project,
        CritterStackSubjectResolver subjects,
        SubjectId sourceSubject,
        IMethodSymbol method,
        Evidence evidence,
        List<GenerationFact> facts,
        List<GenerationDiagnostic> diagnostics) =>
        AddDirectBusConsequences(project, subjects, sourceSubject, method, evidence, facts, diagnostics, sagaAnalysis: true);

    static List<HttpEndpoint> AnalyzeHandlers(
        WolverineFactScope scope,
        WolverineHandlerDiscoveryPolicy policy,
        WolverineValidationAuthorizationDiscoveryResult validationAuthorization)
    {
        var endpoints = new List<HttpEndpoint>();
        foreach (var (type, method) in CandidateMethods(scope.Project))
        {
            var endpoint = EndpointFor(method);
            if (endpoint is not null)
            {
                endpoints.Add(endpoint);
            }
            else if (IsHandler(type, method, policy))
            {
                AnalyzeWithSlicePattern(scope, method, null, () => AnalyzeHandler(scope, method, validationAuthorization));
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
            .Select(_ => _.Definition.Key.Subject)
            .ToHashSet();
        foreach (var endpoint in endpoints)
        {
            AnalyzeWithSlicePattern(scope, endpoint.Method, endpoint, () => AnalyzeEndpoint(scope, endpoint, validationAuthorization, handledCommands));
        }
    }

    static IEnumerable<(INamedTypeSymbol Type, IMethodSymbol Method)> CandidateMethods(DotNetProjectCompilation project)
    {
        var catalog = new DotNetArtifactCatalog(project.Compilation);
        foreach (var type in catalog.Types.Where(_ => IsCandidateType(_, project)))
        {
            foreach (var method in type.GetMembers().OfType<IMethodSymbol>().Where(_ => IsPublicSourceMethod(_, project) && !IsIgnored(_)))
            {
                yield return (type, method);
            }
        }
    }

    static bool IsCandidateType(INamedTypeSymbol type, DotNetProjectCompilation project) =>
        IsPublicSourceType(type, project) && !IsIgnored(type) && !WolverineSagaFacts.IsSagaType(type, project);

    static void AnalyzeWithSlicePattern(WolverineFactScope scope, IMethodSymbol method, HttpEndpoint? endpoint, Action analyze)
    {
        var request = RequestParameter(method, scope.Project);
        var slicePattern = WolverineSlicePatterns.Resolve(
            method,
            request is not null ? MessageElementType(request.Type) : null,
            endpoint,
            scope.Project,
            scope.Adapter,
            scope.Diagnostics);
        var firstPlacement = scope.Placements.Count;
        analyze();
        WolverineSlicePatterns.Apply(slicePattern, method, scope.Project, scope.Placements, firstPlacement, scope.Diagnostics);
    }
}
