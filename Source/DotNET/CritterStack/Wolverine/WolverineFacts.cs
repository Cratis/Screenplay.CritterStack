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
    IReadOnlyList<GenerationDiagnostic> Diagnostics);

sealed record HttpEndpoint(IMethodSymbol Method, string Verb, string? Route);

sealed record WolverineOutgoingMessageConsequence(INamedTypeSymbol MessageType, bool Delayed);

static class WolverineFacts
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
        AdapterIdentity adapter)
    {
        if (project.Compilation.GetTypeByMetadataName(WellKnownTypes.WolverineOptions) is null)
        {
            return new([], []);
        }

        var facts = new List<GenerationFact>();
        var discovery = WolverineHandlerDiscovery.Discover(project);
        var validationAuthorization = WolverineValidationAuthorizationDiscovery.Discover(project);
        var diagnostics = new List<GenerationDiagnostic>(discovery.Diagnostics);
        diagnostics.AddRange(validationAuthorization.Diagnostics);
        var sagaDiscovery = WolverineSagaFacts.Discover(project, adapter, discovery.Policy);
        facts.AddRange(sagaDiscovery.Facts);
        diagnostics.AddRange(sagaDiscovery.Diagnostics);
        var catalog = new DotNetArtifactCatalog(project.Compilation);
        foreach (var type in catalog.Types.Where(_ => IsPublicSourceType(_, project) && !IsIgnored(_) && !WolverineSagaFacts.IsSagaType(_, project)))
        {
            foreach (var method in type.GetMembers().OfType<IMethodSymbol>().Where(_ => IsPublicSourceMethod(_, project) && !IsIgnored(_)))
            {
                var endpoint = EndpointFor(method);
                if (endpoint is not null)
                {
                    AnalyzeEndpoint(project, options, adapter, endpoint, validationAuthorization, facts, diagnostics);
                }
                else if (IsHandler(type, method, discovery.Policy))
                {
                    AnalyzeHandler(project, options, adapter, method, validationAuthorization, facts, diagnostics);
                }
            }
        }

        return new(facts, diagnostics);
    }

    internal static bool IsSagaMessagePayloadType(ITypeSymbol type) => IsEventPayloadType(type);

    internal static void AddSagaReturnConsequences(
        DotNetProjectCompilation project,
        SubjectId sourceSubject,
        IReadOnlyList<WolverineReturnConsequence> consequences,
        Evidence evidence,
        List<GenerationFact> facts) => AddReturnConsequences(project, sourceSubject, consequences, evidence, facts);

    internal static List<WolverineOutgoingMessageConsequence> DiscoverSagaOutgoingMessages(
        IMethodSymbol method,
        DotNetProjectCompilation project) => DiscoverOutgoingMessages(method, project);

    internal static void AddSagaOutgoingMessages(
        DotNetProjectCompilation project,
        SubjectId sourceSubject,
        IMethodSymbol method,
        IReadOnlyList<WolverineOutgoingMessageConsequence> consequences,
        Evidence evidence,
        List<GenerationFact> facts,
        List<GenerationDiagnostic> diagnostics) =>
        AddOutgoingMessages(project, sourceSubject, method, consequences, evidence, facts, diagnostics);

    internal static void AddSagaDirectBusConsequences(
        DotNetProjectCompilation project,
        SubjectId sourceSubject,
        IMethodSymbol method,
        Evidence evidence,
        List<GenerationFact> facts,
        List<GenerationDiagnostic> diagnostics) =>
        AddDirectBusConsequences(project, sourceSubject, method, evidence, facts, diagnostics);

    static void AnalyzeEndpoint(
        DotNetProjectCompilation project,
        DotNetAdapterOptions options,
        AdapterIdentity adapter,
        HttpEndpoint endpoint,
        WolverineValidationAuthorizationDiscoveryResult validationAuthorization,
        List<GenerationFact> facts,
        List<GenerationDiagnostic> diagnostics)
    {
        if (string.Equals(endpoint.Verb, "GET", StringComparison.Ordinal) ||
            string.Equals(endpoint.Verb, "QUERY", StringComparison.Ordinal))
        {
            AnalyzeQuery(project, options, adapter, endpoint, validationAuthorization, facts, diagnostics);
            return;
        }

        var method = endpoint.Method;
        var aggregateWorkflow = IsAggregateWorkflow(method);
        var request = RequestParameter(method, project);
        var commandType = request?.Type as INamedTypeSymbol;
        var dcb = WolverineDcb.Discover(method, request, project, isHttpEndpoint: true);
        var streamBindings = WolverineEventStreams.Bindings(method, commandType, project);
        var appendDiscovery = WolverineEventStreams.Appends(method, project, streamBindings);
        var aggregate = dcb is null ? AggregateParameter(method, request, aggregateWorkflow) : null;
        var commandSubject = commandType is not null
            ? project.SubjectForType(commandType)
            : MethodSubject(project, method, "command");
        var entity = method.Parameters.FirstOrDefault(IsEntityParameter);
        var commandName = request?.Type.Name ?? (method.ContainingType.Name.EndsWith("Endpoints", StringComparison.Ordinal)
            ? $"{method.Name}{entity?.Type.Name}"
            : method.ContainingType.Name.Replace("Endpoint", string.Empty, StringComparison.Ordinal));
        var evidence = MethodEvidence(method, project, adapter, EvidenceStrength.Exact, $"Wolverine HTTP {endpoint.Verb} endpoint");
        var file = evidence.Source?.Path;
        var properties = commandType is not null
            ? CommandProperties(commandType, aggregate?.Type as INamedTypeSymbol, streamBindings)
            : RouteProperties(method);
        var feature = StateFeature(commandName, aggregate?.Type as INamedTypeSymbol, streamBindings, dcb?.ModelType);
        var placement = BehaviorPlacement(project, options, feature, commandName, GenerationSliceKind.StateChange);
        var commandKey = new ArtifactKey { Subject = commandSubject, Kind = ArtifactKind.Command };

        facts.Add(Artifact($"wolverine:command:{commandSubject.Value}", commandKey, commandName, file, properties, evidence));
        facts.Add(Placement($"wolverine:placement:command:{commandSubject.Value}", commandKey, placement, evidence));
        diagnostics.Add(new GenerationDiagnostic
        {
            Code = WolverineDiagnosticCodes.HttpMetadataOmitted,
            Severity = GenerationDiagnosticSeverity.Information,
            Message = $"HTTP {endpoint.Verb} route '{endpoint.Route}' for '{commandName}' is not represented by the current Screenplay language",
            Source = evidence.Source,
            Subject = commandSubject
        });

        if (aggregate?.Type is INamedTypeSymbol aggregateType)
        {
            AddReadModelAndRelationship(project, adapter, commandSubject, commandType, aggregateType, facts, evidence);
            if (commandType is not null && IdentityProperty(commandType, aggregateType) is null)
            {
                diagnostics.Add(new GenerationDiagnostic
                {
                    Code = WolverineDiagnosticCodes.RouteIdentityOmitted,
                    Severity = GenerationDiagnosticSeverity.Warning,
                    Message = $"The '{aggregateType.Name}' identity for '{commandName}' comes from the HTTP route rather than a command property and cannot be marked as a Screenplay identifier",
                    Source = evidence.Source,
                    Subject = commandSubject
                });
            }
            if (commandType?.GetMembers().OfType<IPropertySymbol>().Any(_ => _.Name == "Version") == true)
            {
                diagnostics.Add(new GenerationDiagnostic
                {
                    Code = WolverineDiagnosticCodes.StreamVersionOmitted,
                    Severity = GenerationDiagnosticSeverity.Information,
                    Message = $"The expected stream version on '{commandName}' cannot be represented exactly by Screenplay concurrency",
                    Source = evidence.Source,
                    Subject = commandSubject
                });
            }
        }

        AddEventStreamBindingFacts(
            project,
            adapter,
            commandSubject,
            commandName,
            streamBindings,
            isHttpEndpoint: true,
            facts,
            diagnostics);
        AddDcbFacts(project, adapter, commandSubject, commandName, dcb, facts, diagnostics);

        if (commandType is not null)
        {
            diagnostics.AddRange(validationAuthorization.ValidationDiagnostics(
                method,
                commandType,
                commandSubject,
                isHttpEndpoint: true));
        }
        diagnostics.AddRange(validationAuthorization.AuthorizationDiagnostics(method, commandSubject));

        var hasCompoundValidation = validationAuthorization.HasCompoundValidation(method);
        IReadOnlyList<ITypeSymbol> eventTypes = [];
        if (dcb is not null)
        {
            eventTypes = dcb.EventTypes;
        }
        else if (aggregateWorkflow && streamBindings.Count == 0)
        {
            eventTypes = [.. AggregateReturnEvents(method, project)];
        }
        var bodyEvents = dcb is null ? PersistenceEvents(method, project).ToArray() : [];
        foreach (var eventType in eventTypes.Concat(bodyEvents).Distinct(SymbolEqualityComparer.Default).OfType<INamedTypeSymbol>())
        {
            var isImperativeDcbEvent = dcb?.ImperativeEventTypes.Any(_ => SymbolEqualityComparer.Default.Equals(_, eventType)) ?? false;
            var declarative = eventTypes.Any(_ => SymbolEqualityComparer.Default.Equals(_, eventType)) &&
                              !bodyEvents.Any(_ => SymbolEqualityComparer.Default.Equals(_, eventType)) &&
                              !isImperativeDcbEvent &&
                              !hasCompoundValidation;
            AddEventAndProduction(project, commandSubject, eventType, placement, evidence, declarative, facts);
        }
        AddEventStreamAppendFacts(project, adapter, commandSubject, placement, appendDiscovery, facts, diagnostics);

        var returnConsequences = WolverineReturnConsequences.Classify(
            method,
            project,
            isHttpEndpoint: true,
            aggregateWorkflow || dcb is { IsBoundaryParameter: false },
            dcb is null && streamBindings.Count > 0);
        var outgoingMessages = DiscoverOutgoingMessages(method, project);
        AddDocumentDeletes(project, commandSubject, method, evidence, facts);
        AddReturnConsequences(project, commandSubject, returnConsequences, evidence, facts);
        AddDirectBusConsequences(project, commandSubject, method, evidence, facts, diagnostics);
        AddOutgoingMessages(project, commandSubject, method, outgoingMessages, evidence, facts, diagnostics);
    }

    static void AnalyzeHandler(
        DotNetProjectCompilation project,
        DotNetAdapterOptions options,
        AdapterIdentity adapter,
        IMethodSymbol method,
        WolverineValidationAuthorizationDiscoveryResult validationAuthorization,
        List<GenerationFact> facts,
        List<GenerationDiagnostic> diagnostics)
    {
        var request = RequestParameter(method, project);
        if (request?.Type is not INamedTypeSymbol requestType)
        {
            return;
        }

        var aggregateWorkflow = IsAggregateWorkflow(method);
        var dcb = WolverineDcb.Discover(method, request, project, isHttpEndpoint: false);
        var streamBindings = WolverineEventStreams.Bindings(method, requestType, project);
        var appendDiscovery = WolverineEventStreams.Appends(method, project, streamBindings);
        var aggregate = dcb is null ? AggregateParameter(method, request, aggregateWorkflow) : null;
        var bodyEvents = dcb is null ? PersistenceEvents(method, project).ToArray() : [];
        IReadOnlyList<ITypeSymbol> returnEvents = [];
        if (dcb is not null)
        {
            returnEvents = dcb.EventTypes;
        }
        else if (aggregateWorkflow && streamBindings.Count == 0)
        {
            returnEvents = [.. AggregateReturnEvents(method, project)];
        }
        var deletedDocuments = DocumentDeletes(method, project).ToArray();
        var hasDocumentPersistence = HasDocumentPersistence(method, project);
        var busConsequences = WolverineBusConsequences.Discover(method, project);
        var returnConsequences = WolverineReturnConsequences.Classify(
            method,
            project,
            isHttpEndpoint: false,
            aggregateWorkflow || dcb is { IsBoundaryParameter: false },
            dcb is null && streamBindings.Count > 0);
        var outgoingMessages = DiscoverOutgoingMessages(method, project);
        if (bodyEvents.Length == 0 &&
            returnEvents.Count == 0 &&
            deletedDocuments.Length == 0 &&
            !appendDiscovery.HasDirectWrite &&
            !streamBindings.Any(_ => _.LoadsModel) &&
            dcb is null)
        {
            var automationReturns = hasDocumentPersistence ? [] : returnConsequences;
            var automationOutgoingMessages = hasDocumentPersistence ? [] : outgoingMessages;
            if (busConsequences.Count > 0 ||
                automationReturns.Any(IsCascadeConsequence) ||
                automationOutgoingMessages.Count > 0)
            {
                AnalyzeAutomation(
                    project,
                    options,
                    adapter,
                    method,
                    requestType,
                    automationReturns,
                    automationOutgoingMessages,
                    busConsequences,
                    validationAuthorization,
                    facts,
                    diagnostics);
            }

            return;
        }

        var commandSubject = project.SubjectForType(requestType);
        diagnostics.AddRange(validationAuthorization.ValidationDiagnostics(
            method,
            requestType,
            commandSubject,
            isHttpEndpoint: false));
        var evidence = MethodEvidence(method, project, adapter, EvidenceStrength.Exact, "Wolverine message handler with persistence effects");
        var feature = StateFeature(requestType.Name, aggregate?.Type as INamedTypeSymbol, streamBindings, dcb?.ModelType);
        var placement = BehaviorPlacement(project, options, feature, requestType.Name, GenerationSliceKind.StateChange);
        var key = new ArtifactKey { Subject = commandSubject, Kind = ArtifactKind.Command };
        facts.Add(Artifact(
            $"wolverine:command:{commandSubject.Value}",
            key,
            requestType.Name,
            evidence.Source?.Path,
            CommandProperties(requestType, aggregate?.Type as INamedTypeSymbol, streamBindings),
            evidence));
        facts.Add(Placement($"wolverine:placement:command:{commandSubject.Value}", key, placement, evidence));

        if (aggregate?.Type is INamedTypeSymbol aggregateType)
        {
            AddReadModelAndRelationship(project, adapter, commandSubject, requestType, aggregateType, facts, evidence);
        }
        AddEventStreamBindingFacts(
            project,
            adapter,
            commandSubject,
            requestType.Name,
            streamBindings,
            isHttpEndpoint: false,
            facts,
            diagnostics);
        AddDcbFacts(project, adapter, commandSubject, requestType.Name, dcb, facts, diagnostics);

        foreach (var eventType in returnEvents.Concat(bodyEvents).Distinct(SymbolEqualityComparer.Default).OfType<INamedTypeSymbol>())
        {
            var isImperativeDcbEvent = dcb?.ImperativeEventTypes.Any(_ => SymbolEqualityComparer.Default.Equals(_, eventType)) ?? false;
            var declarative = returnEvents.Any(_ => SymbolEqualityComparer.Default.Equals(_, eventType)) &&
                              !bodyEvents.Any(_ => SymbolEqualityComparer.Default.Equals(_, eventType)) &&
                              !isImperativeDcbEvent;
            AddEventAndProduction(project, commandSubject, eventType, placement, evidence, declarative, facts);
        }
        AddEventStreamAppendFacts(project, adapter, commandSubject, placement, appendDiscovery, facts, diagnostics);

        AddDocumentDeletes(project, commandSubject, method, evidence, facts);
        AddReturnConsequences(project, commandSubject, returnConsequences, evidence, facts);
        AddDirectBusConsequences(project, commandSubject, method, evidence, facts, diagnostics, busConsequences);
        AddOutgoingMessages(project, commandSubject, method, outgoingMessages, evidence, facts, diagnostics);
    }

    static void AnalyzeAutomation(
        DotNetProjectCompilation project,
        DotNetAdapterOptions options,
        AdapterIdentity adapter,
        IMethodSymbol method,
        INamedTypeSymbol requestType,
        IReadOnlyList<WolverineReturnConsequence> returnConsequences,
        IReadOnlyList<WolverineOutgoingMessageConsequence> outgoingMessages,
        IReadOnlyList<WolverineBusConsequence> busConsequences,
        WolverineValidationAuthorizationDiscoveryResult validationAuthorization,
        List<GenerationFact> facts,
        List<GenerationDiagnostic> diagnostics)
    {
        var requestSubject = project.SubjectForType(requestType);
        var reactionSubject = MethodSubject(project, method, "reaction");
        diagnostics.AddRange(validationAuthorization.ValidationDiagnostics(
            method,
            requestType,
            reactionSubject,
            isHttpEndpoint: false));
        var evidence = MethodEvidence(method, project, adapter, EvidenceStrength.Exact, "Wolverine message handler with direct bus or return automation consequences");
        var reactionName = method.ContainingType.Name.EndsWith("Handler", StringComparison.Ordinal)
            ? method.ContainingType.Name[..^"Handler".Length]
            : method.ContainingType.Name;
        var placement = BehaviorPlacement(project, options, requestType.Name, reactionName, GenerationSliceKind.Automation);
        var requestKey = new ArtifactKey { Subject = requestSubject, Kind = ArtifactKind.Message };
        var reactionKey = new ArtifactKey { Subject = reactionSubject, Kind = ArtifactKind.Reaction };
        facts.Add(Artifact(
            $"wolverine:message:{requestSubject.Value}",
            requestKey,
            requestType.Name,
            SourceFileOf(requestType, project),
            DotNetTypeShapes.PropertiesOf(requestType),
            evidence));
        facts.Add(Artifact(
            $"wolverine:reaction:{reactionSubject.Value}",
            reactionKey,
            reactionName,
            evidence.Source?.Path,
            [],
            evidence));
        facts.Add(Placement($"wolverine:placement:reaction:{reactionSubject.Value}", reactionKey, placement, evidence));
        facts.Add(Relationship(
            $"wolverine:handles:{reactionSubject.Value}:{requestSubject.Value}",
            reactionSubject,
            RelationshipKind.Handles,
            requestSubject,
            evidence));
        AddReturnConsequences(project, reactionSubject, returnConsequences, evidence, facts);
        AddOutgoingMessages(project, reactionSubject, method, outgoingMessages, evidence, facts, diagnostics);
        AddDirectBusConsequences(project, reactionSubject, method, evidence, facts, diagnostics, busConsequences);
    }

    static void AnalyzeQuery(
        DotNetProjectCompilation project,
        DotNetAdapterOptions options,
        AdapterIdentity adapter,
        HttpEndpoint endpoint,
        WolverineValidationAuthorizationDiscoveryResult validationAuthorization,
        List<GenerationFact> facts,
        List<GenerationDiagnostic> diagnostics)
    {
        var querySubject = MethodSubject(project, endpoint.Method, "query");
        if (RequestParameter(endpoint.Method, project)?.Type is INamedTypeSymbol validationRequestType)
        {
            diagnostics.AddRange(validationAuthorization.ValidationDiagnostics(
                endpoint.Method,
                validationRequestType,
                querySubject,
                isHttpEndpoint: true));
        }
        diagnostics.AddRange(validationAuthorization.AuthorizationDiagnostics(endpoint.Method, querySubject));

        var (model, isCollection, isOptional) = WolverineReturnTypes.QueryModel(endpoint.Method.ReturnType);
        if (model is null || !IsSourceType(model))
        {
            return;
        }

        var evidence = MethodEvidence(endpoint.Method, project, adapter, EvidenceStrength.Exact, $"Wolverine HTTP {endpoint.Verb} endpoint");
        var compiledQueryDiscovery = MartenCompiledQueryDiscovery.Discover(endpoint.Method, querySubject, project, adapter);
        var compiledQueries = compiledQueryDiscovery.Links;
        diagnostics.AddRange(compiledQueryDiscovery.Diagnostics);
        var queryName = endpoint.Method.ContainingType.Name.EndsWith("Endpoints", StringComparison.Ordinal)
            ? endpoint.Method.Name
            : endpoint.Method.ContainingType.Name.Replace("Endpoint", string.Empty, StringComparison.Ordinal);
        var placement = BehaviorPlacement(project, options, model.Name, queryName, GenerationSliceKind.StateView);
        var queryKey = new ArtifactKey { Subject = querySubject, Kind = ArtifactKind.Query };
        var modelSubject = project.SubjectForType(model);
        var modelKey = new ArtifactKey { Subject = modelSubject, Kind = ArtifactKind.ReadModel };

        facts.Add(Artifact(
            $"wolverine:query:{querySubject.Value}",
            queryKey,
            queryName,
            evidence.Source?.Path,
            QueryProperties(endpoint.Method, compiledQueries.SelectMany(_ => _.Parameters)),
            evidence));
        facts.Add(Placement($"wolverine:placement:query:{querySubject.Value}", queryKey, placement, evidence));
        diagnostics.Add(new GenerationDiagnostic
        {
            Code = WolverineDiagnosticCodes.HttpMetadataOmitted,
            Severity = GenerationDiagnosticSeverity.Information,
            Message = $"HTTP {endpoint.Verb} route '{endpoint.Route}' for query '{queryName}' is not represented by the current Screenplay language",
            Source = evidence.Source,
            Subject = querySubject
        });
        facts.Add(Artifact(
            $"wolverine:read-model:{modelSubject.Value}",
            modelKey,
            model.Name,
            SourceFileOf(model, project),
            DotNetTypeShapes.PropertiesOf(model),
            evidence));
        facts.Add(Placement(
            $"wolverine:placement:read-model:{modelSubject.Value}",
            modelKey,
            BehaviorPlacement(project, options, model.Name, model.Name, GenerationSliceKind.StateView),
            evidence with
            {
                Strength = EvidenceStrength.Heuristic,
                Explanation = "The returned model name provides the default Screenplay placement"
            }));
        facts.Add(Relationship(
            $"wolverine:returns:{querySubject.Value}:{modelSubject.Value}",
            querySubject,
            RelationshipKind.Returns,
            modelSubject,
            evidence,
            isCollection: isCollection,
            isOptional: isOptional));

        foreach (var compiledQuery in compiledQueries
                     .GroupBy(_ => project.SubjectForType(_.DocumentType))
                     .Select(_ => _.First()))
        {
            var documentSubject = project.SubjectForType(compiledQuery.DocumentType);
            facts.Add(Relationship(
                $"wolverine:reads:compiled:{querySubject.Value}:{documentSubject.Value}",
                querySubject,
                RelationshipKind.Reads,
                documentSubject,
                compiledQuery.Evidence));
        }
    }

    static void AddReadModelAndRelationship(
        DotNetProjectCompilation project,
        AdapterIdentity adapter,
        SubjectId commandSubject,
        INamedTypeSymbol? commandType,
        INamedTypeSymbol aggregateType,
        List<GenerationFact> facts,
        Evidence evidence)
    {
        var aggregateSubject = project.SubjectForType(aggregateType);
        facts.Add(Artifact(
            $"wolverine:read-model:{aggregateSubject.Value}",
            new ArtifactKey { Subject = aggregateSubject, Kind = ArtifactKind.ReadModel },
            aggregateType.Name,
            SourceFileOf(aggregateType, project),
            DotNetTypeShapes.PropertiesOf(aggregateType),
            CritterStackSource.EvidenceFor(aggregateType, adapter, project, EvidenceStrength.Conventional, "Wolverine loads this model as aggregate decision state")));
        facts.Add(Relationship(
            $"wolverine:reads:{commandSubject.Value}:{aggregateSubject.Value}",
            commandSubject,
            RelationshipKind.Reads,
            aggregateSubject,
            evidence,
            sourceMember: commandType is null ? null : IdentityPropertyName(commandType, aggregateType)));
    }

    static void AddDcbFacts(
        DotNetProjectCompilation project,
        AdapterIdentity adapter,
        SubjectId commandSubject,
        string commandName,
        WolverineDcbDiscovery? dcb,
        List<GenerationFact> facts,
        List<GenerationDiagnostic> diagnostics)
    {
        if (dcb is null)
        {
            return;
        }

        var aggregateSubject = project.SubjectForType(dcb.ModelType);
        var evidence = new Evidence
        {
            Adapter = adapter,
            Strength = EvidenceStrength.Exact,
            Source = dcb.Source,
            Explanation = $"Authored Wolverine DCB model parameter '{dcb.Parameter.Name}' with an EventTagQuery companion"
        };
        AddAggregateArtifact(project, aggregateSubject, dcb.ModelType, evidence, facts);
        facts.Add(Relationship(
            $"wolverine:reads:{commandSubject.Value}:{dcb.Discriminator}:{aggregateSubject.Value}",
            commandSubject,
            RelationshipKind.Reads,
            aggregateSubject,
            evidence,
            sourceMember: dcb.SourceMember,
            discriminator: dcb.Discriminator));

        foreach (var condition in dcb.Conditions)
        {
            var tagSubject = project.SubjectForType(condition.TagType);
            var eventSubject = condition.EventType is null ? null : project.SubjectForType(condition.EventType);
            var discriminator = $"{dcb.Discriminator}:condition:{condition.Ordinal}:tag:{tagSubject.Value}:event:{eventSubject?.Value ?? "any"}";
            var conditionEvidence = evidence with
            {
                Source = condition.Source ?? dcb.QuerySource,
                Explanation = condition.EventType is null
                    ? $"Exact DCB condition {condition.Ordinal} matches any event tagged with '{condition.TagType.Name}'"
                    : $"Exact DCB condition {condition.Ordinal} matches '{condition.EventType.Name}' tagged with '{condition.TagType.Name}'"
            };
            facts.Add(Relationship(
                $"wolverine:reads:{commandSubject.Value}:{discriminator}:{aggregateSubject.Value}",
                commandSubject,
                RelationshipKind.Reads,
                aggregateSubject,
                conditionEvidence,
                sourceMember: condition.SourceMember,
                discriminator: discriminator));
        }

        foreach (var eventType in dcb.QueryEventTypes)
        {
            var eventSubject = project.SubjectForType(eventType);
            var eventEvidence = evidence with
            {
                Source = dcb.QuerySource,
                Explanation = $"Explicit historical event type in the authored DCB EventTagQuery for '{commandName}'"
            };
            facts.Add(Artifact(
                $"wolverine:dcb:query-event:{commandSubject.Value}:{eventSubject.Value}",
                new ArtifactKey { Subject = eventSubject, Kind = ArtifactKind.Event },
                eventType.Name,
                SourceFileOf(eventType, project),
                DotNetTypeShapes.PropertiesOf(eventType),
                eventEvidence));
        }

        diagnostics.Add(new GenerationDiagnostic
        {
            Code = WolverineDiagnosticCodes.DcbBoundaryOmitted,
            Severity = GenerationDiagnosticSeverity.Information,
            Message = $"The DCB consistency boundary for '{commandName}' parameter '{dcb.Parameter.Name}' is retained as neutral Aggregate/Reads evidence, but tag routing and boundary concurrency cannot be represented by the current Screenplay language",
            Source = dcb.Source,
            Subject = commandSubject
        });

        if (!dcb.QueryResolved)
        {
            diagnostics.Add(new GenerationDiagnostic
            {
                Code = WolverineDiagnosticCodes.DcbQueryUnresolved,
                Severity = GenerationDiagnosticSeverity.Warning,
                Message = $"The EventTagQuery companion '{dcb.Companion.Name}' for '{commandName}' is outside the bounded direct fluent-chain shapes and was not interpreted",
                Source = dcb.QuerySource,
                Subject = commandSubject
            });
        }
    }

    static void AddEventStreamBindingFacts(
        DotNetProjectCompilation project,
        AdapterIdentity adapter,
        SubjectId commandSubject,
        string commandName,
        IReadOnlyList<WolverineStateBinding> bindings,
        bool isHttpEndpoint,
        List<GenerationFact> facts,
        List<GenerationDiagnostic> diagnostics)
    {
        var aggregateSubjects = new HashSet<SubjectId>();
        foreach (var binding in bindings.Where(_ => _.LoadsModel))
        {
            var evidence = StateBindingEvidence(
                adapter,
                binding,
                $"Wolverine loads '{binding.ModelType.Name}' through exact IEventStream<T> parameter '{binding.Parameter.Name}'");
            var aggregateSubject = project.SubjectForType(binding.ModelType);
            if (aggregateSubjects.Add(aggregateSubject))
            {
                AddAggregateArtifact(project, aggregateSubject, binding.ModelType, evidence, facts);
            }

            facts.Add(Relationship(
                $"wolverine:reads:{commandSubject.Value}:{binding.Discriminator}:{aggregateSubject.Value}",
                commandSubject,
                RelationshipKind.Reads,
                aggregateSubject,
                evidence,
                sourceMember: binding.IdentityMember is null ? null : LowerFirst(binding.IdentityMember.Name),
                discriminator: binding.Discriminator));

            if (isHttpEndpoint && binding.IdentityMember is null)
            {
                diagnostics.Add(new GenerationDiagnostic
                {
                    Code = WolverineDiagnosticCodes.RouteIdentityOmitted,
                    Severity = GenerationDiagnosticSeverity.Warning,
                    Message = $"The '{binding.ModelType.Name}' identity for '{commandName}' stream parameter '{binding.Parameter.Name}' comes from HTTP route or binding metadata rather than a command property and cannot be marked as a Screenplay identifier",
                    Source = binding.Identity.Source ?? binding.Source,
                    Subject = commandSubject
                });
            }

            if (binding.Version.Value is not null ||
                binding.HasAmbiguousConventionalVersion ||
                !string.Equals(binding.LoadStyle.Value, "Optimistic", StringComparison.Ordinal) ||
                binding.Consistency.Value)
            {
                diagnostics.Add(new GenerationDiagnostic
                {
                    Code = WolverineDiagnosticCodes.StreamVersionOmitted,
                    Severity = GenerationDiagnosticSeverity.Information,
                    Message = binding.HasAmbiguousConventionalVersion
                        ? $"The conventional Version member for '{commandName}' cannot be attributed safely to stream parameter '{binding.Parameter.Name}' because the handler loads multiple streams"
                        : $"The version, load style, or consistency metadata for '{commandName}' stream parameter '{binding.Parameter.Name}' cannot be represented exactly by Screenplay concurrency",
                    Source = binding.Version.Source ?? binding.LoadStyle.Source ?? binding.Consistency.Source ?? binding.Source,
                    Subject = commandSubject
                });
            }

            if (bindings.Count > 1)
            {
                diagnostics.Add(new GenerationDiagnostic
                {
                    Code = WolverineDiagnosticCodes.MultipleStreamMetadataOmitted,
                    Severity = GenerationDiagnosticSeverity.Warning,
                    Message = $"Handler '{commandName}' binds multiple event streams; target and identity for parameter '{binding.Parameter.Name}' are retained as neutral relationship metadata, but parameter-specific loading metadata cannot be lowered faithfully to the current Screenplay language",
                    Source = binding.Source,
                    Subject = commandSubject
                });
            }
        }
    }

    static void AddEventStreamAppendFacts(
        DotNetProjectCompilation project,
        AdapterIdentity adapter,
        SubjectId commandSubject,
        ArtifactPlacement placement,
        WolverineEventStreamAppendDiscovery discovery,
        List<GenerationFact> facts,
        List<GenerationDiagnostic> diagnostics)
    {
        foreach (var unresolved in discovery.Unresolved)
        {
            diagnostics.Add(new GenerationDiagnostic
            {
                Code = WolverineDiagnosticCodes.EventWriteTargetUnresolved,
                Severity = GenerationDiagnosticSeverity.Warning,
                Message = $"An exact IEventStream<T> append was not represented because {unresolved.Reason}",
                Source = unresolved.Source,
                Subject = commandSubject
            });
        }

        var aggregateSubjects = new HashSet<SubjectId>();
        var producedEvents = new HashSet<SubjectId>();
        var appendRelationships = new HashSet<string>(StringComparer.Ordinal);
        foreach (var append in discovery.Appends)
        {
            var binding = append.Binding;
            var aggregateSubject = project.SubjectForType(binding.ModelType);
            var evidence = new Evidence
            {
                Adapter = adapter,
                Strength = EvidenceStrength.Exact,
                Source = append.Source,
                Explanation = $"Exact IEventStream<{binding.ModelType.Name}> append through handler parameter '{binding.Parameter.Name}'"
            };
            if (aggregateSubjects.Add(aggregateSubject))
            {
                AddAggregateArtifact(project, aggregateSubject, binding.ModelType, evidence, facts);
            }

            foreach (var eventType in append.EventTypes)
            {
                var eventSubject = project.SubjectForType(eventType);
                if (producedEvents.Add(eventSubject))
                {
                    AddEventAndProduction(project, commandSubject, eventType, placement, evidence, declarative: false, facts);
                }

                var relationshipDiscriminator = $"{binding.Discriminator}:event:{eventSubject.Value}";
                if (!appendRelationships.Add(relationshipDiscriminator))
                {
                    continue;
                }

                facts.Add(Relationship(
                    $"wolverine:appends:{commandSubject.Value}:{relationshipDiscriminator}:{aggregateSubject.Value}",
                    commandSubject,
                    RelationshipKind.Appends,
                    aggregateSubject,
                    evidence,
                    sourceMember: binding.IdentityMember is null ? null : LowerFirst(binding.IdentityMember.Name),
                    discriminator: relationshipDiscriminator));
            }
        }
    }

    static void AddAggregateArtifact(
        DotNetProjectCompilation project,
        SubjectId aggregateSubject,
        INamedTypeSymbol aggregateType,
        Evidence evidence,
        List<GenerationFact> facts) => facts.Add(Artifact(
        $"wolverine:aggregate:{aggregateSubject.Value}",
        new ArtifactKey { Subject = aggregateSubject, Kind = ArtifactKind.Aggregate },
        aggregateType.Name,
        SourceFileOf(aggregateType, project),
        DotNetTypeShapes.PropertiesOf(aggregateType),
        evidence));

    static Evidence StateBindingEvidence(
        AdapterIdentity adapter,
        WolverineStateBinding binding,
        string explanation) => new()
        {
            Adapter = adapter,
            Strength = EvidenceStrength.Exact,
            Source = binding.Source,
            Explanation = explanation
        };

    static void AddEventAndProduction(
        DotNetProjectCompilation project,
        SubjectId commandSubject,
        INamedTypeSymbol eventType,
        ArtifactPlacement placement,
        Evidence evidence,
        bool declarative,
        List<GenerationFact> facts)
    {
        var eventSubject = project.SubjectForType(eventType);
        var eventKey = new ArtifactKey { Subject = eventSubject, Kind = ArtifactKind.Event };
        facts.Add(Artifact(
            $"wolverine:event:{eventSubject.Value}",
            eventKey,
            eventType.Name,
            SourceFileOf(eventType, project),
            DotNetTypeShapes.PropertiesOf(eventType),
            evidence));
        facts.Add(Placement($"wolverine:placement:event:{eventSubject.Value}:{commandSubject.Value}", eventKey, placement, evidence));
        facts.Add(Relationship(
            $"wolverine:produces:{commandSubject.Value}:{eventSubject.Value}",
            commandSubject,
            RelationshipKind.Produces,
            eventSubject,
            evidence,
            discriminator: declarative ? "declarative" : "imperative"));
    }

    static void AddDirectBusConsequences(
        DotNetProjectCompilation project,
        SubjectId sourceSubject,
        IMethodSymbol method,
        Evidence evidence,
        List<GenerationFact> facts,
        List<GenerationDiagnostic> diagnostics,
        IReadOnlyList<WolverineBusConsequence>? discovered = null)
    {
        foreach (var consequence in discovered ?? WolverineBusConsequences.Discover(method, project))
        {
            if (consequence.MessageType is not INamedTypeSymbol messageType || !IsEventPayloadType(messageType))
            {
                continue;
            }

            var messageSubject = project.SubjectForType(messageType);
            AddMessageRelationship(
                project,
                sourceSubject,
                messageType,
                evidence,
                RelationshipKind.Publishes,
                $"wolverine:publishes:{consequence.Discriminator}:{sourceSubject.Value}:{messageSubject.Value}",
                consequence.Discriminator,
                facts);
            diagnostics.Add(new GenerationDiagnostic
            {
                Code = consequence.IsScheduled
                    ? WolverineDiagnosticCodes.DelayedMessageOmitted
                    : WolverineDiagnosticCodes.DirectMessageDeliveryOmitted,
                Severity = GenerationDiagnosticSeverity.Warning,
                Message = consequence.IsScheduled
                    ? $"Handler '{method.ContainingType.Name}.{method.Name}' schedules '{messageType.Name}', which the current Screenplay language cannot represent"
                    : $"Handler '{method.ContainingType.Name}.{method.Name}' performs Wolverine {consequence.Discriminator} delivery of '{messageType.Name}', which the current Screenplay language cannot represent",
                Source = evidence.Source,
                Subject = sourceSubject
            });
        }
    }

    static List<WolverineOutgoingMessageConsequence> DiscoverOutgoingMessages(
        IMethodSymbol method,
        DotNetProjectCompilation project)
    {
        var consequences = new List<WolverineOutgoingMessageConsequence>();
        foreach (var (declaration, semanticModel) in WolverineMethodSyntax.Declarations(method, project))
        {
            foreach (var collection in declaration.DescendantNodes().OfType<CollectionExpressionSyntax>())
            {
                if (semanticModel.GetTypeInfo(collection).ConvertedType is not INamedTypeSymbol collectionType ||
                    DotNetSubjectIds.MetadataName(collectionType.OriginalDefinition) != WellKnownTypes.WolverineOutgoingMessages)
                {
                    continue;
                }

                foreach (var element in collection.Elements.OfType<ExpressionElementSyntax>())
                {
                    if (semanticModel.GetTypeInfo(element.Expression).Type is not INamedTypeSymbol elementType)
                    {
                        continue;
                    }

                    var messageType = elementType.Name == "DeliveryMessage" && elementType.TypeArguments.FirstOrDefault() is INamedTypeSymbol delivered
                        ? delivered
                        : elementType;
                    if (!IsEventPayloadType(messageType))
                    {
                        continue;
                    }

                    var delayed = element.Expression.DescendantNodesAndSelf()
                        .OfType<InvocationExpressionSyntax>()
                        .Any(_ => _.Expression.ToString().Contains("Delayed", StringComparison.Ordinal));
                    consequences.Add(new(messageType, delayed));
                }
            }
        }

        return consequences;
    }

    static void AddOutgoingMessages(
        DotNetProjectCompilation project,
        SubjectId sourceSubject,
        IMethodSymbol method,
        IReadOnlyList<WolverineOutgoingMessageConsequence> consequences,
        Evidence evidence,
        List<GenerationFact> facts,
        List<GenerationDiagnostic> diagnostics)
    {
        foreach (var consequence in consequences)
        {
            var messageSubject = project.SubjectForType(consequence.MessageType);
            AddMessageRelationship(
                project,
                sourceSubject,
                consequence.MessageType,
                evidence,
                RelationshipKind.Cascades,
                $"wolverine:cascades:{sourceSubject.Value}:{messageSubject.Value}",
                consequence.Delayed ? "delayed" : "immediate",
                facts);

            if (consequence.Delayed)
            {
                diagnostics.Add(new GenerationDiagnostic
                {
                    Code = WolverineDiagnosticCodes.DelayedMessageOmitted,
                    Severity = GenerationDiagnosticSeverity.Warning,
                    Message = $"Handler '{method.ContainingType.Name}.{method.Name}' dispatches '{consequence.MessageType.Name}' after a delay, which the current Screenplay language cannot represent",
                    Source = evidence.Source,
                    Subject = sourceSubject
                });
            }
        }
    }

    static void AddReturnConsequences(
        DotNetProjectCompilation project,
        SubjectId sourceSubject,
        IReadOnlyList<WolverineReturnConsequence> consequences,
        Evidence evidence,
        List<GenerationFact> facts)
    {
        foreach (var consequence in consequences.Where(IsCascadeConsequence))
        {
            var messageType = (INamedTypeSymbol)consequence.Type;
            var messageSubject = project.SubjectForType(messageType);
            AddMessageRelationship(
                project,
                sourceSubject,
                messageType,
                evidence,
                RelationshipKind.Cascades,
                $"wolverine:cascades:return:{sourceSubject.Value}:{consequence.Slot}:{messageSubject.Value}",
                $"return-slot:{consequence.Slot}",
                facts);
        }
    }

    static bool IsCascadeConsequence(WolverineReturnConsequence consequence) =>
        consequence.Kind == WolverineReturnConsequenceKind.Cascade &&
        consequence.Type is INamedTypeSymbol messageType &&
        IsEventPayloadType(messageType);

    static void AddMessageRelationship(
        DotNetProjectCompilation project,
        SubjectId sourceSubject,
        INamedTypeSymbol messageType,
        Evidence evidence,
        RelationshipKind relationshipKind,
        string relationshipId,
        string discriminator,
        List<GenerationFact> facts)
    {
        var messageSubject = project.SubjectForType(messageType);
        facts.Add(Artifact(
            $"wolverine:message:{messageSubject.Value}",
            new ArtifactKey { Subject = messageSubject, Kind = ArtifactKind.Message },
            messageType.Name,
            SourceFileOf(messageType, project),
            DotNetTypeShapes.PropertiesOf(messageType),
            evidence));
        facts.Add(Relationship(
            relationshipId,
            sourceSubject,
            relationshipKind,
            messageSubject,
            evidence,
            discriminator: discriminator));
    }

    static void AddDocumentDeletes(
        DotNetProjectCompilation project,
        SubjectId commandSubject,
        IMethodSymbol method,
        Evidence evidence,
        List<GenerationFact> facts)
    {
        foreach (var documentType in DocumentDeletes(method, project))
        {
            var documentSubject = project.SubjectForType(documentType);
            facts.Add(Relationship(
                $"wolverine:deletes:{commandSubject.Value}:{documentSubject.Value}",
                commandSubject,
                RelationshipKind.Deletes,
                documentSubject,
                evidence));
        }
    }

    static bool HasDocumentPersistence(IMethodSymbol method, DotNetProjectCompilation project) =>
        WolverineMethodSyntax.Declarations(method, project).Any(declaration =>
            declaration.Declaration.DescendantNodes()
                .OfType<InvocationExpressionSyntax>()
                .Select(invocation => declaration.SemanticModel.GetSymbolInfo(invocation).Symbol as IMethodSymbol)
                .Any(invoked => invoked is not null &&
                                _documentPersistenceMethods.Contains(invoked.Name) &&
                                IsPersistenceNamespace(invoked)));

    static IEnumerable<INamedTypeSymbol> DocumentDeletes(IMethodSymbol method, DotNetProjectCompilation project)
    {
        foreach (var (declaration, semanticModel) in WolverineMethodSyntax.Declarations(method, project))
        {
            foreach (var invocation in declaration.DescendantNodes().OfType<InvocationExpressionSyntax>())
            {
                if (semanticModel.GetSymbolInfo(invocation).Symbol is IMethodSymbol invoked &&
                    invoked.Name == "Delete" &&
                    IsPersistenceNamespace(invoked) &&
                    invoked.TypeArguments.FirstOrDefault() is INamedTypeSymbol documentType)
                {
                    yield return documentType;
                }
            }
        }
    }

    static IReadOnlyList<PropertyDefinition> CommandProperties(
        INamedTypeSymbol commandType,
        INamedTypeSymbol? aggregateType,
        IReadOnlyList<WolverineStateBinding> streamBindings)
    {
        var identity = streamBindings.Count switch
        {
            0 => aggregateType is null ? IdentityProperty(commandType, null) : IdentityProperty(commandType, aggregateType),
            1 when streamBindings[0].LoadsModel => streamBindings[0].IdentityMember as IPropertySymbol,
            _ => null
        };
        return
        [
            .. DotNetTypeShapes.PropertiesOf(commandType)
                .Select(_ => _ with
                {
                    IsIdentifier = identity is not null && string.Equals(_.Name, LowerFirst(identity.Name), StringComparison.Ordinal)
                })
        ];
    }

    static IReadOnlyList<PropertyDefinition> QueryProperties(
        IMethodSymbol method,
        IEnumerable<PropertyDefinition>? compiledParameters = null)
    {
        var endpointParameters = method.Parameters
            .Where(_ => !IsInfrastructureParameter(_.Type) && !IsSourceType(_.Type))
            .Select((parameter, index) => new PropertyDefinition
            {
                Name = LowerFirst(parameter.Name),
                Type = DotNetTypeShapes.TypeReferenceFor(parameter.Type),
                IsIdentifier = index == 0
            });
        return
        [
            .. endpointParameters
                .Concat(compiledParameters ?? [])
                .GroupBy(_ => _.Name, StringComparer.Ordinal)
                .Select(_ => _.First())
        ];
    }

    static IReadOnlyList<PropertyDefinition> RouteProperties(IMethodSymbol method) => QueryProperties(method);

    static IParameterSymbol? RequestParameter(IMethodSymbol method, DotNetProjectCompilation project) => method.Parameters.FirstOrDefault(_ =>
        IsSourceType(_.Type) &&
        !IsAggregateParameter(_) &&
        !IsEntityParameter(_) &&
        !WolverineEventStreams.IsEventStream(_.Type) &&
        !WolverineDcb.HasAttributedParameter(_, project));

    static IParameterSymbol? AggregateParameter(
        IMethodSymbol method,
        IParameterSymbol? request,
        bool aggregateWorkflow)
    {
        var attributed = method.Parameters.FirstOrDefault(_ =>
            IsAggregateParameter(_) && !WolverineEventStreams.IsEventStream(_.Type));
        return attributed ?? (aggregateWorkflow
            ? method.Parameters.FirstOrDefault(_ =>
                !SymbolEqualityComparer.Default.Equals(_, request) &&
                IsSourceType(_.Type) &&
                !WolverineEventStreams.IsEventStream(_.Type))
            : null);
    }

    static bool IsEntityParameter(IParameterSymbol parameter) =>
        DotNetSymbols.HasAttributeAssignableTo(parameter, WellKnownTypes.WolverineEntityAttribute);

    static bool IsAggregateParameter(IParameterSymbol parameter) =>
        DotNetSymbols.HasAttributeAssignableTo(parameter, WellKnownTypes.WolverineWriteModelAttribute) ||
        DotNetSymbols.HasAttribute(parameter, WellKnownTypes.WolverineLegacyWriteAggregateAttribute) ||
        DotNetSymbols.HasAttribute(parameter, WellKnownTypes.WolverineHttpAggregateAttribute);

    static bool IsAggregateWorkflow(IMethodSymbol method) =>
        DotNetSymbols.HasAttributeAssignableTo(method, WellKnownTypes.WolverineAggregateHandlerAttribute) ||
        DotNetSymbols.HasAttributeAssignableTo(method, WellKnownTypes.WolverineLegacyAggregateHandlerAttribute) ||
        DotNetSymbols.HasAttributeAssignableTo(method.ContainingType, WellKnownTypes.WolverineAggregateHandlerAttribute) ||
        method.ContainingType.Name.EndsWith("AggregateHandler", StringComparison.Ordinal) ||
        method.Parameters.Any(IsAggregateParameter);

    static IEnumerable<ITypeSymbol> AggregateReturnEvents(
        IMethodSymbol method,
        DotNetProjectCompilation project) =>
        WolverineReturnConsequences.Classify(
                method,
                project,
                isHttpEndpoint: false,
                aggregateWorkflow: true,
                hasEventStream: false)
            .Where(_ => _.Kind == WolverineReturnConsequenceKind.PersistedEvent)
            .Select(_ => _.Type);

    static IEnumerable<ITypeSymbol> PersistenceEvents(IMethodSymbol method, DotNetProjectCompilation project)
    {
        foreach (var (declaration, semanticModel) in WolverineMethodSyntax.Declarations(method, project))
        {
            foreach (var invocation in declaration.DescendantNodes().OfType<InvocationExpressionSyntax>())
            {
                if (semanticModel.GetOperation(invocation) is Microsoft.CodeAnalysis.Operations.IInvocationOperation operation &&
                    WolverineEventStreams.IsExactAppend(operation, project))
                {
                    continue;
                }

                if (semanticModel.GetSymbolInfo(invocation).Symbol is not IMethodSymbol invoked ||
                    !_persistenceMethods.Contains(invoked.Name) ||
                    !IsPersistenceNamespace(invoked))
                {
                    continue;
                }

                foreach (var argument in invocation.ArgumentList.Arguments)
                {
                    if (semanticModel.GetTypeInfo(argument.Expression).Type is INamedTypeSymbol eventType &&
                        IsEventPayloadType(eventType) &&
                        !SymbolEqualityComparer.Default.Equals(eventType, method.ContainingType))
                    {
                        yield return eventType;
                    }
                }
            }

            foreach (var assignment in declaration.DescendantNodes().OfType<AssignmentExpressionSyntax>())
            {
                if (!assignment.IsKind(Microsoft.CodeAnalysis.CSharp.SyntaxKind.AddAssignmentExpression) ||
                    semanticModel.GetTypeInfo(assignment.Left).Type is not INamedTypeSymbol collection ||
                    !IsEventCollection(collection) ||
                    semanticModel.GetTypeInfo(assignment.Right).Type is not INamedTypeSymbol eventType ||
                    !IsEventPayloadType(eventType))
                {
                    continue;
                }

                yield return eventType;
            }

            foreach (var collectionExpression in declaration.DescendantNodes().OfType<CollectionExpressionSyntax>())
            {
                if (semanticModel.GetTypeInfo(collectionExpression).ConvertedType is not INamedTypeSymbol collectionType ||
                    !IsEventCollection(collectionType))
                {
                    continue;
                }

                foreach (var element in collectionExpression.Elements.OfType<ExpressionElementSyntax>())
                {
                    if (semanticModel.GetTypeInfo(element.Expression).Type is INamedTypeSymbol eventType &&
                        IsEventPayloadType(eventType))
                    {
                        yield return eventType;
                    }
                }
            }
        }
    }

    static bool IsEventCollection(INamedTypeSymbol type)
    {
        var metadataName = DotNetSubjectIds.MetadataName(type.OriginalDefinition);
        return metadataName == WellKnownTypes.WolverineEvents ||
               metadataName == WellKnownTypes.WolverineEventsToAppend;
    }

    static bool IsPersistenceNamespace(IMethodSymbol method)
    {
        var candidate = method.ReducedFrom ?? method;
        var @namespace = candidate.ContainingNamespace.ToDisplayString();
        return @namespace.StartsWith("Marten", StringComparison.Ordinal) ||
               @namespace.StartsWith("JasperFx.Events", StringComparison.Ordinal) ||
               @namespace.StartsWith("Wolverine.Marten", StringComparison.Ordinal);
    }

    static string? IdentityPropertyName(INamedTypeSymbol command, INamedTypeSymbol? aggregate)
    {
        var identity = IdentityProperty(command, aggregate);
        return identity is null ? null : LowerFirst(identity.Name);
    }

    static IPropertySymbol? IdentityProperty(INamedTypeSymbol command, INamedTypeSymbol? aggregate)
    {
        var properties = command.GetMembers().OfType<IPropertySymbol>().ToArray();
        var attributed = properties.FirstOrDefault(_ => _.GetAttributes().Any(attribute =>
            attribute.AttributeClass is { } attributeType &&
            DotNetSubjectIds.MetadataName(attributeType.OriginalDefinition) == WellKnownTypes.JasperFxIdentityAttribute));
        if (attributed is not null)
        {
            return attributed;
        }

        if (aggregate is not null)
        {
            var aggregateId = properties.FirstOrDefault(_ => string.Equals(_.Name, $"{aggregate.Name}Id", StringComparison.OrdinalIgnoreCase));
            if (aggregateId is not null)
            {
                return aggregateId;
            }
        }

        return properties.FirstOrDefault(_ => string.Equals(_.Name, "Id", StringComparison.OrdinalIgnoreCase));
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

    static bool IsHandler(
        INamedTypeSymbol type,
        IMethodSymbol method,
        WolverineHandlerDiscoveryPolicy discovery)
    {
        if (type.IsGenericType || (type.IsAbstract && !type.IsStatic) || !discovery.Includes(type))
        {
            return false;
        }

        var explicitMethod = DotNetSymbols.HasAttribute(method, WellKnownTypes.WolverineHandlerAttribute) ||
                             DotNetSymbols.HasAttribute(method, WellKnownTypes.WolverineLegacyHandlerAttribute);
        return explicitMethod || _handlerMethodNames.Contains(method.Name);
    }

    static bool IsIgnored(ISymbol symbol) =>
        DotNetSymbols.HasAttribute(symbol, WellKnownTypes.WolverineIgnoreAttribute) ||
        DotNetSymbols.HasAttribute(symbol, WellKnownTypes.WolverineLegacyIgnoreAttribute);

    static bool IsPublicSourceType(INamedTypeSymbol type, DotNetProjectCompilation project) =>
        type.DeclaredAccessibility == Accessibility.Public && type.Locations.Any(_ => IsAuthoredSourceLocation(_, project));

    static bool IsPublicSourceMethod(IMethodSymbol method, DotNetProjectCompilation project) =>
        method.DeclaredAccessibility == Accessibility.Public &&
        method.MethodKind == MethodKind.Ordinary &&
        method.Locations.Any(_ => IsAuthoredSourceLocation(_, project));

    static bool IsAuthoredSourceLocation(Location location, DotNetProjectCompilation project) =>
        location.IsInSource &&
        location.SourceTree is not null &&
        project.AuthoredSyntaxTrees.Contains(location.SourceTree) &&
        !DotNetGeneratedSource.IsGenerated(location.SourceTree);

    static bool IsSourceType(ITypeSymbol type) =>
        type is INamedTypeSymbol named && named.Locations.Any(_ => _.IsInSource);

    static bool IsEventPayloadType(ITypeSymbol type) =>
        type is INamedTypeSymbol named &&
        type.SpecialType == SpecialType.None &&
        (WolverineReturnConsequences.IsTimeoutMessage(named) || !WolverineReturnTypes.IsSpecialReturn(named)) &&
        !DotNetSubjectIds.MetadataName(named.OriginalDefinition).StartsWith("System.", StringComparison.Ordinal);

    static bool IsInfrastructureParameter(ITypeSymbol type)
    {
        if (WolverineEventStreams.IsEventStream(type))
        {
            return true;
        }

        if (type.SpecialType != SpecialType.None || type is not INamedTypeSymbol named)
        {
            return type.SpecialType != SpecialType.None;
        }

        var metadataName = DotNetSubjectIds.MetadataName(named.OriginalDefinition);
        return metadataName.StartsWith("Marten.", StringComparison.Ordinal) ||
               metadataName.StartsWith("Wolverine.", StringComparison.Ordinal) ||
               metadataName.StartsWith("Microsoft.AspNetCore.", StringComparison.Ordinal) ||
               metadataName.StartsWith("Microsoft.Extensions.", StringComparison.Ordinal) ||
               metadataName == "System.Threading.CancellationToken";
    }

    static ArtifactFact Artifact(
        string id,
        ArtifactKey key,
        string name,
        string? file,
        IReadOnlyList<PropertyDefinition> properties,
        Evidence evidence) => new()
        {
            Id = new FactId { Value = id },
            Subject = key.Subject,
            Definition = new ArtifactDefinition
            {
                Key = key,
                Name = name,
                File = file,
                Properties = properties
            },
            Evidence = evidence
        };

    static ArtifactPlacementFact Placement(
        string id,
        ArtifactKey artifact,
        ArtifactPlacement placement,
        Evidence evidence) => new()
        {
            Id = new FactId { Value = id },
            Subject = artifact.Subject,
            Artifact = artifact,
            Placement = placement,
            Evidence = evidence
        };

    static RelationshipFact Relationship(
        string id,
        SubjectId source,
        RelationshipKind kind,
        SubjectId target,
        Evidence evidence,
        string? sourceMember = null,
        string? targetMember = null,
        string? discriminator = null,
        bool isCollection = false,
        bool isOptional = false) => new()
        {
            Id = new FactId { Value = id },
            Subject = source,
            Definition = new RelationshipDefinition
            {
                Key = new RelationshipKey
                {
                    Kind = kind,
                    Source = source,
                    Target = target,
                    Discriminator = discriminator
                },
                SourceMember = sourceMember,
                TargetMember = targetMember,
                IsCollection = isCollection,
                IsOptional = isOptional
            },
            Evidence = evidence
        };

    static string StateFeature(
        string requestName,
        INamedTypeSymbol? aggregateType,
        IReadOnlyList<WolverineStateBinding> streamBindings,
        INamedTypeSymbol? dcbModelType = null)
    {
        if (aggregateType is not null)
        {
            return aggregateType.Name;
        }

        if (dcbModelType is not null)
        {
            return dcbModelType.Name;
        }

        var models = streamBindings
            .Select(_ => _.ModelType)
            .GroupBy(DotNetSubjectIds.MetadataName, StringComparer.Ordinal)
            .Select(_ => _.First())
            .ToArray();
        return models.Length == 1 ? models[0].Name : requestName;
    }

    static ArtifactPlacement BehaviorPlacement(
        DotNetProjectCompilation project,
        DotNetAdapterOptions options,
        string feature,
        string slice,
        GenerationSliceKind kind) => new()
        {
            Module = ScreenplayNames.Declaration(options.Module ?? project.Name),
            Features = [feature],
            Slice = slice,
            SliceKind = kind
        };

    static Evidence MethodEvidence(
        IMethodSymbol method,
        DotNetProjectCompilation project,
        AdapterIdentity adapter,
        EvidenceStrength strength,
        string explanation) =>
        CritterStackSource.EvidenceFor(method, adapter, project, strength, explanation);

    static SubjectId MethodSubject(DotNetProjectCompilation project, IMethodSymbol method, string role) => new()
    {
        Value = $"{project.SubjectForType(method.ContainingType).Value}#{role}:{method.MetadataName}"
    };

    static string? SourceFileOf(ISymbol symbol, DotNetProjectCompilation project) =>
        CritterStackSource.EvidenceFor(symbol, new AdapterIdentity { Id = "source", Version = "1" }, project, EvidenceStrength.Exact).Source?.Path;

    static string LowerFirst(string value) => value.Length == 0
        ? value
        : $"{char.ToLowerInvariant(value[0])}{value[1..]}";
}
