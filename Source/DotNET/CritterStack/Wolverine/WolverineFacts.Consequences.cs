// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Cratis.Screenplay.Generation;
using Cratis.Screenplay.Generation.DotNet;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Cratis.CritterStack.Screenplay.Wolverine;

internal static partial class WolverineFacts
{
    static void AddDirectBusConsequences(
        MessageEmission emission,
        IMethodSymbol method,
        List<GenerationDiagnostic> diagnostics,
        IReadOnlyList<WolverineBusConsequence>? discovered = null)
    {
        foreach (var consequence in discovered ?? WolverineBusConsequences.Discover(method, emission.Project))
        {
            if (!IsPublishedMessage(consequence, emission.Project))
            {
                continue;
            }

            var messageType = (INamedTypeSymbol)consequence.MessageType;
            var messageSubject = emission.Subjects.SubjectForType(emission.Project, messageType);
            AddMessageRelationship(
                emission,
                messageType,
                RelationshipKind.Publishes,
                $"wolverine:publishes:{consequence.Discriminator}:{emission.SourceSubject.Value}:{messageSubject.Value}",
                consequence.Discriminator);
            diagnostics.Add(DirectBusDiagnostic(emission, method, consequence, messageType));
        }
    }

    static bool IsPublishedMessage(WolverineBusConsequence consequence, DotNetProjectCompilation project) =>
        consequence.MessageType is INamedTypeSymbol messageType &&
        IsEventPayloadType(messageType) &&
        !WolverineSagaTypes.IsSagaState(messageType, project);

    static GenerationDiagnostic DirectBusDiagnostic(
        MessageEmission emission,
        IMethodSymbol method,
        WolverineBusConsequence consequence,
        INamedTypeSymbol messageType) => new()
        {
            Code = consequence.IsScheduled
                ? WolverineDiagnosticCodes.DelayedMessageOmitted
                : WolverineDiagnosticCodes.DirectMessageDeliveryOmitted,
            Severity = GenerationDiagnosticSeverity.Warning,
            Outcome = GenerationDiagnosticOutcome.Unsupported,
            Message = consequence.IsScheduled
                ? $"Handler '{method.ContainingType.Name}.{method.Name}' schedules '{messageType.Name}', which the current Screenplay language cannot represent"
                : $"Handler '{method.ContainingType.Name}.{method.Name}' performs Wolverine {consequence.Discriminator} delivery of '{messageType.Name}', which the current Screenplay language cannot represent",
            Source = emission.Evidence.Source,
            Subject = emission.SourceSubject
        };

    static List<WolverineOutgoingMessageConsequence> DiscoverOutgoingMessages(
        IMethodSymbol method,
        DotNetProjectCompilation project) =>
    [
        .. WolverineMethodSyntax.Declarations(method, project)
            .SelectMany(_ => OutgoingMessageElements(_.Declaration, _.SemanticModel)
                .Select(element => OutgoingMessage(element, _.SemanticModel, project)))
            .OfType<WolverineOutgoingMessageConsequence>()
    ];

    static IEnumerable<ExpressionElementSyntax> OutgoingMessageElements(MethodDeclarationSyntax declaration, SemanticModel semanticModel) =>
        declaration.DescendantNodes()
            .OfType<CollectionExpressionSyntax>()
            .Where(collection => IsOutgoingMessagesCollection(semanticModel.GetTypeInfo(collection).ConvertedType))
            .SelectMany(collection => collection.Elements.OfType<ExpressionElementSyntax>());

    static bool IsOutgoingMessagesCollection(ITypeSymbol? type) =>
        type is INamedTypeSymbol collectionType &&
        DotNetSubjectIds.MetadataName(collectionType.OriginalDefinition) == WellKnownTypes.WolverineOutgoingMessages;

    static WolverineOutgoingMessageConsequence? OutgoingMessage(
        ExpressionElementSyntax element,
        SemanticModel semanticModel,
        DotNetProjectCompilation project)
    {
        if (semanticModel.GetTypeInfo(element.Expression).Type is not INamedTypeSymbol elementType)
        {
            return null;
        }

        var messageType = elementType.Name == "DeliveryMessage" && elementType.TypeArguments.FirstOrDefault() is INamedTypeSymbol delivered
            ? delivered
            : elementType;
        if (!IsEventPayloadType(messageType) || WolverineSagaTypes.IsSagaState(messageType, project))
        {
            return null;
        }

        var delayed = element.Expression.DescendantNodesAndSelf()
            .OfType<InvocationExpressionSyntax>()
            .Any(_ => _.Expression.ToString().Contains("Delayed", StringComparison.Ordinal));
        return new(messageType, delayed);
    }

