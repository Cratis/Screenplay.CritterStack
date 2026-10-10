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
        HashSet<SubjectId> handledCommands)
    {
        if (endpoint.IsQuery)
        {
            AnalyzeQuery(scope, endpoint, validationAuthorization);
            return;
        }

        var command = DescribeEndpointCommand(scope, endpoint);
        var shape = command.Shape;
        var productionDiagnostics = new List<GenerationDiagnostic>();
        var productions = WolverineProductionPlan.Decide(
            shape.ToProductionSources(
                command.CommandName,
                command.CommandSubject,
                command.Evidence.Source,
                validationAuthorization.HasCompoundValidation(endpoint.Method)),
            productionDiagnostics);
        if (IsForwardedCommand(scope, command, productions, handledCommands))
        {
            AddForwarderDiagnostics(scope, command, validationAuthorization);
            return;
        }

        scope.Diagnostics.AddRange(productionDiagnostics);
        var feature = StateFeature(command.CommandName, shape.AggregateType, shape.StreamBindings, shape.Dcb?.ModelType);
        var compatibilityPlacement = CritterStackSourcePlacement.CompatibilityPlacement(
            scope.Project,
            scope.Options,
            feature,
            command.CommandName,
            GenerationSliceKind.StateChange);
        var commandSourceOwner = command.CommandType is null
            ? scope.Subjects.SubjectForType(scope.Project, endpoint.Method.ContainingType)
            : null;

        AddEndpointCommandFacts(scope, command, productions, compatibilityPlacement, commandSourceOwner);
        AddEndpointAggregateFacts(scope, command);
        AddStateReadFacts(scope, command.CommandSubject, command.CommandName, shape, isHttpEndpoint: true);
        AddEndpointValidationDiagnostics(scope, command, validationAuthorization);

        var eventOwner = commandSourceOwner ?? command.CommandSubject;
        AddProductionFacts(scope, command.CommandSubject, eventOwner, productions, shape, compatibilityPlacement, command.Evidence);
        AddEffectFacts(scope, command.CommandSubject, shape, command.Evidence);
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
            Shape = DiscoverStateChangeShape(method, request, commandType, project, isHttpEndpoint: true),
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
        HashSet<SubjectId> handledCommands) =>
        command.CommandType is not null &&
        handledCommands.Contains(command.CommandSubject) &&
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
        ArtifactPlacement compatibilityPlacement,
        SubjectId? commandSourceOwner)
    {
        var commandKey = new ArtifactKey { Subject = command.CommandSubject, Kind = ArtifactKind.Command };
        scope.Facts.Add(Artifact(
            $"wolverine:command:{command.CommandSubject.Value}",
            commandKey,
            command.CommandName,
            command.Evidence.Source?.Path,
            EndpointCommandProperties(scope, command, productions),
            command.Evidence));
        scope.Placements.Add(new(
            $"wolverine:placement:command:{command.CommandSubject.Value}",
            commandKey,
            commandSourceOwner,
            compatibilityPlacement,
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
        var shape = command.Shape;
        return command.CommandType is not null
            ? CommandProperties(command.CommandType, shape.AggregateType, shape.ParameterBindings, shape.FetchedBindings.Count > 0, LinkedStreamIdentity(productions))
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
        WolverineValidationAuthorizationDiscoveryResult validationAuthorization)
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

        AddQueryFacts(scope, endpoint, querySubject, queryName, compatibilityPlacement, compiledQueries, evidence);
        AddQueryModelFacts(scope, querySubject, model, evidence, isCollection, isOptional);
        AddPersistenceBoundReads(scope, querySubject, endpoint.Method, aggregateParameter: null);
        AddCompiledQueryReads(scope, querySubject, compiledQueries);
    }

    static bool IsQueryModel(INamedTypeSymbol model, DotNetProjectCompilation project) =>
        IsSourceType(model) && !WolverineSagaTypes.IsSagaState(model, project);

    static void AddQueryFacts(
        WolverineFactScope scope,
        HttpEndpoint endpoint,
        SubjectId querySubject,
        string queryName,
        ArtifactPlacement compatibilityPlacement,
        IReadOnlyList<MartenCompiledQueryLink> compiledQueries,
        Evidence evidence)
    {
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

    static void AddQueryModelFacts(
        WolverineFactScope scope,
        SubjectId querySubject,
        INamedTypeSymbol model,
        Evidence evidence,
        bool isCollection,
        bool isOptional)
    {
        var modelSubject = scope.Subjects.SubjectForType(scope.Project, model);
        var modelKey = new ArtifactKey { Subject = modelSubject, Kind = ArtifactKind.ReadModel };
        scope.Facts.Add(Artifact(
            $"wolverine:read-model:{modelSubject.Value}",
            modelKey,
            model.Name,
            SourceFileOf(model, scope.Project),
            DotNetTypeShapes.PropertiesOf(model),
            evidence));
        scope.Placements.Add(new(
            $"wolverine:placement:read-model:{modelSubject.Value}",
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
        scope.Facts.Add(Relationship(
            $"wolverine:returns:{querySubject.Value}:{modelSubject.Value}",
            querySubject,
            RelationshipKind.Returns,
            modelSubject,
            evidence,
            isCollection: isCollection,
            isOptional: isOptional));
    }

    static void AddCompiledQueryReads(
        WolverineFactScope scope,
        SubjectId querySubject,
        IReadOnlyList<MartenCompiledQueryLink> compiledQueries)
    {
        foreach (var compiledQuery in compiledQueries
                     .GroupBy(_ => scope.Subjects.SubjectForType(scope.Project, _.DocumentType))
                     .Select(_ => _.First()))
        {
            var documentSubject = scope.Subjects.SubjectForType(scope.Project, compiledQuery.DocumentType);
            scope.Facts.Add(Relationship(
                $"wolverine:reads:compiled:{querySubject.Value}:{documentSubject.Value}",
                querySubject,
                RelationshipKind.Reads,
                documentSubject,
                compiledQuery.Evidence));
        }
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
