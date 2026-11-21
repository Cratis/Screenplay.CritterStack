// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Cratis.Screenplay.Generation;
using Cratis.Screenplay.Generation.DotNet;
using Microsoft.CodeAnalysis;

namespace Cratis.CritterStack.Screenplay.Wolverine;

internal static partial class WolverineFacts
{
    // An OnException hook only sends its messages when the handler fails. The messages keep their association with the
    // owning handler through a failure discriminator, so they never read as an ordinary success cascade, and the exception
    // filter and retry conditions the shared model cannot express are reported on the hook.
    static void AddFailureStage(
        WolverineFactScope scope,
        SubjectId sourceSubject,
        string entryPointName,
        WolverineCompoundStage stage)
    {
        var exceptionType = WolverineCompoundStages.ExceptionParameter(stage.Method)!.Type;
        var evidence = MethodEvidence(
            stage.Method,
            scope.Project,
            scope.Adapter,
            EvidenceStrength.Exact,
            $"Wolverine failure stage for '{entryPointName}' when '{exceptionType.Name}' is thrown");
        var emission = EmissionFor(scope, sourceSubject, evidence);
        var consequences = WolverineReturnConsequences.Classify(
            stage.Method,
            scope.Project,
            isHttpEndpoint: false,
            aggregateWorkflow: false,
            hasEventStream: false);
        var messages = consequences
            .Where(_ => IsCascadeConsequence(_) && !IsCompoundStageControl(_.Type))
            .Select(_ => ((INamedTypeSymbol)_.Type, (int?)_.Slot))
            .Concat(DiscoverOutgoingMessages(stage.Method, scope.Project).Select(_ => (_.MessageType, (int?)null)))
            .ToList();
        foreach (var (messageType, returnSlot) in messages)
        {
            AddFailureCascade(emission, exceptionType, messageType, returnSlot);
        }

        var messageNames = messages.Select(_ => $"'{_.Item1.Name}'").Distinct(StringComparer.Ordinal).ToList();
        scope.Diagnostics.Add(new()
        {
            Code = WolverineDiagnosticCodes.FailureStageConditionOmitted,
            Severity = GenerationDiagnosticSeverity.Warning,
            Outcome = GenerationDiagnosticOutcome.Unsupported,
            Message = $"Failure stage '{stage.Method.Name}' handles '{exceptionType.Name}' for entry point '{entryPointName}'; " +
                      (messageNames.Count == 0 ? "it sends no recognized messages" : $"{string.Join(", ", messageNames)} {(messageNames.Count == 1 ? "is" : "are")} sent only on failure") +
                      ", and the exception filter and retry conditions are not represented",
            Source = evidence.Source,
            Subject = sourceSubject
        });
    }

    static void AddFailureCascade(
        MessageEmission emission,
        ITypeSymbol exceptionType,
        INamedTypeSymbol messageType,
        int? returnSlot)
    {
        var messageSubject = emission.Subjects.SubjectForType(emission.Project, messageType);
        var failure = $"{emission.SourceSubject.Value}:{exceptionType.ToDisplayString()}";
        var relationshipId = returnSlot is null
            ? $"wolverine:cascades:failure:{failure}:{messageSubject.Value}"
            : $"wolverine:cascades:failure:return:{failure}:{returnSlot}:{messageSubject.Value}";
        AddMessageRelationship(emission, messageType, RelationshipKind.Cascades, relationshipId, $"failure:{exceptionType.ToDisplayString()}");
    }
}