    static void AddOutgoingMessages(
        MessageEmission emission,
        IMethodSymbol method,
        IReadOnlyList<WolverineOutgoingMessageConsequence> consequences,
        List<GenerationDiagnostic> diagnostics)
    {
        foreach (var consequence in consequences)
        {
            var messageSubject = emission.Subjects.SubjectForType(emission.Project, consequence.MessageType);
            AddMessageRelationship(
                emission,
                consequence.MessageType,
                RelationshipKind.Cascades,
                $"wolverine:cascades:{emission.SourceSubject.Value}:{messageSubject.Value}",
                consequence.Delayed ? "delayed" : "immediate");

            if (consequence.Delayed)
            {
                diagnostics.Add(new GenerationDiagnostic
                {
                    Code = WolverineDiagnosticCodes.DelayedMessageOmitted,
                    Severity = GenerationDiagnosticSeverity.Warning,
                    Outcome = GenerationDiagnosticOutcome.Unsupported,
                    Message = $"Handler '{method.ContainingType.Name}.{method.Name}' dispatches '{consequence.MessageType.Name}' after a delay, which the current Screenplay language cannot represent",
                    Source = emission.Evidence.Source,
                    Subject = emission.SourceSubject
                });
            }
        }
    }

    static void AddReturnConsequences(
        MessageEmission emission,
        IReadOnlyList<WolverineReturnConsequence> consequences)
    {
        foreach (var consequence in consequences.Where(IsCascadeConsequence))
        {
            var messageType = (INamedTypeSymbol)consequence.Type;
            var messageSubject = emission.Subjects.SubjectForType(emission.Project, messageType);
            AddMessageRelationship(
                emission,
                messageType,
                RelationshipKind.Cascades,
                $"wolverine:cascades:return:{emission.SourceSubject.Value}:{consequence.Slot}:{messageSubject.Value}",
                $"return-slot:{consequence.Slot}");
        }
    }

    static void AddCompoundStageConsequences(
        WolverineFactScope scope,
        SubjectId sourceSubject,
        IMethodSymbol entryPoint,
        IReadOnlyList<WolverineCompoundStage> stages)
    {
        var entryPointName = DotNetMethodIdentity.DisplayName(entryPoint);
        foreach (var stage in stages)
        {
            AddCompoundStage(scope, sourceSubject, entryPointName, stage);
        }
    }

    static void AddCompoundStage(
        WolverineFactScope scope,
        SubjectId sourceSubject,
        string entryPointName,
        WolverineCompoundStage stage)
    {
        var evidence = MethodEvidence(
            stage.Method,
            scope.Project,
            scope.Adapter,
            EvidenceStrength.Exact,
            $"Wolverine compound handler {stage.StageKind} stage for '{entryPointName}'");
        var emission = EmissionFor(scope, sourceSubject, evidence);
        var consequences = WolverineReturnConsequences.Classify(
            stage.Method,
            scope.Project,
            isHttpEndpoint: false,
            aggregateWorkflow: false,
            hasEventStream: false);
        if (!string.Equals(stage.StageKind, "load", StringComparison.Ordinal))
        {
            foreach (var consequence in consequences.Where(_ => IsCascadeConsequence(_) && !IsCompoundStageControl(_.Type)))
            {
                AddStageCascade(emission, stage, (INamedTypeSymbol)consequence.Type, consequence.Slot);
            }
        }

        foreach (var outgoing in DiscoverOutgoingMessages(stage.Method, scope.Project))
        {
            AddStageCascade(emission, stage, outgoing.MessageType, returnSlot: null);
        }

        var canShortCircuit = consequences.Any(_ => IsCompoundStageControl(_.Type));
        scope.Diagnostics.Add(new()
        {
            Code = WolverineDiagnosticCodes.CompoundStageOmitted,
            Severity = GenerationDiagnosticSeverity.Information,
            Outcome = GenerationDiagnosticOutcome.Unsupported,
            Message = $"Compound handler stage '{stage.Method.Name}' ({stage.StageKind}) participates in entry point '{entryPointName}' and {(canShortCircuit ? "can short-circuit" : "does not expose recognized short-circuit control")}; its data-loading and continuation semantics are not fully represented",
            Source = evidence.Source,
            Subject = sourceSubject
        });
    }

