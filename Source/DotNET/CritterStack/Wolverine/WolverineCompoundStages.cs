// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Cratis.Screenplay.Generation.DotNet;
using Microsoft.CodeAnalysis;

namespace Cratis.CritterStack.Screenplay.Wolverine;

sealed record WolverineCompoundStage(IMethodSymbol Method, string StageKind);

static class WolverineCompoundStages
{
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

    static readonly HashSet<string> _afterMethodNames =
    [
        "After",
        "AfterAsync",
        "PostProcess",
        "PostProcessAsync",
        "Finally",
        "FinallyAsync"
    ];

    static readonly HashSet<string> _afterCommitMethodNames = ["AfterCommit", "AfterCommitAsync"];

    public static IReadOnlyList<IMethodSymbol> ValidationMethodsFor(
        IMethodSymbol handler,
        DotNetProjectCompilation project) =>
        [.. CandidateMethods(handler, project).Where(_ =>
            (string.Equals(_.Name, "Validate", StringComparison.Ordinal) ||
             string.Equals(_.Name, "ValidateAsync", StringComparison.Ordinal)) &&
            MatchesValidationParameters(handler, _))];

    public static IReadOnlyList<WolverineCompoundStage> StagesFor(
        IMethodSymbol handler,
        ITypeSymbol requestType,
        DotNetProjectCompilation project) =>
        [
            .. CandidateMethods(handler, project)
                .Where(_ =>
                    !_handlerMethodNames.Contains(_.Name) &&
                    !string.Equals(_.Name, "Validate", StringComparison.Ordinal) &&
                    !string.Equals(_.Name, "ValidateAsync", StringComparison.Ordinal))
                .Select(StageFor)
                .OfType<WolverineCompoundStage>()
                .Where(stage => MatchesHandler(handler, requestType, stage))
        ];

    static WolverineCompoundStage? StageFor(IMethodSymbol method)
    {
        if (string.Equals(method.Name, "Load", StringComparison.Ordinal) ||
            string.Equals(method.Name, "LoadAsync", StringComparison.Ordinal))
        {
            return new(method, "load");
        }

        if (string.Equals(method.Name, "Before", StringComparison.Ordinal) ||
            string.Equals(method.Name, "BeforeAsync", StringComparison.Ordinal))
        {
            return new(method, "before");
        }

        if (_afterMethodNames.Contains(method.Name))
        {
            return new(method, "after");
        }

        if (_afterCommitMethodNames.Contains(method.Name) ||
            DotNetSymbols.HasAttributeAssignableTo(method, WellKnownTypes.WolverineAfterCommitAttribute))
        {
            return new(method, "after-commit");
        }

        return null;
    }

    static bool MatchesHandler(
        IMethodSymbol handler,
        ITypeSymbol requestType,
        WolverineCompoundStage stage)
    {
        if (!string.Equals(stage.StageKind, "load", StringComparison.Ordinal))
        {
            return stage.Method.Parameters.FirstOrDefault()?.Type is { } stageRequestType &&
                   SymbolEqualityComparer.Default.Equals(stageRequestType, requestType);
        }

        var handlerParameterTypes = handler.Parameters.Select(_ => _.Type).ToArray();
        var consumesHandlerData = stage.Method.Parameters.Any(parameter =>
            handlerParameterTypes.Any(handlerType => SymbolEqualityComparer.Default.Equals(handlerType, parameter.Type)));
        var suppliesHandlerData = WolverineReturnTypes.CreatedValues(stage.Method).Any(returnType =>
            handlerParameterTypes.Any(handlerType => SymbolEqualityComparer.Default.Equals(handlerType, returnType)));

        return consumesHandlerData && suppliesHandlerData;
    }

    static bool MatchesValidationParameters(IMethodSymbol handler, IMethodSymbol validation)
    {
        if (validation.Parameters.Length == 0)
        {
            return true;
        }

        var handlerParameterTypes = handler.Parameters.Select(_ => _.Type).ToArray();
        return validation.Parameters.Any(parameter =>
            handlerParameterTypes.Any(handlerType => SymbolEqualityComparer.Default.Equals(handlerType, parameter.Type)));
    }

    static IEnumerable<IMethodSymbol> CandidateMethods(
        IMethodSymbol handler,
        DotNetProjectCompilation project) =>
        handler.ContainingType.GetMembers()
            .OfType<IMethodSymbol>()
            .Where(_ =>
                !SymbolEqualityComparer.Default.Equals(_, handler) &&
                _.DeclaredAccessibility == Accessibility.Public &&
                _.Locations.Any(location => IsAuthoredSourceLocation(location, project)))
            .OrderBy(_ => _.Locations.First(location => IsAuthoredSourceLocation(location, project)).SourceSpan.Start);

    static bool IsAuthoredSourceLocation(Location location, DotNetProjectCompilation project) => location is
    {
        IsInSource: true,
        SourceTree: not null
    } && project.AuthoredSyntaxTrees.Contains(location.SourceTree) &&
         !DotNetGeneratedSource.IsGenerated(location.SourceTree);
}
