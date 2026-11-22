// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Cratis.Screenplay.Generation;
using Cratis.Screenplay.Generation.DotNet;
using Microsoft.CodeAnalysis;

namespace Cratis.CritterStack.Screenplay.Wolverine;

internal static partial class WolverineFacts
{
    static void AnalyzeHandler(
        WolverineFactScope scope,
        IMethodSymbol method,
        WolverineValidationAuthorizationDiscoveryResult validationAuthorization)
    {
        var request = RequestParameter(method, scope.Project);
        var requestType = request is not null ? MessageElementType(request.Type) : null;
        if (requestType is null || WolverineSagaTypes.IsSagaState(requestType, scope.Project))
        {
            return;
        }

        var handler = new HandlerCommand
        {
            Method = method,
            RequestType = requestType,
            Batched = request!.Type is IArrayTypeSymbol,
            Shape = DiscoverStateChangeShape(method, request, requestType, scope, isHttpEndpoint: false)
        };
        var compoundStages = WolverineCompoundStages.StagesFor(method, requestType, scope);
        if (HasNoPersistenceEffects(handler.Shape, scope.Project))
        {
            AnalyzeNonPersistingHandler(scope, handler, compoundStages, validationAuthorization);
            return;
        }

        AnalyzePersistingHandler(scope, handler, compoundStages, validationAuthorization);
    }

    static bool HasNoPersistenceEffects(StateChangeShape shape, DotNetProjectCompilation project) =>
        HasNoProducedEvents(shape) &&
        !DocumentDeletes(shape.Method, project).Any() &&
        !shape.AppendDiscovery.HasDirectWrite &&
        !shape.StreamBindings.Any(_ => _.LoadsModel) &&
        !shape.ReturnConsequences.Any(_ => _.Kind == WolverineReturnConsequenceKind.StorageAction) &&
        !shape.Method.Parameters.Any(IsPersistenceBoundParameter) &&
        shape.Dcb is null;

    static bool HasNoProducedEvents(StateChangeShape shape) =>
        shape.BodyEvents.Length == 0 &&
        shape.ReturnEvents.Count == 0 &&
        shape.SessionAppends.Count == 0 &&
        shape.Yields.Count == 0;

    static void AnalyzeNonPersistingHandler(
        WolverineFactScope scope,
        HandlerCommand handler,
        IReadOnlyList<WolverineCompoundStage> compoundStages,
        WolverineValidationAuthorizationDiscoveryResult validationAuthorization)
    {
        var method = handler.Method;
        if (!HasMartenOperations(scope, method))
        {
            scope = scope.WithOwnership(BackendOwnership.Legacy);
        }
        else if (!scope.RequireOwnership(method))
        {
            return;
        }

        var hasDocumentPersistence = HasDocumentPersistence(method, scope.Project);
        var automationReturns = hasDocumentPersistence ? [] : handler.Shape.ReturnConsequences;
        var automationOutgoingMessages = hasDocumentPersistence ? [] : handler.Shape.OutgoingMessages;
        var busConsequences = handler.Shape.BusConsequences;
        var automation = new AutomationHandler
        {
            Method = method,
            RequestType = handler.RequestType,
            Batched = handler.Batched,
            CompoundStages = compoundStages,
            ReturnConsequences = automationReturns,
            OutgoingMessages = automationOutgoingMessages,
            BusConsequences = busConsequences
        };
        if (!HasReadModelHandlerConsequences(scope, method, HasAutomationConsequences(automation)))
        {
            AddHandlerChainConfigurationDiagnostics(scope, MethodSubject(scope.Project, method, "handler"), method);
            return;
        }

        AnalyzeAutomation(scope, automation, validationAuthorization);
    }

    static bool HasAutomationConsequences(AutomationHandler handler) =>
        handler.Batched ||
        handler.CompoundStages.Count > 0 ||
        handler.BusConsequences.Count > 0 ||
        handler.ReturnConsequences.Any(IsCascadeConsequence) ||
        handler.OutgoingMessages.Count > 0;

    static bool IsLegacyPersistingShape(WolverineFactScope scope, IMethodSymbol method, StateChangeShape shape) =>
        !HasEventSourcingShape(shape) &&
        !shape.ReturnConsequences.Any(consequence => consequence.MartenOperation is not null) &&
        !HasMartenOperations(scope, method);

