// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Cratis.CritterStack.Screenplay.Marten;
using Cratis.Screenplay.Generation;
using Cratis.Screenplay.Generation.DotNet;
using Microsoft.CodeAnalysis;

namespace Cratis.CritterStack.Screenplay.Wolverine;

internal static partial class WolverineFacts
{
    static void AnalyzeEndpoint(
        WolverineFactScope scope,
        HttpEndpoint endpoint,
        WolverineValidationAuthorizationDiscoveryResult validationAuthorization,
        HashSet<ArtifactKey> handledCommands)
    {
        var stages = EndpointStages(scope, endpoint);
        if (endpoint.IsQuery)
        {
            AnalyzeQueryEndpoint(scope, endpoint, validationAuthorization, stages);
            return;
        }

        var command = DescribeEndpointCommand(scope, endpoint);
        var shape = command.Shape;
        if (EndpointCommandScope(scope, command) is not { } commandScope)
        {
            return;
        }

        scope = commandScope;
        var productionDiagnostics = new List<GenerationDiagnostic>();
        var productions = WolverineProductionPlan.Decide(
            shape.ToProductionSources(
                command.CommandName,
                command.CommandSubject,
                command.Evidence.Source,
                ShortCircuitsEndpoint(validationAuthorization, endpoint, stages)),
            productionDiagnostics);
        if (IsForwardedCommand(scope, command, productions, handledCommands))
        {
            AddForwarderDiagnostics(scope, command, validationAuthorization);
            AddResolvedCompoundStageConsequences(scope, command.CommandSubject, endpoint.Method, stages);
            return;
        }

        scope.Diagnostics.AddRange(productionDiagnostics);
        var placement = EndpointPlacementFor(scope, command);

        AddEndpointCommandFacts(scope, command, productions, placement);
        AddEndpointAggregateFacts(scope, command);
        AddStateReadFacts(scope, command.CommandSubject, command.CommandName, shape, isHttpEndpoint: true);
        AddEndpointValidationDiagnostics(scope, command, validationAuthorization);

        var eventOwner = placement.SourceOwner ?? command.CommandSubject;
        AddProductionFacts(scope, command.CommandSubject, eventOwner, productions, shape, placement.Compatibility, command.Evidence);
        AddEffectFacts(scope, command.CommandSubject, shape, command.Evidence);
        AddResolvedCompoundStageConsequences(scope, command.CommandSubject, endpoint.Method, stages);
    }

    static IReadOnlyList<WolverineCompoundStage> EndpointStages(WolverineFactScope scope, HttpEndpoint endpoint)
    {
        var stageRequestType = RequestParameter(endpoint.Method, scope.Project)?.Type ?? endpoint.Method.ContainingType;
        return WolverineCompoundStages.StagesFor(endpoint.Method, stageRequestType, scope, isHttpEndpoint: true);
    }

    static bool ShortCircuitsEndpoint(
        WolverineValidationAuthorizationDiscoveryResult validationAuthorization,
        HttpEndpoint endpoint,
        IReadOnlyList<WolverineCompoundStage> stages) =>
        validationAuthorization.HasCompoundValidation(endpoint.Method) || HasShortCircuitingStage(stages);

    static void AnalyzeQueryEndpoint(
        WolverineFactScope scope,
        HttpEndpoint endpoint,
        WolverineValidationAuthorizationDiscoveryResult validationAuthorization,
        IReadOnlyList<WolverineCompoundStage> stages)
    {
        if (!HasMartenOperations(scope, endpoint.Method) && !endpoint.Method.Parameters.Any(IsPersistenceBoundParameter))
        {
            scope = scope.WithOwnership(BackendOwnership.Legacy);
        }

        AnalyzeQuery(scope, endpoint, validationAuthorization, stages);
    }

    static WolverineFactScope? EndpointCommandScope(WolverineFactScope scope, EndpointCommand command)
    {
        if (HasNoPersistenceEffects(command.Shape, scope.Project) && !HasMartenOperations(scope, command.Endpoint.Method))
        {
            return scope.WithOwnership(BackendOwnership.Legacy);
        }

        return scope.RequireOwnership(command.Endpoint.Method) ? scope : null;
    }