    static void AddStageCascade(
        MessageEmission emission,
        WolverineCompoundStage stage,
        INamedTypeSymbol messageType,
        int? returnSlot)
    {
        var messageSubject = emission.Subjects.SubjectForType(emission.Project, messageType);
        var source = emission.SourceSubject.Value;
        var relationshipId = returnSlot is null
            ? $"wolverine:cascades:stage:{source}:{stage.Method.MetadataName}:{messageSubject.Value}"
            : $"wolverine:cascades:stage:return:{source}:{stage.Method.MetadataName}:{returnSlot}:{messageSubject.Value}";
        AddMessageRelationship(emission, messageType, RelationshipKind.Cascades, relationshipId, $"stage:{stage.Method.Name}");
    }

    static void AddHandlerChainConfigurationDiagnostics(
        WolverineFactScope scope,
        SubjectId entryPointSubject,
        IMethodSymbol entryPoint)
    {
        var entryPointName = DotNetMethodIdentity.DisplayName(entryPoint);
        foreach (var configure in entryPoint.ContainingType.GetMembers()
                     .OfType<IMethodSymbol>()
                     .Where(_ => IsHandlerChainConfigure(_, scope.Project)))
        {
            var evidence = MethodEvidence(
                configure,
                scope.Project,
                scope.Adapter,
                EvidenceStrength.Exact,
                $"Wolverine per-chain configuration for '{entryPointName}'");
            scope.Diagnostics.Add(new()
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

    static bool IsHandlerChainConfigure(IMethodSymbol method, DotNetProjectCompilation project) =>
        string.Equals(method.Name, "Configure", StringComparison.Ordinal) &&
        method.DeclaredAccessibility == Accessibility.Public &&
        method.IsStatic &&
        method.ReturnsVoid &&
        method.Parameters is [{ Type: INamedTypeSymbol parameterType }] &&
        DotNetSubjectIds.MetadataName(parameterType.OriginalDefinition) == WellKnownTypes.WolverineHandlerChain &&
        method.Locations.Any(location => IsAuthoredSourceLocation(location, project));

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

    static bool IsCascadeConsequence(WolverineReturnConsequence consequence) =>
        consequence.Kind == WolverineReturnConsequenceKind.Cascade &&
        consequence.Type is INamedTypeSymbol messageType &&
        IsEventPayloadType(messageType);

    static void AddMessageRelationship(
        MessageEmission emission,
        INamedTypeSymbol messageType,
        RelationshipKind relationshipKind,
        string relationshipId,
        string discriminator)
    {
        if (WolverineSagaTypes.IsSagaState(messageType, emission.Project))
        {
            return;
        }

        var messageSubject = emission.Subjects.SubjectForType(emission.Project, messageType);
        emission.Facts.Add(Artifact(
            $"wolverine:message:{messageSubject.Value}",
            new ArtifactKey { Subject = messageSubject, Kind = ArtifactKind.Message },
            messageType.Name,
            SourceFileOf(messageType, emission.Project),
            emission.SagaAnalysis ? AuthoredMessageProperties(messageType, emission.Project) : DotNetTypeShapes.PropertiesOf(messageType),
            emission.Evidence));
        emission.Facts.Add(Relationship(
            relationshipId,
            emission.SourceSubject,
            relationshipKind,
            messageSubject,
            emission.Evidence,
            discriminator: discriminator));
    }

    static void AddDocumentDeletes(MessageEmission emission, IMethodSymbol method)
    {
        foreach (var documentType in DocumentDeletes(method, emission.Project))
        {
            var documentSubject = emission.Subjects.SubjectForType(emission.Project, documentType);
            emission.Facts.Add(Relationship(
                $"wolverine:deletes:{emission.SourceSubject.Value}:{documentSubject.Value}",
                emission.SourceSubject,
                RelationshipKind.Deletes,
                documentSubject,
                emission.Evidence));
        }
    }

    static MessageEmission EmissionFor(WolverineFactScope scope, SubjectId sourceSubject, Evidence evidence) => new()
    {
        Project = scope.Project,
        Subjects = scope.Subjects,
        SourceSubject = sourceSubject,
        Evidence = evidence,
        Facts = scope.Facts
    };

    static MessageEmission SagaEmissionFor(
        DotNetProjectCompilation project,
        CritterStackSubjectResolver subjects,
        SubjectId sourceSubject,
        Evidence evidence,
        List<GenerationFact> facts) => new()
        {
            Project = project,
            Subjects = subjects,
            SourceSubject = sourceSubject,
            Evidence = evidence,
            Facts = facts,
            SagaAnalysis = true
        };

    sealed class MessageEmission
    {
        public required DotNetProjectCompilation Project { get; init; }

        public required CritterStackSubjectResolver Subjects { get; init; }

        public required SubjectId SourceSubject { get; init; }

        public required Evidence Evidence { get; init; }

        public required List<GenerationFact> Facts { get; init; }

        public bool SagaAnalysis { get; init; }
    }
}
