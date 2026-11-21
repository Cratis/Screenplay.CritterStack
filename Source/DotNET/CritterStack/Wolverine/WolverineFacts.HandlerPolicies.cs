// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Cratis.Screenplay.Generation;
using Cratis.Screenplay.Generation.DotNet;
using Microsoft.CodeAnalysis;

namespace Cratis.CritterStack.Screenplay.Wolverine;

internal static partial class WolverineFacts
{
    static bool HasShortCircuitingStage(IReadOnlyList<WolverineCompoundStage> stages) => stages.Any(_ =>
        _.StageKind is "before" or "load" && WolverineReturnTypes.CreatedValues(_.Method).Any(IsShortCircuitingResult));

    static bool IsShortCircuitingResult(ITypeSymbol type) => type is INamedTypeSymbol named &&
        (DotNetSubjectIds.MetadataName(named.OriginalDefinition) is WellKnownTypes.WolverineHandlerContinuation or WellKnownTypes.WolverineRequirementResult ||
         WolverineReturnTypes.IsHttpResponseType(named));

    static void AddResolvedCompoundStageConsequences(
        WolverineFactScope scope,
        SubjectId sourceSubject,
        IMethodSymbol entryPoint,
        IReadOnlyList<WolverineCompoundStage> stages)
    {
        foreach (var stage in stages)
        {
            var firstFact = scope.Facts.Count;
            var firstDiagnostic = scope.Diagnostics.Count;
            AddCompoundStageConsequences(scope, sourceSubject, entryPoint, [stage]);
            if (stage.Source is not { } source)
            {
                continue;
            }

            for (var index = firstFact; index < scope.Facts.Count; index++)
            {
                var fact = scope.Facts[index];
                scope.Facts[index] = fact with { Evidence = fact.Evidence with { Source = source } };
            }

            for (var index = firstDiagnostic; index < scope.Diagnostics.Count; index++)
            {
                scope.Diagnostics[index] = scope.Diagnostics[index] with { Source = source };
            }
        }
    }
}