    static EndpointPlacement EndpointPlacementFor(WolverineFactScope scope, EndpointCommand command)
    {
        var shape = command.Shape;
        var feature = StateFeature(command.CommandName, shape.AggregateType, shape.StreamBindings, shape.Dcb?.ModelType);
        var compatibilityPlacement = CritterStackSourcePlacement.CompatibilityPlacement(
            scope.Project,
            scope.Options,
            feature,
            command.CommandName,
            GenerationSliceKind.StateChange);
        var commandSourceOwner = command.CommandType is null
            ? scope.Subjects.SubjectForType(scope.Project, command.Endpoint.Method.ContainingType)
            : null;
        return new(compatibilityPlacement, commandSourceOwner);
    }

    static EndpointCommand DescribeEndpointCommand(WolverineFactScope scope, HttpEndpoint endpoint)
    {
        var project = scope.Project;
        var method = endpoint.Method;
        var request = RequestParameter(method, project);
        var commandType = request?.Type as INamedTypeSymbol;
        return new EndpointCommand
        {
            Endpoint = endpoint,
            CommandType = commandType,
            Shape = DiscoverStateChangeShape(method, request, commandType, scope, isHttpEndpoint: true),
            CommandSubject = commandType is not null
                ? scope.Subjects.SubjectForType(project, commandType)
                : MethodSubject(project, method, "command"),
            CommandName = EndpointCommandName(method, request),
            Evidence = MethodEvidence(method, project, scope.Adapter, EvidenceStrength.Exact, $"Wolverine HTTP {endpoint.Verb} endpoint")
        };
    }

    static string EndpointCommandName(IMethodSymbol method, IParameterSymbol? request)
    {
        if (request is not null)
        {
            return request.Type.Name;
        }

        return method.ContainingType.Name.EndsWith("Endpoints", StringComparison.Ordinal)
            ? $"{method.Name}{method.Parameters.FirstOrDefault(IsEntityParameter)?.Type.Name}"
            : method.ContainingType.Name.Replace("Endpoint", string.Empty, StringComparison.Ordinal);
    }

    static bool IsForwardedCommand(
        WolverineFactScope scope,
        EndpointCommand command,
        IReadOnlyList<WolverineProductionDecision> productions,
        HashSet<ArtifactKey> handledCommands) =>
        command.CommandType is not null &&
        handledCommands.Contains(scope.Key(command.CommandSubject, ArtifactKind.Command)) &&
        IsHandlerForwarder(scope.Project, command.Shape, command.CommandType, productions);

    static bool IsHandlerForwarder(
        DotNetProjectCompilation project,
        StateChangeShape shape,
        INamedTypeSymbol commandType,
        IReadOnlyList<WolverineProductionDecision> productions)
    {
        return HasNoStateEffects(project, shape, productions) &&
               ForwardsOnlyCommand(project, shape, commandType);
    }

    static bool HasNoStateEffects(
        DotNetProjectCompilation project,
        StateChangeShape shape,
        IReadOnlyList<WolverineProductionDecision> productions) =>
        shape.Aggregate is null &&
        shape.Dcb is null &&
        shape.StreamBindings.Count == 0 &&
        !shape.AppendDiscovery.HasDirectWrite &&
        productions.Count == 0 &&
        !shape.Method.Parameters.Any(IsPersistenceBoundParameter) &&
        !HasDocumentPersistence(shape.Method, project);

    static bool ForwardsOnlyCommand(
        DotNetProjectCompilation project,
        StateChangeShape shape,
        INamedTypeSymbol commandType) =>
        shape.BusConsequences is [var consequence] &&
        SymbolEqualityComparer.Default.Equals(consequence.MessageType, commandType) &&
        shape.OutgoingMessages.Count == 0 &&
        WolverineReturnConsequences.Classify(shape.Method, project, isHttpEndpoint: true, aggregateWorkflow: false, hasEventStream: false)
            .All(_ => _.Kind == WolverineReturnConsequenceKind.HttpResponse);

