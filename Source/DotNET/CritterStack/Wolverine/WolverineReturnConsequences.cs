// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Cratis.Screenplay.Generation.DotNet;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Cratis.CritterStack.Screenplay.Wolverine;

enum WolverineReturnConsequenceKind
{
    Unknown,
    HttpResponse,
    PersistedEvent,
    PersistenceOperation,
    Cascade,
    OutgoingMessages,
    SideEffect,
    SagaState
}

sealed record WolverineReturnConsequence(int Slot, ITypeSymbol Type, WolverineReturnConsequenceKind Kind);

static class WolverineReturnConsequences
{
    static readonly HashSet<string> _persistenceOperations =
    [
        WellKnownTypes.WolverineEvents,
        WellKnownTypes.WolverineEventsToAppend,
        WellKnownTypes.WolverineStartStream
    ];

    static readonly HashSet<string> _responseContracts =
    [
        "Microsoft.AspNetCore.Http.IResult",
        "Wolverine.IResponse",
        WellKnownTypes.WolverineResponseAware,
        WellKnownTypes.WolverineLegacyResponseAware
    ];

    public static IReadOnlyList<WolverineReturnConsequence> Classify(
        IMethodSymbol method,
        DotNetProjectCompilation project,
        bool isHttpEndpoint,
        bool aggregateWorkflow,
        bool hasEventStream)
    {
        var emptyResponse = DotNetSymbols.HasAttribute(method, WellKnownTypes.WolverineEmptyResponseAttribute);
        return
        [
            .. WolverineReturnTypes.CreatedValues(method)
                .Select((type, slot) => new WolverineReturnConsequence(
                    slot,
                    type,
                    Classify(type, slot, project, isHttpEndpoint, aggregateWorkflow, hasEventStream, emptyResponse)))
        ];
    }

    public static bool IsTimeoutMessage(INamedTypeSymbol type) =>
        IsAssignableTo(type, WellKnownTypes.WolverineTimeoutMessage);

    static WolverineReturnConsequenceKind Classify(
        ITypeSymbol type,
        int slot,
        DotNetProjectCompilation project,
        bool isHttpEndpoint,
        bool aggregateWorkflow,
        bool hasEventStream,
        bool emptyResponse)
    {
        if (type is not INamedTypeSymbol named)
        {
            return WolverineReturnConsequenceKind.Unknown;
        }

        var metadataName = DotNetSubjectIds.MetadataName(named.OriginalDefinition);
        if (IsSagaState(named, project))
        {
            return WolverineReturnConsequenceKind.SagaState;
        }

        if (IsTimeoutMessage(named))
        {
            return WolverineReturnConsequenceKind.Cascade;
        }

        if (metadataName == WellKnownTypes.WolverineOutgoingMessages)
        {
            return WolverineReturnConsequenceKind.OutgoingMessages;
        }

        if (IsAssignableTo(named, WellKnownTypes.WolverineSideEffect))
        {
            return WolverineReturnConsequenceKind.SideEffect;
        }

        if (_persistenceOperations.Contains(metadataName))
        {
            return WolverineReturnConsequenceKind.PersistenceOperation;
        }

        if (IsResponse(named))
        {
            return WolverineReturnConsequenceKind.HttpResponse;
        }

        if (aggregateWorkflow && !hasEventStream && IsPayload(named))
        {
            return WolverineReturnConsequenceKind.PersistedEvent;
        }

        if (isHttpEndpoint && slot == 0 && !emptyResponse)
        {
            return WolverineReturnConsequenceKind.HttpResponse;
        }

        return IsPayload(named)
            ? WolverineReturnConsequenceKind.Cascade
            : WolverineReturnConsequenceKind.Unknown;
    }

    static bool IsResponse(INamedTypeSymbol type)
    {
        if (type.Name.StartsWith("CreationResponse", StringComparison.Ordinal) ||
            type.Name.StartsWith("UpdatedAggregate", StringComparison.Ordinal))
        {
            return true;
        }

        return _responseContracts.Any(contract => IsAssignableTo(type, contract));
    }

    static bool IsPayload(INamedTypeSymbol type) =>
        type.SpecialType == SpecialType.None &&
        !WolverineReturnTypes.IsSpecialReturn(type) &&
        !DotNetSubjectIds.MetadataName(type.OriginalDefinition).StartsWith("System.", StringComparison.Ordinal);

    static bool IsSagaState(
        INamedTypeSymbol type,
        DotNetProjectCompilation project)
    {
        if (project.Compilation.GetTypeByMetadataName(WellKnownTypes.WolverineSaga) is not { } sagaType ||
            !IsAuthoredOrMetadataSymbol(sagaType, project))
        {
            return false;
        }

        return IsAuthoredOrMetadataAssignableTo(type, sagaType, project, new HashSet<INamedTypeSymbol>(SymbolEqualityComparer.Default));
    }

    static bool IsAuthoredOrMetadataAssignableTo(
        INamedTypeSymbol type,
        INamedTypeSymbol target,
        DotNetProjectCompilation project,
        HashSet<INamedTypeSymbol> visited)
    {
        if (!visited.Add(type))
        {
            return false;
        }

        if (SymbolEqualityComparer.Default.Equals(type.OriginalDefinition, target.OriginalDefinition))
        {
            return true;
        }

        if (type.DeclaringSyntaxReferences.Length == 0)
        {
            return type.BaseType is not null && IsAuthoredOrMetadataAssignableTo(type.BaseType, target, project, visited);
        }

        foreach (var syntaxReference in type.DeclaringSyntaxReferences)
        {
            if (syntaxReference.GetSyntax() is not TypeDeclarationSyntax { BaseList: not null } declaration ||
                !project.AuthoredSyntaxTrees.Contains(declaration.SyntaxTree) ||
                DotNetGeneratedSource.IsGenerated(declaration.SyntaxTree))
            {
                continue;
            }

            var semanticModel = project.Compilation.GetSemanticModel(declaration.SyntaxTree);
            foreach (var baseType in declaration.BaseList.Types)
            {
                if (semanticModel.GetTypeInfo(baseType.Type).Type is INamedTypeSymbol candidate &&
                    IsAuthoredOrMetadataAssignableTo(candidate, target, project, visited))
                {
                    return true;
                }
            }
        }

        return false;
    }

    static bool IsAuthoredOrMetadataSymbol(
        ISymbol symbol,
        DotNetProjectCompilation project) => symbol.Locations.All(location =>
        !location.IsInSource ||
        (location.SourceTree is not null &&
         project.AuthoredSyntaxTrees.Contains(location.SourceTree) &&
         !DotNetGeneratedSource.IsGenerated(location.SourceTree)));

    static bool IsAssignableTo(INamedTypeSymbol type, string metadataName)
    {
        for (var current = type; current is not null; current = current.BaseType)
        {
            if (DotNetSubjectIds.MetadataName(current.OriginalDefinition) == metadataName)
            {
                return true;
            }
        }

        return type.AllInterfaces.Any(@interface => DotNetSubjectIds.MetadataName(@interface.OriginalDefinition) == metadataName);
    }
}