    static bool HasEventSourcingShape(StateChangeShape shape) =>
        shape.AggregateWorkflow ||
        shape.Dcb is not null ||
        shape.StreamBindings.Count > 0 ||
        shape.SessionAppends.Count > 0 ||
        shape.BodyEvents.Length > 0;

    static void AnalyzePersistingHandler(
        WolverineFactScope scope,
        HandlerCommand handler,
        IReadOnlyList<WolverineCompoundStage> compoundStages,
        WolverineValidationAuthorizationDiscoveryResult validationAuthorization)
    {
        var project = scope.Project;
        var method = handler.Method;
        if (IsLegacyPersistingShape(scope, method, handler.Shape))
        {
            scope = scope.WithOwnership(BackendOwnership.Legacy);
        }

        if (!scope.RequireOwnership(method))
        {
            return;
        }

        var requestType = handler.RequestType;
        var shape = handler.Shape;
        var commandSubject = scope.Subjects.SubjectForType(project, requestType);
        scope.Diagnostics.AddRange(validationAuthorization.ValidationDiagnostics(
            method,
            requestType,
            commandSubject,
            isHttpEndpoint: false));
        var evidenceExplanation = $"Wolverine message handler with persistence effects{(handler.Batched ? " (batched: Wolverine delivers arrays of this message)" : string.Empty)}";
        var evidence = MethodEvidence(method, project, scope.Adapter, EvidenceStrength.Exact, evidenceExplanation);
        var productions = WolverineProductionPlan.Decide(
            shape.ToProductionSources(requestType.Name, commandSubject, evidence.Source, hasCompoundValidation: HasShortCircuitingStage(compoundStages)),
            scope.Diagnostics);
        var feature = StateFeature(requestType.Name, shape.AggregateType, shape.StreamBindings, shape.Dcb?.ModelType);
        var compatibilityPlacement = CritterStackSourcePlacement.CompatibilityPlacement(
            project,
            scope.Options,
            feature,
            requestType.Name,
            GenerationSliceKind.StateChange);

        AddHandlerCommandFacts(scope, new(handler, commandSubject, productions, compatibilityPlacement, evidence));
        if (shape.AggregateType is { } aggregateType)
        {
            AddReadModelAndRelationship(scope, commandSubject, requestType, aggregateType, evidence);
        }

        AddStateReadFacts(scope, commandSubject, requestType.Name, shape, isHttpEndpoint: false);
        AddProductionFacts(scope, commandSubject, commandSubject, productions, shape, compatibilityPlacement, evidence);
        AddEffectFacts(scope, commandSubject, shape, evidence);
        AddResolvedCompoundStageConsequences(scope, commandSubject, method, compoundStages);
        AddHandlerChainConfigurationDiagnostics(scope, commandSubject, method);
    }

    static void AddHandlerCommandFacts(WolverineFactScope scope, HandlerCommandPlan plan)
    {
        var (handler, commandSubject, productions, compatibilityPlacement, evidence) = plan;
        var shape = handler.Shape;
        var key = scope.Key(commandSubject, ArtifactKind.Command);
        scope.Facts.Add(Artifact(
            $"wolverine:command:{commandSubject.Value}",
            key,
            handler.RequestType.Name,
            evidence.Source?.Path,
            CommandProperties(handler.RequestType, shape, productions),
            evidence));
        scope.Placements.Add(new(
            scope.PlacementId($"wolverine:placement:command:{commandSubject.Value}"),
            key,
            null,
            compatibilityPlacement,
            evidence));
    }

    static void AnalyzeAutomation(
        WolverineFactScope scope,
        AutomationHandler handler,
        WolverineValidationAuthorizationDiscoveryResult validationAuthorization)
    {
        var project = scope.Project;
        var method = handler.Method;
        var requestType = handler.RequestType;
        var requestSubject = scope.Subjects.SubjectForType(project, requestType);
        var reactionSubject = MethodSubject(project, method, "reaction");
        scope.Diagnostics.AddRange(validationAuthorization.ValidationDiagnostics(
            method,
            requestType,
            reactionSubject,
            isHttpEndpoint: false));
        var evidenceExplanation = $"Wolverine message handler with direct bus or return automation consequences{(handler.Batched ? " (batched: Wolverine delivers arrays of this message)" : string.Empty)}";
        var evidence = MethodEvidence(method, project, scope.Adapter, EvidenceStrength.Exact, evidenceExplanation);
        var reactionName = method.ContainingType.Name.EndsWith("Handler", StringComparison.Ordinal)
            ? method.ContainingType.Name[..^"Handler".Length]
            : method.ContainingType.Name;

        AddReactionFacts(scope, handler, new(requestSubject, reactionSubject, reactionName), evidence);
        AddReadModelParameterFacts(scope, reactionSubject, method);
        var emission = EmissionFor(scope, reactionSubject, evidence);
        AddReturnConsequences(emission, handler.ReturnConsequences);
        AddOutgoingMessages(emission, method, handler.OutgoingMessages, scope.Diagnostics);
        AddDirectBusConsequences(emission, method, scope.Diagnostics, handler.BusConsequences);
        AddResolvedCompoundStageConsequences(scope, reactionSubject, method, handler.CompoundStages);
        AddHandlerChainConfigurationDiagnostics(scope, reactionSubject, method);
    }