    static void AddForwarderDiagnostics(
        WolverineFactScope scope,
        EndpointCommand command,
        WolverineValidationAuthorizationDiscoveryResult validationAuthorization)
    {
        var endpoint = command.Endpoint;
        scope.Diagnostics.Add(new GenerationDiagnostic
        {
            Code = WolverineDiagnosticCodes.HttpMetadataOmitted,
            Severity = GenerationDiagnosticSeverity.Information,
            Outcome = GenerationDiagnosticOutcome.Unsupported,
            Message = $"HTTP {endpoint.Verb} route '{endpoint.Route}' forwards '{command.CommandType!.Name}' to its Wolverine handler; the route is not represented by the current Screenplay language",
            Source = command.Evidence.Source,
            Subject = command.CommandSubject
        });
        AddEndpointValidationDiagnostics(scope, command, validationAuthorization);
    }

    static void AddEndpointValidationDiagnostics(
        WolverineFactScope scope,
        EndpointCommand command,
        WolverineValidationAuthorizationDiscoveryResult validationAuthorization)
    {
        var method = command.Endpoint.Method;
        if (command.CommandType is not null)
        {
            scope.Diagnostics.AddRange(validationAuthorization.ValidationDiagnostics(
                method,
                command.CommandType,
                command.CommandSubject,
                isHttpEndpoint: true));
        }

        scope.Diagnostics.AddRange(validationAuthorization.AuthorizationDiagnostics(method, command.CommandSubject));
    }

    static void AddEndpointCommandFacts(
        WolverineFactScope scope,
        EndpointCommand command,
        IReadOnlyList<WolverineProductionDecision> productions,
        EndpointPlacement placement)
    {
        var commandKey = scope.Key(command.CommandSubject, ArtifactKind.Command);
        scope.Facts.Add(Artifact(
            $"wolverine:command:{command.CommandSubject.Value}",
            commandKey,
            command.CommandName,
            command.Evidence.Source?.Path,
            EndpointCommandProperties(scope, command, productions),
            command.Evidence));
        scope.Placements.Add(new(
            scope.PlacementId($"wolverine:placement:command:{command.CommandSubject.Value}"),
            commandKey,
            placement.SourceOwner,
            placement.Compatibility,
            command.Evidence));
        scope.Diagnostics.Add(new GenerationDiagnostic
        {
            Code = WolverineDiagnosticCodes.HttpMetadataOmitted,
            Severity = GenerationDiagnosticSeverity.Information,
            Outcome = GenerationDiagnosticOutcome.Unsupported,
            Message = $"HTTP {command.Endpoint.Verb} route '{command.Endpoint.Route}' for '{command.CommandName}' is not represented by the current Screenplay language",
            Source = command.Evidence.Source,
            Subject = command.CommandSubject
        });
    }

    static IReadOnlyList<PropertyDefinition> EndpointCommandProperties(
        WolverineFactScope scope,
        EndpointCommand command,
        IReadOnlyList<WolverineProductionDecision> productions)
    {
        return command.CommandType is not null
            ? CommandProperties(command.CommandType, command.Shape, productions)
            : QueryProperties(command.Endpoint.Method, scope.Subjects);
    }

    static void AddEndpointAggregateFacts(WolverineFactScope scope, EndpointCommand command)
    {
        if (command.Shape.AggregateType is not { } aggregateType)
        {
            return;
        }

        AddReadModelAndRelationship(scope, command.CommandSubject, command.CommandType, aggregateType, command.Evidence);
        if (command.CommandType is not null && IdentityProperty(command.CommandType, aggregateType) is null)
        {
            scope.Diagnostics.Add(new GenerationDiagnostic
            {
                Code = WolverineDiagnosticCodes.RouteIdentityOmitted,
                Severity = GenerationDiagnosticSeverity.Warning,
                Outcome = GenerationDiagnosticOutcome.Unsupported,
                Message = $"The '{aggregateType.Name}' identity for '{command.CommandName}' comes from the HTTP route rather than a command property and cannot be marked as a Screenplay identifier",
                Source = command.Evidence.Source,
                Subject = command.CommandSubject
            });
        }

        if (command.CommandType?.GetMembers().OfType<IPropertySymbol>().Any(_ => _.Name == "Version") == true)
        {
            scope.Diagnostics.Add(new GenerationDiagnostic
            {
                Code = WolverineDiagnosticCodes.StreamVersionOmitted,
                Severity = GenerationDiagnosticSeverity.Information,
                Outcome = GenerationDiagnosticOutcome.Unsupported,
                Message = $"The expected stream version on '{command.CommandName}' cannot be represented exactly by Screenplay concurrency",
                Source = command.Evidence.Source,
                Subject = command.CommandSubject
            });
        }
    }

