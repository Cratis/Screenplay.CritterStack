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
        var catalog = new DotNetArtifactCatalog(project.Compilation);
        foreach (var type in catalog.Types.Where(_ => IsPublicSourceType(_, project) && !IsIgnored(_) && !WolverineSagaFacts.IsSagaType(_, project)))
        {
            foreach (var method in type.GetMembers().OfType<IMethodSymbol>().Where(_ => IsPublicSourceMethod(_, project) && !IsIgnored(_)))
            {
                var endpoint = EndpointFor(method);
                if (endpoint is not null)
                {
                    AnalyzeEndpoint(project, options, adapter, subjects, endpoint, validationAuthorization, facts, placements, diagnostics);
                }
                else if (IsHandler(type, method, discovery.Policy))
                {
                    AnalyzeHandler(project, options, adapter, subjects, method, validationAuthorization, facts, placements, diagnostics);
                }
            }
        }

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

    static void AnalyzeEndpoint(
        DotNetProjectCompilation project,
        DotNetAdapterOptions options,
        AdapterIdentity adapter,
        CritterStackSubjectResolver subjects,
        HttpEndpoint endpoint,
        WolverineValidationAuthorizationDiscoveryResult validationAuthorization,
        List<GenerationFact> facts,
        List<CritterStackPlacementIntent> placements,
        List<GenerationDiagnostic> diagnostics)
    {
        if (string.Equals(endpoint.Verb, "GET", StringComparison.Ordinal) ||
            string.Equals(endpoint.Verb, "QUERY", StringComparison.Ordinal))
        {
            AnalyzeQuery(project, options, adapter, subjects, endpoint, validationAuthorization, facts, placements, diagnostics);
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
            ? subjects.SubjectForType(project, commandType)
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
        var compatibilityPlacement = CritterStackSourcePlacement.CompatibilityPlacement(
            project,
            options,
            feature,
            commandName,
            GenerationSliceKind.StateChange);
        var commandKey = new ArtifactKey { Subject = commandSubject, Kind = ArtifactKind.Command };
        var commandSourceOwner = commandType is null
            ? subjects.SubjectForType(project, method.ContainingType)
            : null;

        facts.Add(Artifact($"wolverine:command:{commandSubject.Value}", commandKey, commandName, file, properties, evidence));
        placements.Add(new(
            $"wolverine:placement:command:{commandSubject.Value}",
            commandKey,
            commandSourceOwner,
            compatibilityPlacement,
            evidence));
        diagnostics.Add(new GenerationDiagnostic
        {
            Code = WolverineDiagnosticCodes.HttpMetadataOmitted,
            Severity = GenerationDiagnosticSeverity.Information,
            Outcome = GenerationDiagnosticOutcome.Unsupported,
            Message = $"HTTP {endpoint.Verb} route '{endpoint.Route}' for '{commandName}' is not represented by the current Screenplay language",
            Source = evidence.Source,
            Subject = commandSubject
        });

        if (aggregate?.Type is INamedTypeSymbol aggregateType)
        {
            AddReadModelAndRelationship(project, adapter, subjects, commandSubject, commandType, aggregateType, facts, evidence);
            if (commandType is not null && IdentityProperty(commandType, aggregateType) is null)
            {
                diagnostics.Add(new GenerationDiagnostic
                {
                    Code = WolverineDiagnosticCodes.RouteIdentityOmitted,
                    Severity = GenerationDiagnosticSeverity.Warning,
                    Outcome = GenerationDiagnosticOutcome.Unsupported,
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
                    Outcome = GenerationDiagnosticOutcome.Unsupported,
                    Message = $"The expected stream version on '{commandName}' cannot be represented exactly by Screenplay concurrency",
                    Source = evidence.Source,
                    Subject = commandSubject
                });
            }
        }

        AddPersistenceBoundReads(project, adapter, subjects, commandSubject, method, aggregate, facts);
        AddEventStreamBindingFacts(
            project,
            adapter,
            subjects,
            commandSubject,
            commandName,
            streamBindings,
            isHttpEndpoint: true,
            facts,
            diagnostics);
        AddDcbFacts(project, adapter, subjects, commandSubject, commandName, dcb, facts, diagnostics);

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
            AddEventAndProduction(project, subjects, commandSubject, eventType, compatibilityPlacement, evidence, declarative, facts, placements);
        }
        AddEventStreamAppendFacts(project, adapter, subjects, commandSubject, compatibilityPlacement, appendDiscovery, facts, placements, diagnostics);

        var returnConsequences = WolverineReturnConsequences.Classify(
            method,
            project,
            isHttpEndpoint: true,
            aggregateWorkflow || dcb is { IsBoundaryParameter: false },
            dcb is null && streamBindings.Count > 0);
        var outgoingMessages = DiscoverOutgoingMessages(method, project);
        AddDocumentDeletes(project, subjects, commandSubject, method, evidence, facts);
        AddReturnConsequences(project, subjects, commandSubject, returnConsequences, evidence, facts, sagaAnalysis: false);
        AddStorageActionConsequences(project, adapter, subjects, commandSubject, method, returnConsequences, evidence, facts);
        AddDirectBusConsequences(project, subjects, commandSubject, method, evidence, facts, diagnostics, sagaAnalysis: false);
        AddOutgoingMessages(project, subjects, commandSubject, method, outgoingMessages, evidence, facts, diagnostics, sagaAnalysis: false);
    }

    static void AnalyzeHandler(
        DotNetProjectCompilation project,
        DotNetAdapterOptions options,
        AdapterIdentity adapter,
        CritterStackSubjectResolver subjects,
        IMethodSymbol method,
        WolverineValidationAuthorizationDiscoveryResult validationAuthorization,
        List<GenerationFact> facts,
        List<CritterStackPlacementIntent> placements,
        List<GenerationDiagnostic> diagnostics)
    {
        var request = RequestParameter(method, project);
        var requestType = request is not null ? MessageElementType(request.Type) : null;
        if (requestType is null || WolverineSagaTypes.IsSagaState(requestType, project))
        {
            return;
        }

        var batched = request!.Type is IArrayTypeSymbol;
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
        var compoundStages = dcb is null
            ? WolverineCompoundStages.StagesFor(method, requestType, project)
            : [];
        if (bodyEvents.Length == 0 &&
            returnEvents.Count == 0 &&
            deletedDocuments.Length == 0 &&
            !appendDiscovery.HasDirectWrite &&
            !streamBindings.Any(_ => _.LoadsModel) &&
            !returnConsequences.Any(_ => _.Kind == WolverineReturnConsequenceKind.StorageAction) &&
            !method.Parameters.Any(IsPersistenceBoundParameter) &&
            dcb is null)
        {
            var automationReturns = hasDocumentPersistence ? [] : returnConsequences;
            var automationOutgoingMessages = hasDocumentPersistence ? [] : outgoingMessages;
            if (batched ||
                compoundStages.Count > 0 ||
                busConsequences.Count > 0 ||
                automationReturns.Any(IsCascadeConsequence) ||
                automationOutgoingMessages.Count > 0)
            {
                AnalyzeAutomation(
                    project,
                    options,
                    adapter,
                    subjects,
                    method,
                    requestType,
                    batched,
                    compoundStages,
                    automationReturns,
                    automationOutgoingMessages,
                    busConsequences,
                    validationAuthorization,
                    facts,
                    placements,
                    diagnostics);
            }
            else
            {
                AddHandlerChainConfigurationDiagnostics(
                    project,
                    adapter,
                    MethodSubject(project, method, "handler"),
                    method,
                    diagnostics);
            }

            return;
        }

        var commandSubject = subjects.SubjectForType(project, requestType);
        diagnostics.AddRange(validationAuthorization.ValidationDiagnostics(
            method,
            requestType,
            commandSubject,
            isHttpEndpoint: false));
        var evidenceExplanation = $"Wolverine message handler with persistence effects{(batched ? " (batched: Wolverine delivers arrays of this message)" : string.Empty)}";
        var evidence = MethodEvidence(method, project, adapter, EvidenceStrength.Exact, evidenceExplanation);
        var feature = StateFeature(requestType.Name, aggregate?.Type as INamedTypeSymbol, streamBindings, dcb?.ModelType);
        var compatibilityPlacement = CritterStackSourcePlacement.CompatibilityPlacement(
            project,
            options,
            feature,
            requestType.Name,
            GenerationSliceKind.StateChange);
        var key = new ArtifactKey { Subject = commandSubject, Kind = ArtifactKind.Command };
        facts.Add(Artifact(
            $"wolverine:command:{commandSubject.Value}",
            key,
            requestType.Name,
            evidence.Source?.Path,
            CommandProperties(requestType, aggregate?.Type as INamedTypeSymbol, streamBindings),
            evidence));
        placements.Add(new(
            $"wolverine:placement:command:{commandSubject.Value}",
            key,
            null,
            compatibilityPlacement,
            evidence));

        if (aggregate?.Type is INamedTypeSymbol aggregateType)
        {
            AddReadModelAndRelationship(project, adapter, subjects, commandSubject, requestType, aggregateType, facts, evidence);
        }
        AddPersistenceBoundReads(project, adapter, subjects, commandSubject, method, aggregate, facts);
        AddEventStreamBindingFacts(
            project,
            adapter,
            subjects,
            commandSubject,
            requestType.Name,
            streamBindings,
            isHttpEndpoint: false,
            facts,
            diagnostics);
        AddDcbFacts(project, adapter, subjects, commandSubject, requestType.Name, dcb, facts, diagnostics);

        foreach (var eventType in returnEvents.Concat(bodyEvents).Distinct(SymbolEqualityComparer.Default).OfType<INamedTypeSymbol>())
        {
            var isImperativeDcbEvent = dcb?.ImperativeEventTypes.Any(_ => SymbolEqualityComparer.Default.Equals(_, eventType)) ?? false;
            var declarative = returnEvents.Any(_ => SymbolEqualityComparer.Default.Equals(_, eventType)) &&
                              !bodyEvents.Any(_ => SymbolEqualityComparer.Default.Equals(_, eventType)) &&
                              !isImperativeDcbEvent;
            AddEventAndProduction(project, subjects, commandSubject, eventType, compatibilityPlacement, evidence, declarative, facts, placements);
        }
        AddEventStreamAppendFacts(project, adapter, subjects, commandSubject, compatibilityPlacement, appendDiscovery, facts, placements, diagnostics);

        AddDocumentDeletes(project, subjects, commandSubject, method, evidence, facts);
        AddReturnConsequences(project, subjects, commandSubject, returnConsequences, evidence, facts, sagaAnalysis: false);
        AddStorageActionConsequences(project, adapter, subjects, commandSubject, method, returnConsequences, evidence, facts);
        AddDirectBusConsequences(project, subjects, commandSubject, method, evidence, facts, diagnostics, busConsequences, sagaAnalysis: false);
        AddOutgoingMessages(project, subjects, commandSubject, method, outgoingMessages, evidence, facts, diagnostics, sagaAnalysis: false);
        AddCompoundStageConsequences(project, adapter, subjects, commandSubject, method, compoundStages, facts, diagnostics);
        AddHandlerChainConfigurationDiagnostics(project, adapter, commandSubject, method, diagnostics);
    }

    static void AnalyzeAutomation(
        DotNetProjectCompilation project,
        DotNetAdapterOptions options,
        AdapterIdentity adapter,
        CritterStackSubjectResolver subjects,
        IMethodSymbol method,
        INamedTypeSymbol requestType,
        bool batched,
        IReadOnlyList<WolverineCompoundStage> compoundStages,
        IReadOnlyList<WolverineReturnConsequence> returnConsequences,
        IReadOnlyList<WolverineOutgoingMessageConsequence> outgoingMessages,
        IReadOnlyList<WolverineBusConsequence> busConsequences,
        WolverineValidationAuthorizationDiscoveryResult validationAuthorization,
        List<GenerationFact> facts,
        List<CritterStackPlacementIntent> placements,
        List<GenerationDiagnostic> diagnostics)
    {
        var requestSubject = subjects.SubjectForType(project, requestType);
        var reactionSubject = MethodSubject(project, method, "reaction");
        diagnostics.AddRange(validationAuthorization.ValidationDiagnostics(
            method,
            requestType,
            reactionSubject,
            isHttpEndpoint: false));
        var evidenceExplanation = $"Wolverine message handler with direct bus or return automation consequences{(batched ? " (batched: Wolverine delivers arrays of this message)" : string.Empty)}";
        var evidence = MethodEvidence(method, project, adapter, EvidenceStrength.Exact, evidenceExplanation);
        var reactionName = method.ContainingType.Name.EndsWith("Handler", StringComparison.Ordinal)
            ? method.ContainingType.Name[..^"Handler".Length]
            : method.ContainingType.Name;
        var compatibilityPlacement = CritterStackSourcePlacement.CompatibilityPlacement(
            project,
            options,
            requestType.Name,
            reactionName,
            GenerationSliceKind.Automation);
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
        placements.Add(new(
            $"wolverine:placement:reaction:{reactionSubject.Value}",
            reactionKey,
            subjects.SubjectForType(project, method.ContainingType),
            compatibilityPlacement,
            evidence));
        facts.Add(Relationship(
            $"wolverine:handles:{reactionSubject.Value}:{requestSubject.Value}",
            reactionSubject,
            RelationshipKind.Handles,
            requestSubject,
            evidence,
            isCollection: batched));
        AddReturnConsequences(project, subjects, reactionSubject, returnConsequences, evidence, facts, sagaAnalysis: false);
        AddOutgoingMessages(project, subjects, reactionSubject, method, outgoingMessages, evidence, facts, diagnostics, sagaAnalysis: false);
        AddDirectBusConsequences(project, subjects, reactionSubject, method, evidence, facts, diagnostics, busConsequences, sagaAnalysis: false);
        AddCompoundStageConsequences(project, adapter, subjects, reactionSubject, method, compoundStages, facts, diagnostics);
        AddHandlerChainConfigurationDiagnostics(project, adapter, reactionSubject, method, diagnostics);
    }

    static void AnalyzeQuery(
        DotNetProjectCompilation project,
        DotNetAdapterOptions options,
        AdapterIdentity adapter,
        CritterStackSubjectResolver subjects,
        HttpEndpoint endpoint,
        WolverineValidationAuthorizationDiscoveryResult validationAuthorization,
        List<GenerationFact> facts,
        List<CritterStackPlacementIntent> placements,
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
        if (model is null || !IsSourceType(model) || WolverineSagaTypes.IsSagaState(model, project))
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
        var compatibilityPlacement = CritterStackSourcePlacement.CompatibilityPlacement(
            project,
            options,
            model.Name,
            queryName,
            GenerationSliceKind.StateView);
        var queryKey = new ArtifactKey { Subject = querySubject, Kind = ArtifactKind.Query };
        var modelSubject = subjects.SubjectForType(project, model);
        var modelKey = new ArtifactKey { Subject = modelSubject, Kind = ArtifactKind.ReadModel };

        facts.Add(Artifact(
            $"wolverine:query:{querySubject.Value}",
            queryKey,
            queryName,
            evidence.Source?.Path,
            QueryProperties(endpoint.Method, compiledQueries.SelectMany(_ => _.Parameters)),
            evidence));
        placements.Add(new(
            $"wolverine:placement:query:{querySubject.Value}",
            queryKey,
            subjects.SubjectForType(project, endpoint.Method.ContainingType),
            compatibilityPlacement,
            evidence));
        diagnostics.Add(new GenerationDiagnostic
        {
            Code = WolverineDiagnosticCodes.HttpMetadataOmitted,
            Severity = GenerationDiagnosticSeverity.Information,
            Outcome = GenerationDiagnosticOutcome.Unsupported,
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
        placements.Add(new(
            $"wolverine:placement:read-model:{modelSubject.Value}",
            modelKey,
            null,
            CritterStackSourcePlacement.CompatibilityPlacement(
                project,
                options,
                model.Name,
                model.Name,
                GenerationSliceKind.StateView),
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
        AddPersistenceBoundReads(
            project,
            adapter,
            subjects,
            querySubject,
            endpoint.Method,
            aggregateParameter: null,
            facts);

        foreach (var compiledQuery in compiledQueries
                     .GroupBy(_ => subjects.SubjectForType(project, _.DocumentType))
                     .Select(_ => _.First()))
        {
            var documentSubject = subjects.SubjectForType(project, compiledQuery.DocumentType);
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
        CritterStackSubjectResolver subjects,
        SubjectId commandSubject,
        INamedTypeSymbol? commandType,
        INamedTypeSymbol aggregateType,
        List<GenerationFact> facts,
        Evidence evidence)
    {
        var aggregateSubject = subjects.SubjectForType(project, aggregateType);
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

    static void AddPersistenceBoundReads(
        DotNetProjectCompilation project,
        AdapterIdentity adapter,
        CritterStackSubjectResolver subjects,
        SubjectId sourceSubject,
        IMethodSymbol method,
        IParameterSymbol? aggregateParameter,
        List<GenerationFact> facts)
    {
        var artifactIds = new HashSet<string>(StringComparer.Ordinal);
        foreach (var parameter in method.Parameters.Where(IsPersistenceBoundParameter))
        {
            if (SymbolEqualityComparer.Default.Equals(parameter, aggregateParameter) ||
                PersistenceBoundRead(parameter) is not { } binding)
            {
                continue;
            }

            var documentSubject = subjects.SubjectForType(project, binding.DocumentType);
            var artifactId = $"wolverine:read-model:{documentSubject.Value}";
            var evidence = CritterStackSource.EvidenceFor(
                parameter,
                adapter,
                project,
                EvidenceStrength.Exact,
                binding.Explanation);
            if (artifactIds.Add(artifactId))
            {
                facts.Add(Artifact(
                    artifactId,
                    new ArtifactKey { Subject = documentSubject, Kind = ArtifactKind.ReadModel },
                    binding.DocumentType.Name,
                    SourceFileOf(binding.DocumentType, project),
                    DotNetTypeShapes.PropertiesOf(binding.DocumentType),
                    evidence));
            }

            facts.Add(Relationship(
                $"wolverine:reads:{sourceSubject.Value}:{documentSubject.Value}:{parameter.Name}",
                sourceSubject,
                RelationshipKind.Reads,
                documentSubject,
                evidence,
                discriminator: binding.Discriminator,
                isCollection: binding.IsCollection,
                isOptional: binding.IsOptional));
        }
    }

    static (INamedTypeSymbol DocumentType, string Discriminator, string Explanation, bool IsCollection, bool IsOptional)? PersistenceBoundRead(
        IParameterSymbol parameter)
    {
        var isNullable = parameter.Type.NullableAnnotation == NullableAnnotation.Annotated ||
                         parameter.Type is INamedTypeSymbol { OriginalDefinition.SpecialType: SpecialType.System_Nullable_T };
        if (IsEntityParameter(parameter))
        {
            var documentType = UnwrapNullable(parameter.Type);
            return documentType is null
                ? null
                : (documentType, "entity", "Wolverine loads this entity by identity for the handler", false, isNullable || IsOptionalEntity(parameter));
        }

        if (DotNetSymbols.HasAttributeAssignableTo(parameter, WellKnownTypes.WolverineFirstOrDefaultAttribute))
        {
            var documentType = UnwrapNullable(parameter.Type);
            return documentType is null
                ? null
                : (documentType, "first-or-default", "Wolverine loads the first matching document for the handler", false, isNullable);
        }

        if (DotNetSymbols.HasAttributeAssignableTo(parameter, WellKnownTypes.WolverineQueryableAttribute) &&
            parameter.Type is INamedTypeSymbol queryable &&
            DotNetSubjectIds.MetadataName(queryable.OriginalDefinition) == "System.Linq.IQueryable`1" &&
            queryable.TypeArguments[0] is INamedTypeSymbol elementType)
        {
            return (elementType, "queryable", "Wolverine exposes this document set as a queryable to the handler", true, isNullable);
        }

        return null;
    }

    static INamedTypeSymbol? UnwrapNullable(ITypeSymbol type) =>
        type is INamedTypeSymbol { OriginalDefinition.SpecialType: SpecialType.System_Nullable_T } nullable
            ? nullable.TypeArguments[0] as INamedTypeSymbol
            : type as INamedTypeSymbol;

    static bool IsOptionalEntity(IParameterSymbol parameter) =>
        parameter.GetAttributes()
            .Where(_ => _.AttributeClass is not null && DotNetSymbols.IsOrInheritsFrom(_.AttributeClass, WellKnownTypes.WolverineEntityAttribute))
            .SelectMany(_ => _.NamedArguments)
            .Any(_ => string.Equals(_.Key, "Required", StringComparison.Ordinal) && _.Value.Value is false);

    static void AddDcbFacts(
        DotNetProjectCompilation project,
        AdapterIdentity adapter,
        CritterStackSubjectResolver subjects,
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

        var aggregateSubject = subjects.SubjectForType(project, dcb.ModelType);
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

        foreach (var condition in dcb.Conditions.Where(condition =>
                     condition.EventType is null || !WolverineSagaTypes.IsSagaState(condition.EventType, project)))
        {
            var tagSubject = subjects.SubjectForType(project, condition.TagType);
            var eventSubject = condition.EventType is null ? null : subjects.SubjectForType(project, condition.EventType);
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

        foreach (var eventType in dcb.QueryEventTypes.Where(eventType => !WolverineSagaTypes.IsSagaState(eventType, project)))
        {
            var eventSubject = subjects.SubjectForType(project, eventType);
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
            Outcome = GenerationDiagnosticOutcome.Unsupported,
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
                Outcome = GenerationDiagnosticOutcome.Unknown,
                Message = $"The EventTagQuery companion '{dcb.Companion.Name}' for '{commandName}' is outside the bounded direct fluent-chain shapes and was not interpreted",
                Source = dcb.QuerySource,
                Subject = commandSubject
            });
        }
    }

    static void AddEventStreamBindingFacts(
        DotNetProjectCompilation project,
        AdapterIdentity adapter,
        CritterStackSubjectResolver subjects,
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
            var aggregateSubject = subjects.SubjectForType(project, binding.ModelType);
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
                    Outcome = GenerationDiagnosticOutcome.Unsupported,
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
                    Outcome = binding.HasAmbiguousConventionalVersion
                        ? GenerationDiagnosticOutcome.Unknown
                        : GenerationDiagnosticOutcome.Unsupported,
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
                    Outcome = GenerationDiagnosticOutcome.Unsupported,
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
        CritterStackSubjectResolver subjects,
        SubjectId commandSubject,
        ArtifactPlacement compatibilityPlacement,
        WolverineEventStreamAppendDiscovery discovery,
        List<GenerationFact> facts,
        List<CritterStackPlacementIntent> placements,
        List<GenerationDiagnostic> diagnostics)
    {
        foreach (var unresolved in discovery.Unresolved)
        {
            diagnostics.Add(new GenerationDiagnostic
            {
                Code = WolverineDiagnosticCodes.EventWriteTargetUnresolved,
                Severity = GenerationDiagnosticSeverity.Warning,
                Outcome = GenerationDiagnosticOutcome.Unknown,
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
            var aggregateSubject = subjects.SubjectForType(project, binding.ModelType);
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
                var eventSubject = subjects.SubjectForType(project, eventType);
                if (producedEvents.Add(eventSubject))
                {
                    AddEventAndProduction(
                        project,
                        subjects,
                        commandSubject,
                        eventType,
                        compatibilityPlacement,
                        evidence,
                        declarative: false,
                        facts,
                        placements);
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
        CritterStackSubjectResolver subjects,
        SubjectId commandSubject,
        INamedTypeSymbol eventType,
        ArtifactPlacement compatibilityPlacement,
        Evidence evidence,
        bool declarative,
        List<GenerationFact> facts,
        List<CritterStackPlacementIntent> placements)
    {
        if (WolverineSagaTypes.IsSagaState(eventType, project))
        {
            return;
        }

        var eventSubject = subjects.SubjectForType(project, eventType);
        var eventKey = new ArtifactKey { Subject = eventSubject, Kind = ArtifactKind.Event };
        facts.Add(Artifact(
            $"wolverine:event:{eventSubject.Value}",
            eventKey,
            eventType.Name,
            SourceFileOf(eventType, project),
            DotNetTypeShapes.PropertiesOf(eventType),
            evidence));
        placements.Add(new(
            $"wolverine:placement:event:{eventSubject.Value}:{commandSubject.Value}",
            eventKey,
            null,
            compatibilityPlacement,
            evidence));
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
        CritterStackSubjectResolver subjects,
        SubjectId sourceSubject,
        IMethodSymbol method,
        Evidence evidence,
        List<GenerationFact> facts,
        List<GenerationDiagnostic> diagnostics,
        IReadOnlyList<WolverineBusConsequence>? discovered = null,
        bool sagaAnalysis = false)
    {
        foreach (var consequence in discovered ?? WolverineBusConsequences.Discover(method, project))
        {
            if (consequence.MessageType is not INamedTypeSymbol messageType ||
                !IsEventPayloadType(messageType) ||
                WolverineSagaTypes.IsSagaState(messageType, project))
            {
                continue;
            }

            var messageSubject = subjects.SubjectForType(project, messageType);
            AddMessageRelationship(
                project,
                subjects,
                sourceSubject,
                messageType,
                evidence,
                RelationshipKind.Publishes,
                $"wolverine:publishes:{consequence.Discriminator}:{sourceSubject.Value}:{messageSubject.Value}",
                consequence.Discriminator,
                facts,
                sagaAnalysis);
            diagnostics.Add(new GenerationDiagnostic
            {
                Code = consequence.IsScheduled
                    ? WolverineDiagnosticCodes.DelayedMessageOmitted
                    : WolverineDiagnosticCodes.DirectMessageDeliveryOmitted,
                Severity = GenerationDiagnosticSeverity.Warning,
                Outcome = GenerationDiagnosticOutcome.Unsupported,
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
                    if (!IsEventPayloadType(messageType) || WolverineSagaTypes.IsSagaState(messageType, project))
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
        CritterStackSubjectResolver subjects,
        SubjectId sourceSubject,
        IMethodSymbol method,
        IReadOnlyList<WolverineOutgoingMessageConsequence> consequences,
        Evidence evidence,
        List<GenerationFact> facts,
        List<GenerationDiagnostic> diagnostics,
        bool sagaAnalysis)
    {
        foreach (var consequence in consequences)
        {
            var messageSubject = subjects.SubjectForType(project, consequence.MessageType);
            AddMessageRelationship(
                project,
                subjects,
                sourceSubject,
                consequence.MessageType,
                evidence,
                RelationshipKind.Cascades,
                $"wolverine:cascades:{sourceSubject.Value}:{messageSubject.Value}",
                consequence.Delayed ? "delayed" : "immediate",
                facts,
                sagaAnalysis);

            if (consequence.Delayed)
            {
                diagnostics.Add(new GenerationDiagnostic
                {
                    Code = WolverineDiagnosticCodes.DelayedMessageOmitted,
                    Severity = GenerationDiagnosticSeverity.Warning,
                    Outcome = GenerationDiagnosticOutcome.Unsupported,
                    Message = $"Handler '{method.ContainingType.Name}.{method.Name}' dispatches '{consequence.MessageType.Name}' after a delay, which the current Screenplay language cannot represent",
                    Source = evidence.Source,
                    Subject = sourceSubject
                });
            }
        }
    }

    static void AddReturnConsequences(
        DotNetProjectCompilation project,
        CritterStackSubjectResolver subjects,
        SubjectId sourceSubject,
        IReadOnlyList<WolverineReturnConsequence> consequences,
        Evidence evidence,
        List<GenerationFact> facts,
        bool sagaAnalysis)
    {
        foreach (var consequence in consequences.Where(IsCascadeConsequence))
        {
            var messageType = (INamedTypeSymbol)consequence.Type;
            var messageSubject = subjects.SubjectForType(project, messageType);
            AddMessageRelationship(
                project,
                subjects,
                sourceSubject,
                messageType,
                evidence,
                RelationshipKind.Cascades,
                $"wolverine:cascades:return:{sourceSubject.Value}:{consequence.Slot}:{messageSubject.Value}",
                $"return-slot:{consequence.Slot}",
                facts,
                sagaAnalysis);
        }
    }

    static void AddCompoundStageConsequences(
        DotNetProjectCompilation project,
        AdapterIdentity adapter,
        CritterStackSubjectResolver subjects,
        SubjectId sourceSubject,
        IMethodSymbol entryPoint,
        IReadOnlyList<WolverineCompoundStage> stages,
        List<GenerationFact> facts,
        List<GenerationDiagnostic> diagnostics)
    {
        var entryPointName = DotNetMethodIdentity.DisplayName(entryPoint);
        foreach (var stage in stages)
        {
            var evidence = MethodEvidence(
                stage.Method,
                project,
                adapter,
                EvidenceStrength.Exact,
                $"Wolverine compound handler {stage.StageKind} stage for '{entryPointName}'");
            var consequences = WolverineReturnConsequences.Classify(
                stage.Method,
                project,
                isHttpEndpoint: false,
                aggregateWorkflow: false,
                hasEventStream: false);
            if (!string.Equals(stage.StageKind, "load", StringComparison.Ordinal))
            {
                foreach (var consequence in consequences.Where(_ => IsCascadeConsequence(_) && !IsCompoundStageControl(_.Type)))
                {
                    var messageType = (INamedTypeSymbol)consequence.Type;
                    var messageSubject = subjects.SubjectForType(project, messageType);
                    AddMessageRelationship(
                        project,
                        subjects,
                        sourceSubject,
                        messageType,
                        evidence,
                        RelationshipKind.Cascades,
                        $"wolverine:cascades:stage:return:{sourceSubject.Value}:{stage.Method.MetadataName}:{consequence.Slot}:{messageSubject.Value}",
                        $"stage:{stage.Method.Name}",
                        facts,
                        sagaAnalysis: false);
                }
            }

            foreach (var outgoing in DiscoverOutgoingMessages(stage.Method, project))
            {
                var messageSubject = subjects.SubjectForType(project, outgoing.MessageType);
                AddMessageRelationship(
                    project,
                    subjects,
                    sourceSubject,
                    outgoing.MessageType,
                    evidence,
                    RelationshipKind.Cascades,
                    $"wolverine:cascades:stage:{sourceSubject.Value}:{stage.Method.MetadataName}:{messageSubject.Value}",
                    $"stage:{stage.Method.Name}",
                    facts,
                    sagaAnalysis: false);
            }

            var canShortCircuit = consequences.Any(_ => IsCompoundStageControl(_.Type));
            diagnostics.Add(new()
            {
                Code = WolverineDiagnosticCodes.CompoundStageOmitted,
                Severity = GenerationDiagnosticSeverity.Information,
                Outcome = GenerationDiagnosticOutcome.Unsupported,
                Message = $"Compound handler stage '{stage.Method.Name}' ({stage.StageKind}) participates in entry point '{entryPointName}' and {(canShortCircuit ? "can short-circuit" : "does not expose recognized short-circuit control")}; its data-loading and continuation semantics are not fully represented",
                Source = evidence.Source,
                Subject = sourceSubject
            });
        }
    }

    static void AddHandlerChainConfigurationDiagnostics(
        DotNetProjectCompilation project,
        AdapterIdentity adapter,
        SubjectId entryPointSubject,
        IMethodSymbol entryPoint,
        List<GenerationDiagnostic> diagnostics)
    {
        var entryPointName = DotNetMethodIdentity.DisplayName(entryPoint);
        foreach (var configure in entryPoint.ContainingType.GetMembers()
                     .OfType<IMethodSymbol>()
                     .Where(_ =>
                         string.Equals(_.Name, "Configure", StringComparison.Ordinal) &&
                         _.DeclaredAccessibility == Accessibility.Public &&
                         _.IsStatic &&
                         _.ReturnsVoid &&
                         _.Parameters is [{ Type: INamedTypeSymbol parameterType }] &&
                         DotNetSubjectIds.MetadataName(parameterType.OriginalDefinition) == WellKnownTypes.WolverineHandlerChain &&
                         _.Locations.Any(location => IsAuthoredSourceLocation(location, project))))
        {
            var evidence = MethodEvidence(
                configure,
                project,
                adapter,
                EvidenceStrength.Exact,
                $"Wolverine per-chain configuration for '{entryPointName}'");
            diagnostics.Add(new()
            {
                Code = WolverineDiagnosticCodes.HandlerChainConfigurationOmitted,
                Severity = GenerationDiagnosticSeverity.Information,
                Outcome = GenerationDiagnosticOutcome.Unsupported,
                Message = $"Handler chain configuration member '{configure.Name}' for entry point '{entryPointName}' may alter retry or discard delivery semantics, which are not represented",
                Source = evidence.Source,
                Subject = entryPointSubject
            });
        }
    }

    static bool IsCompoundStageControl(ITypeSymbol type)
    {
        if (type is not INamedTypeSymbol named)
        {
            return false;
        }

        var metadataName = DotNetSubjectIds.MetadataName(named.OriginalDefinition);
        return string.Equals(metadataName, WellKnownTypes.WolverineHandlerContinuation, StringComparison.Ordinal) ||
               string.Equals(metadataName, WellKnownTypes.WolverineRequirementResult, StringComparison.Ordinal);
    }

    static void AddStorageActionConsequences(
        DotNetProjectCompilation project,
        AdapterIdentity adapter,
        CritterStackSubjectResolver subjects,
        SubjectId sourceSubject,
        IMethodSymbol method,
        IReadOnlyList<WolverineReturnConsequence> consequences,
        Evidence evidence,
        List<GenerationFact> facts)
    {
        var artifactIds = new HashSet<string>(StringComparer.Ordinal);
        var factoryMethodsBySlot = StorageFactoryMethodsBySlot(method, project);
        foreach (var consequence in consequences)
        {
            if (consequence.Kind != WolverineReturnConsequenceKind.StorageAction ||
                consequence.EntityType is not INamedTypeSymbol entityType)
            {
                continue;
            }

            var relationshipKind = StorageActionRelationshipKind(consequence.Slot, entityType, factoryMethodsBySlot);
            if (relationshipKind is null)
            {
                continue;
            }

            var entitySubject = subjects.SubjectForType(project, entityType);
            var artifactId = $"wolverine:read-model:{entitySubject.Value}";
            if (artifactIds.Add(artifactId))
            {
                facts.Add(Artifact(
                    artifactId,
                    new ArtifactKey { Subject = entitySubject, Kind = ArtifactKind.ReadModel },
                    entityType.Name,
                    SourceFileOf(entityType, project),
                    DotNetTypeShapes.PropertiesOf(entityType),
                    CritterStackSource.EvidenceFor(entityType, adapter, project, EvidenceStrength.Exact, "Wolverine persists this model through a returned storage action")));
            }

            facts.Add(Relationship(
                $"wolverine:stores:{sourceSubject.Value}:{entitySubject.Value}:{consequence.Slot}",
                sourceSubject,
                relationshipKind.Value,
                entitySubject,
                evidence with
                {
                    Strength = EvidenceStrength.Exact,
                    Explanation = "Wolverine applies this returned storage action to the persistence layer"
                },
                discriminator: $"storage-action:{consequence.Slot}"));
        }
    }

    static RelationshipKind? StorageActionRelationshipKind(
        int slot,
        ITypeSymbol entityType,
        Dictionary<int, List<IMethodSymbol?>> factoryMethodsBySlot)
    {
        if (!factoryMethodsBySlot.TryGetValue(slot, out var slotMethods))
        {
            return RelationshipKind.Stores;
        }

        var factoryMethods = slotMethods
            .OfType<IMethodSymbol>()
            .Where(_ => _.TypeArguments.Any(typeArgument => SymbolEqualityComparer.Default.Equals(typeArgument, entityType)))
            .ToArray();
        if (slotMethods.Exists(_ => _ is null) || factoryMethods.Length != slotMethods.Count)
        {
            return RelationshipKind.Stores;
        }

        var kinds = factoryMethods
            .Select(_ => _.Name switch
            {
                "Delete" => RelationshipKind.Deletes,
                "Update" => RelationshipKind.Updates,
                "Nothing" or "StartStream" => (RelationshipKind?)null,
                _ => RelationshipKind.Stores
            })
            .Where(_ => _ is not null)
            .Distinct()
            .ToArray();
        return kinds.Length switch
        {
            0 => null,
            1 => kinds[0],
            _ => RelationshipKind.Stores
        };
    }

    static Dictionary<int, List<IMethodSymbol?>> StorageFactoryMethodsBySlot(
        IMethodSymbol method,
        DotNetProjectCompilation project)
    {
        var methodsBySlot = new Dictionary<int, List<IMethodSymbol?>>();
        foreach (var (declaration, semanticModel) in WolverineMethodSyntax.Declarations(method, project))
        {
            foreach (var returned in ReturnedExpressions(declaration))
            {
                var expression = UnwrapReturnedExpression(returned, semanticModel);
                var slotExpressions = expression is TupleExpressionSyntax tuple
                    ? tuple.Arguments.Select(_ => _.Expression).ToArray()
                    : [expression];
                for (var slot = 0; slot < slotExpressions.Length; slot++)
                {
                    if (!methodsBySlot.TryGetValue(slot, out var methods))
                    {
                        methods = [];
                        methodsBySlot.Add(slot, methods);
                    }

                    methods.Add(StorageFactoryMethod(slotExpressions[slot], semanticModel, []));
                }
            }
        }

        return methodsBySlot;
    }

    static IEnumerable<ExpressionSyntax> ReturnedExpressions(MethodDeclarationSyntax declaration)
    {
        if (declaration.ExpressionBody?.Expression is { } expressionBody)
        {
            yield return expressionBody;
        }

        if (declaration.Body is null)
        {
            yield break;
        }

        foreach (var returnStatement in declaration.Body.DescendantNodes().OfType<ReturnStatementSyntax>())
        {
            var nestedExecutable = returnStatement.Ancestors()
                .TakeWhile(_ => !ReferenceEquals(_, declaration))
                .Any(_ => _ is LocalFunctionStatementSyntax or AnonymousFunctionExpressionSyntax);
            if (!nestedExecutable && returnStatement.Expression is { } expression)
            {
                yield return expression;
            }
        }
    }

    static ExpressionSyntax UnwrapReturnedExpression(ExpressionSyntax expression, SemanticModel semanticModel)
    {
        while (true)
        {
            expression = expression switch
            {
                ParenthesizedExpressionSyntax parenthesized => parenthesized.Expression,
                AwaitExpressionSyntax awaited => awaited.Expression,
                CastExpressionSyntax cast => cast.Expression,
                _ => expression
            };

            if (expression is InvocationExpressionSyntax invocation &&
                semanticModel.GetSymbolInfo(invocation).Symbol is IMethodSymbol taskFactory &&
                string.Equals(taskFactory.Name, "FromResult", StringComparison.Ordinal) &&
                taskFactory.ContainingType is { } containingType &&
                DotNetSubjectIds.MetadataName(containingType.OriginalDefinition) is { } taskType &&
                (string.Equals(taskType, "System.Threading.Tasks.Task", StringComparison.Ordinal) ||
                 string.Equals(taskType, "System.Threading.Tasks.ValueTask", StringComparison.Ordinal)) &&
                invocation.ArgumentList.Arguments.FirstOrDefault()?.Expression is { } result)
            {
                expression = result;
                continue;
            }

            return expression;
        }
    }

    static IMethodSymbol? StorageFactoryMethod(
        ExpressionSyntax expression,
        SemanticModel semanticModel,
        HashSet<ILocalSymbol> visitedLocals)
    {
        expression = UnwrapReturnedExpression(expression, semanticModel);
        if (expression is InvocationExpressionSyntax invocation &&
            semanticModel.GetSymbolInfo(invocation).Symbol is IMethodSymbol invoked &&
            invoked.IsStatic &&
            DotNetSubjectIds.MetadataName(invoked.ContainingType.OriginalDefinition) == WellKnownTypes.WolverineStorageFactory)
        {
            return invoked;
        }

        if (semanticModel.GetSymbolInfo(expression).Symbol is not ILocalSymbol local ||
            !visitedLocals.Add(local) ||
            local.DeclaringSyntaxReferences
                .Select(_ => _.GetSyntax())
                .OfType<VariableDeclaratorSyntax>()
                .Select(_ => _.Initializer?.Value)
                .OfType<ExpressionSyntax>()
                .FirstOrDefault() is not { } initializer)
        {
            return null;
        }

        return StorageFactoryMethod(initializer, semanticModel, visitedLocals);
    }

    static bool IsCascadeConsequence(WolverineReturnConsequence consequence) =>
        consequence.Kind == WolverineReturnConsequenceKind.Cascade &&
        consequence.Type is INamedTypeSymbol messageType &&
        IsEventPayloadType(messageType);

    static void AddMessageRelationship(
        DotNetProjectCompilation project,
        CritterStackSubjectResolver subjects,
        SubjectId sourceSubject,
        INamedTypeSymbol messageType,
        Evidence evidence,
        RelationshipKind relationshipKind,
        string relationshipId,
        string discriminator,
        List<GenerationFact> facts,
        bool sagaAnalysis)
    {
        if (WolverineSagaTypes.IsSagaState(messageType, project))
        {
            return;
        }

        var messageSubject = subjects.SubjectForType(project, messageType);
        facts.Add(Artifact(
            $"wolverine:message:{messageSubject.Value}",
            new ArtifactKey { Subject = messageSubject, Kind = ArtifactKind.Message },
            messageType.Name,
            SourceFileOf(messageType, project),
            sagaAnalysis ? AuthoredMessageProperties(messageType, project) : DotNetTypeShapes.PropertiesOf(messageType),
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
        CritterStackSubjectResolver subjects,
        SubjectId commandSubject,
        IMethodSymbol method,
        Evidence evidence,
        List<GenerationFact> facts)
    {
        foreach (var documentType in DocumentDeletes(method, project))
        {
            var documentSubject = subjects.SubjectForType(project, documentType);
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
        MessageElementType(_.Type) is not null &&
        !IsAggregateParameter(_) &&
        !IsPersistenceBoundParameter(_) &&
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

    static bool IsPersistenceBoundParameter(IParameterSymbol parameter) =>
        IsEntityParameter(parameter) ||
        DotNetSymbols.HasAttributeAssignableTo(parameter, WellKnownTypes.WolverineFirstOrDefaultAttribute) ||
        DotNetSymbols.HasAttributeAssignableTo(parameter, WellKnownTypes.WolverineQueryableAttribute);

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
                        !WolverineSagaTypes.IsSagaState(eventType, project) &&
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
                    !IsEventPayloadType(eventType) ||
                    WolverineSagaTypes.IsSagaState(eventType, project))
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
                        IsEventPayloadType(eventType) &&
                        !WolverineSagaTypes.IsSagaState(eventType, project))
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

    static INamedTypeSymbol? MessageElementType(ITypeSymbol type) => type switch
    {
        IArrayTypeSymbol { ElementType: INamedTypeSymbol element } when IsSourceType(element) => element,
        INamedTypeSymbol named when IsSourceType(named) => named,
        _ => null
    };

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

    static Evidence MethodEvidence(
        IMethodSymbol method,
        DotNetProjectCompilation project,
        AdapterIdentity adapter,
        EvidenceStrength strength,
        string explanation) =>
        CritterStackSource.EvidenceFor(method, adapter, project, strength, explanation);

    static SubjectId MethodSubject(DotNetProjectCompilation project, IMethodSymbol method, string role) => new()
    {
        Value = $"{DotNetMethodIdentity.SubjectFor(project, method).Value}:{role}"
    };

    static string? SourceFileOf(ISymbol symbol, DotNetProjectCompilation project) =>
        CritterStackSource.EvidenceFor(symbol, new AdapterIdentity { Id = "source", Version = "1" }, project, EvidenceStrength.Exact).Source?.Path;

    static string LowerFirst(string value) => value.Length == 0
        ? value
        : $"{char.ToLowerInvariant(value[0])}{value[1..]}";
}
