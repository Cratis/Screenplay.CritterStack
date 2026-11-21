// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Cratis.Screenplay.Generation.DotNet;
using Microsoft.CodeAnalysis;

namespace Cratis.CritterStack.Screenplay.Wolverine;

sealed record WolverineCompoundStage(IMethodSymbol Method, string StageKind)
{
    public const string OnException = "on-exception";

    public bool IsFailurePath => string.Equals(StageKind, OnException, StringComparison.Ordinal);
}

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

    static readonly HashSet<string> _onExceptionMethodNames = ["OnException", "OnExceptionAsync"];

    static readonly (HashSet<string> Names, string StageKind)[] _namedStages =
    [
        (["Load", "LoadAsync"], "load"),
        (["Before", "BeforeAsync"], "before"),
        (_afterMethodNames, "after"),
        (_afterCommitMethodNames, "after-commit")
    ];

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

    /// <summary>
    /// Gets the parameter through which a failure stage receives the exception it handles.
    /// </summary>
    /// <param name="method">The failure stage method.</param>
    /// <returns>The exception parameter, or null when the method does not accept an exception.</returns>
    public static IParameterSymbol? ExceptionParameter(IMethodSymbol method) =>
        method.Parameters.FirstOrDefault(_ => IsException(_.Type));

    static WolverineCompoundStage? StageFor(IMethodSymbol method)
    {
        if (_namedStages.FirstOrDefault(_ => _.Names.Contains(method.Name)).StageKind is { } stageKind)
        {
            return new(method, stageKind);
        }

        if (DotNetSymbols.HasAttributeAssignableTo(method, WellKnownTypes.WolverineAfterCommitAttribute))
        {
            return new(method, "after-commit");
        }

        if (_onExceptionMethodNames.Contains(method.Name) && ExceptionParameter(method) is not null)
        {
            return new(method, WolverineCompoundStage.OnException);
        }

        return null;
    }

    static bool IsException(ITypeSymbol type)
    {
        for (var current = type; current is not null; current = current.BaseType)
        {
            if (string.Equals(current.ToDisplayString(), "System.Exception", StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
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