    static void AnalyzeQuery(
        WolverineFactScope scope,
        HttpEndpoint endpoint,
        WolverineValidationAuthorizationDiscoveryResult validationAuthorization,
        IReadOnlyList<WolverineCompoundStage> stages)
    {
        var project = scope.Project;
        var querySubject = MethodSubject(project, endpoint.Method, "query");
        if (RequestParameter(endpoint.Method, project)?.Type is INamedTypeSymbol validationRequestType)
        {
            scope.Diagnostics.AddRange(validationAuthorization.ValidationDiagnostics(
                endpoint.Method,
                validationRequestType,
                querySubject,
                isHttpEndpoint: true));
        }

        scope.Diagnostics.AddRange(validationAuthorization.AuthorizationDiagnostics(endpoint.Method, querySubject));

        var (model, isCollection, isOptional) = WolverineReturnTypes.QueryModel(endpoint.Method.ReturnType);
        if (model is null || !IsQueryModel(model, project))
        {
            return;
        }

        var evidence = MethodEvidence(endpoint.Method, project, scope.Adapter, EvidenceStrength.Exact, $"Wolverine HTTP {endpoint.Verb} endpoint");
        var compiledQueryDiscovery = MartenCompiledQueryDiscovery.Discover(endpoint.Method, querySubject, project, scope.Adapter, scope.Subjects);
        var compiledQueries = compiledQueryDiscovery.Links;
        scope.Diagnostics.AddRange(compiledQueryDiscovery.Diagnostics);
        var queryName = endpoint.Method.ContainingType.Name.EndsWith("Endpoints", StringComparison.Ordinal)
            ? endpoint.Method.Name
            : endpoint.Method.ContainingType.Name.Replace("Endpoint", string.Empty, StringComparison.Ordinal);
        var compatibilityPlacement = CritterStackSourcePlacement.CompatibilityPlacement(
            project,
            scope.Options,
            model.Name,
            queryName,
            GenerationSliceKind.StateView);

        var query = new EndpointQuery(endpoint, querySubject, queryName, evidence);
        AddQueryFacts(scope, query, compatibilityPlacement, compiledQueries);
        var sessionReads = new MartenSessionReadDiscovery(project, scope.Adapter, scope.Resolver).Discover(endpoint.Method, scope.Ownership);
        var queryScope = scope.WithOwnership(QueryResultOwnership(scope, sessionReads));
        if (!queryScope.RequireOwnership(endpoint.Method))
        {
            return;
        }

        AddQueryModelFacts(queryScope, query, new QueryResultModel(model, isCollection, isOptional));
        AddSessionQueryReads(scope, querySubject, sessionReads);
        AddPersistenceBoundReads(scope, querySubject, endpoint.Method, aggregateParameter: null, unscopedSource: true);
        AddResolvedCompoundStageConsequences(scope, querySubject, endpoint.Method, stages);
    }

    static bool IsQueryModel(INamedTypeSymbol model, DotNetProjectCompilation project) =>
        IsSourceType(model) && !WolverineSagaTypes.IsSagaState(model, project);

