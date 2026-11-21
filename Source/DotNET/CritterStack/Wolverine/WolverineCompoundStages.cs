// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Cratis.Screenplay.Generation;
using Cratis.Screenplay.Generation.DotNet;
using Microsoft.CodeAnalysis;

namespace Cratis.CritterStack.Screenplay.Wolverine;

sealed record WolverineCompoundStage(IMethodSymbol Method, string StageKind, SourceRange? Source = null, bool IsExplicit = false)
{
    public const string OnException = "on-exception";

    public bool IsFailurePath => string.Equals(StageKind, OnException, StringComparison.Ordinal);
}

static partial class WolverineCompoundStages
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
        [.. CandidateMethods(handler.ContainingType, project).Where(_ =>
            !SymbolEqualityComparer.Default.Equals(_, handler) &&
            (string.Equals(_.Name, "Validate", StringComparison.Ordinal) ||
             string.Equals(_.Name, "ValidateAsync", StringComparison.Ordinal)) &&
            MatchesValidationParameters(handler, _))];

    public static IReadOnlyList<WolverineCompoundStage> StagesFor(
        IMethodSymbol handler,
        ITypeSymbol requestType,
        DotNetProjectCompilation project,
        bool isHttpEndpoint = false)
    {
        var stages = CandidatesFor(handler.ContainingType, project, isHttpEndpoint).ToList();
        foreach (var application in WolverinePolicyDiscovery.MiddlewareFor(handler, project, isHttpEndpoint))
        {
            stages.AddRange(CandidatesFor(application.Type, project, isHttpEndpoint)
                .Select(_ => _ with { Source = application.Source, IsExplicit = true }));
        }

        var seen = new HashSet<IMethodSymbol>(SymbolEqualityComparer.Default);
        return [.. stages.Where(_ => !SymbolEqualityComparer.Default.Equals(_.Method, handler) &&
            MatchesHandler(handler, requestType, _) && seen.Add(_.Method))];
    }

    static IEnumerable<WolverineCompoundStage> CandidatesFor(
        INamedTypeSymbol type,
        DotNetProjectCompilation project,
        bool isHttpEndpoint) =>
        CandidateMethods(type, project)
            .Where(_ => !_handlerMethodNames.Contains(_.Name) && _.Name is not "Validate" and not "ValidateAsync")
            .Select(_ => StageFor(_, project, isHttpEndpoint))
            .OfType<WolverineCompoundStage>();

    /// <summary>
    /// Gets the parameter through which a failure stage receives the exception it handles.
    /// </summary>
    /// <param name="method">The failure stage method.</param>
    /// <returns>The exception parameter, or null when the method does not accept an exception.</returns>
    public static IParameterSymbol? ExceptionParameter(IMethodSymbol method) =>
        method.Parameters.FirstOrDefault(_ => IsException(_.Type));

    static WolverineCompoundStage? StageFor(IMethodSymbol method, DotNetProjectCompilation project, bool isHttpEndpoint)
    {
        if (_onExceptionMethodNames.Contains(method.Name) && ExceptionParameter(method) is not null)
        {
            return new(method, WolverineCompoundStage.OnException);
        }

        var attribute = method.GetAttributes().FirstOrDefault(_ => AttributeStageKind(_) is not null);
        if (attribute is not null)
        {
            return AttributeStageFor(method, attribute, project, isHttpEndpoint);
        }

        if (_namedStages.FirstOrDefault(_ => _.Names.Contains(method.Name)).StageKind is { } stageKind)
        {
            return new(method, stageKind);
        }

        if (DotNetSymbols.HasAttributeAssignableTo(method, WellKnownTypes.WolverineAfterCommitAttribute))
        {
            return new(method, "after-commit");
        }

        return null;
    }

    static WolverineCompoundStage? AttributeStageFor(
        IMethodSymbol method,
        AttributeData attribute,
        DotNetProjectCompilation project,
        bool isHttpEndpoint) => WolverinePolicyDiscovery.AppliesTo(attribute, isHttpEndpoint)
            ? new(method, AttributeStageKind(attribute)!, WolverinePolicyDiscovery.AttributeSource(attribute, project), IsExplicit: true)
            : null;

    static string? AttributeStageKind(AttributeData attribute) => attribute.AttributeClass is { } type
        ? DotNetSubjectIds.MetadataName(type) switch
        {
            WellKnownTypes.WolverineBeforeAttribute => "before",
            WellKnownTypes.WolverineAfterAttribute or WellKnownTypes.WolverineFinallyAttribute => "after",
            WellKnownTypes.WolverineAfterCommitAttribute => "after-commit",
            _ => null
        }
        : null;

    static IEnumerable<IMethodSymbol> CandidateMethods(
        INamedTypeSymbol type,
        DotNetProjectCompilation project) =>
        type.GetMembers()
            .OfType<IMethodSymbol>()
            .Where(_ =>
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
