// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Cratis.Screenplay.Generation;
using Microsoft.CodeAnalysis;

namespace Cratis.CritterStack.Screenplay.Wolverine;

internal static partial class WolverineFacts
{
    // Generation has no failure branch: a Cascades relationship would print as an ordinary success consequence.
    // Keep the owning handler and failure-only messages in a located diagnostic instead.
    static void AddFailureStage(
        WolverineFactScope scope,
        SubjectId sourceSubject,
        string entryPointName,
        WolverineCompoundStage stage)
    {
        scope.FailureStageMethods.Add(stage.Method);
        var exceptionType = WolverineCompoundStages.ExceptionParameter(stage.Method)!.Type;
        var evidence = MethodEvidence(
            stage.Method,
            scope.Project,
            scope.Adapter,
            EvidenceStrength.Exact,
            $"Wolverine failure stage for '{entryPointName}' when '{exceptionType.Name}' is thrown");
        var consequences = WolverineReturnConsequences.Classify(
            stage.Method,
            scope.Project,
            isHttpEndpoint: false,
            aggregateWorkflow: false,
            hasEventStream: false);
        var messages = consequences
            .Where(_ => IsCompoundStageMessage(_, scope.Project))
            .Select(_ => (INamedTypeSymbol)_.Type)
            .Concat(DiscoverOutgoingMessages(stage.Method, scope.Project).Select(_ => _.MessageType))
            .Concat(WolverineBusConsequences.Discover(stage.Method, scope.Project).Select(_ => _.MessageType).OfType<INamedTypeSymbol>());
        var messageNames = messages.Select(_ => $"'{_.Name}'").Distinct(StringComparer.Ordinal).ToList();
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
}