    static void AddReactionFacts(
        WolverineFactScope scope,
        AutomationHandler handler,
        ReactionIdentity identity,
        Evidence evidence)
    {
        var (requestSubject, reactionSubject, reactionName) = identity;
        var project = scope.Project;
        var requestType = handler.RequestType;
        var compatibilityPlacement = CritterStackSourcePlacement.CompatibilityPlacement(
            project,
            scope.Options,
            requestType.Name,
            reactionName,
            GenerationSliceKind.Automation);
        var requestKey = scope.Key(requestSubject, ArtifactKind.Message);
        var reactionKey = scope.Key(reactionSubject, ArtifactKind.Reaction);
        scope.Facts.Add(Artifact(
            $"wolverine:message:{requestSubject.Value}",
            requestKey,
            requestType.Name,
            SourceFileOf(requestType, project),
            DotNetTypeShapes.PropertiesOf(requestType),
            evidence));
        scope.Facts.Add(Artifact(
            $"wolverine:reaction:{reactionSubject.Value}",
            reactionKey,
            reactionName,
            evidence.Source?.Path,
            [],
            evidence));
        scope.Placements.Add(new(
            scope.PlacementId($"wolverine:placement:reaction:{reactionSubject.Value}"),
            reactionKey,
            scope.Subjects.SubjectForType(project, handler.Method.ContainingType),
            compatibilityPlacement,
            evidence));
        scope.Facts.Add(Relationship(
            scope,
            $"wolverine:handles:{reactionSubject.Value}:{requestSubject.Value}",
            reactionSubject,
            RelationshipKind.Handles,
            requestSubject,
            evidence,
            isCollection: handler.Batched));
    }

    static bool IsHandler(
        INamedTypeSymbol type,
        IMethodSymbol method,
        WolverineHandlerDiscoveryPolicy discovery)
    {
        if (!IsHandlerCandidateType(type, discovery))
        {
            return false;
        }

        var explicitMethod = DotNetSymbols.HasAttribute(method, WellKnownTypes.WolverineHandlerAttribute) ||
                             DotNetSymbols.HasAttribute(method, WellKnownTypes.WolverineLegacyHandlerAttribute);
        return explicitMethod || _handlerMethodNames.Contains(method.Name);
    }

    static bool IsHandlerCandidateType(INamedTypeSymbol type, WolverineHandlerDiscoveryPolicy discovery) =>
        !type.IsGenericType && !(type.IsAbstract && !type.IsStatic) && discovery.Includes(type);

    sealed record HandlerCommandPlan(
        HandlerCommand Handler,
        SubjectId CommandSubject,
        IReadOnlyList<WolverineProductionDecision> Productions,
        ArtifactPlacement CompatibilityPlacement,
        Evidence Evidence);

    sealed record ReactionIdentity(SubjectId RequestSubject, SubjectId ReactionSubject, string ReactionName);

    sealed class HandlerCommand
    {
        public required IMethodSymbol Method { get; init; }

        public required INamedTypeSymbol RequestType { get; init; }

        public required bool Batched { get; init; }

        public required StateChangeShape Shape { get; init; }
    }

    sealed class AutomationHandler
    {
        public required IMethodSymbol Method { get; init; }

        public required INamedTypeSymbol RequestType { get; init; }

        public required bool Batched { get; init; }

        public required IReadOnlyList<WolverineCompoundStage> CompoundStages { get; init; }

        public required IReadOnlyList<WolverineReturnConsequence> ReturnConsequences { get; init; }

        public required List<WolverineOutgoingMessageConsequence> OutgoingMessages { get; init; }

        public required IReadOnlyList<WolverineBusConsequence> BusConsequences { get; init; }
    }
}