    static void AddQueryFacts(
        WolverineFactScope scope,
        EndpointQuery query,
        ArtifactPlacement compatibilityPlacement,
        IReadOnlyList<MartenCompiledQueryLink> compiledQueries)
    {
        var (endpoint, querySubject, queryName, evidence) = query;
        var queryKey = new ArtifactKey { Subject = querySubject, Kind = ArtifactKind.Query };
        scope.Facts.Add(Artifact(
            $"wolverine:query:{querySubject.Value}",
            queryKey,
            queryName,
            evidence.Source?.Path,
            QueryProperties(endpoint.Method, scope.Subjects, compiledQueries.SelectMany(_ => _.Parameters)),
            evidence));
        scope.Placements.Add(new(
            $"wolverine:placement:query:{querySubject.Value}",
            queryKey,
            scope.Subjects.SubjectForType(scope.Project, endpoint.Method.ContainingType),
            compatibilityPlacement,
            evidence));
        scope.Diagnostics.Add(new GenerationDiagnostic
        {
            Code = WolverineDiagnosticCodes.HttpMetadataOmitted,
            Severity = GenerationDiagnosticSeverity.Information,
            Outcome = GenerationDiagnosticOutcome.Unsupported,
            Message = $"HTTP {endpoint.Verb} route '{endpoint.Route}' for query '{queryName}' is not represented by the current Screenplay language",
            Source = evidence.Source,
            Subject = querySubject
        });
    }

    static void AddQueryModelFacts(WolverineFactScope scope, EndpointQuery query, QueryResultModel result)
    {
        var (_, querySubject, _, evidence) = query;
        var (model, isCollection, isOptional) = result;
        var modelSubject = scope.Subjects.SubjectForType(scope.Project, model);
        var modelKey = scope.Key(modelSubject, ArtifactKind.ReadModel);
        scope.Facts.Add(Artifact(
            $"wolverine:read-model:{modelSubject.Value}",
            modelKey,
            model.Name,
            SourceFileOf(model, scope.Project),
            DotNetTypeShapes.PropertiesOf(model),
            evidence));
        scope.Placements.Add(new(
            scope.PlacementId($"wolverine:placement:read-model:{modelSubject.Value}"),
            modelKey,
            null,
            CritterStackSourcePlacement.CompatibilityPlacement(
                scope.Project,
                scope.Options,
                model.Name,
                model.Name,
                GenerationSliceKind.StateView),
            evidence with
            {
                Strength = EvidenceStrength.Heuristic,
                Explanation = "The returned model name provides the default Screenplay placement"
            }));
        scope.Facts.Add(Relationship(scope, new WolverineRelationshipFact(
            $"wolverine:returns:{querySubject.Value}:{modelSubject.Value}",
            querySubject,
            RelationshipKind.Returns,
            modelSubject,
            evidence)
        {
            IsCollection = isCollection,
            IsOptional = isOptional,
            UnscopedSource = true
        }));
    }

    static HttpEndpoint? EndpointFor(IMethodSymbol method)
    {
        var attribute = method.GetAttributes().FirstOrDefault(_ =>
            _.AttributeClass is not null &&
            (DotNetSymbols.IsOrInheritsFrom(_.AttributeClass, WellKnownTypes.WolverineHttpMethodAttribute) ||
             (_.AttributeClass.ContainingNamespace.ToDisplayString() == "Wolverine.Http" &&
              _.AttributeClass.Name.StartsWith("Wolverine", StringComparison.Ordinal) &&
              _.AttributeClass.Name.EndsWith("Attribute", StringComparison.Ordinal))));
        if (attribute?.AttributeClass is null)
        {
            return null;
        }

        var verb = attribute.AttributeClass.Name
            .Replace("Wolverine", string.Empty, StringComparison.Ordinal)
            .Replace("Attribute", string.Empty, StringComparison.Ordinal)
            .ToUpperInvariant();
        var route = attribute.ConstructorArguments.FirstOrDefault().Value as string;
        return new(method, verb, route);
    }

    sealed record EndpointPlacement(ArtifactPlacement Compatibility, SubjectId? SourceOwner);

    sealed record EndpointQuery(HttpEndpoint Endpoint, SubjectId Subject, string Name, Evidence Evidence);

    sealed record QueryResultModel(INamedTypeSymbol Model, bool IsCollection, bool IsOptional);

    sealed class EndpointCommand
    {
        public required HttpEndpoint Endpoint { get; init; }

        public required INamedTypeSymbol? CommandType { get; init; }

        public required StateChangeShape Shape { get; init; }

        public required SubjectId CommandSubject { get; init; }

        public required string CommandName { get; init; }

        public required Evidence Evidence { get; init; }
    }
}
