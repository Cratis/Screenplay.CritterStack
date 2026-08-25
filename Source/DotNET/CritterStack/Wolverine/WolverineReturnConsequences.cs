// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Cratis.Screenplay.Generation.DotNet;
using Microsoft.CodeAnalysis;

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
    StorageAction,
    SagaState
}

sealed record WolverineReturnConsequence(
    int Slot,
    ITypeSymbol Type,
    WolverineReturnConsequenceKind Kind,
    ITypeSymbol? EntityType = null);

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
                .Select((type, slot) => Classify(type, slot, project, isHttpEndpoint, aggregateWorkflow, hasEventStream, emptyResponse))
        ];
    }

    public static bool IsTimeoutMessage(INamedTypeSymbol type) =>
        IsAssignableTo(type, WellKnownTypes.WolverineTimeoutMessage);

    static WolverineReturnConsequence Classify(
        ITypeSymbol type,
        int slot,
        DotNetProjectCompilation project,
        bool isHttpEndpoint,
        bool aggregateWorkflow,
        bool hasEventStream,
        bool emptyResponse)
    {
        WolverineReturnConsequence Consequence(
            WolverineReturnConsequenceKind kind,
            ITypeSymbol? entityType = null) => new(slot, type, kind, entityType);

        if (type is not INamedTypeSymbol named)
        {
            return Consequence(WolverineReturnConsequenceKind.Unknown);
        }

        var metadataName = DotNetSubjectIds.MetadataName(named.OriginalDefinition);
        if (IsSagaState(named, project))
        {
            return Consequence(WolverineReturnConsequenceKind.SagaState);
        }

        if (IsTimeoutMessage(named))
        {
            return Consequence(WolverineReturnConsequenceKind.Cascade);
        }

        if (metadataName == WellKnownTypes.WolverineOutgoingMessages)
        {
            return Consequence(WolverineReturnConsequenceKind.OutgoingMessages);
        }

        if (IsAssignableTo(named, WellKnownTypes.WolverineSideEffect))
        {
            return Consequence(WolverineReturnConsequenceKind.SideEffect);
        }

        if (StorageActionEntityType(named) is { } entityType)
        {
            return Consequence(WolverineReturnConsequenceKind.StorageAction, entityType);
        }

        if (_persistenceOperations.Contains(metadataName))
        {
            return Consequence(WolverineReturnConsequenceKind.PersistenceOperation);
        }

        if (IsResponse(named))
        {
            return Consequence(WolverineReturnConsequenceKind.HttpResponse);
        }

        if (aggregateWorkflow && !hasEventStream && IsPayload(named))
        {
            return Consequence(WolverineReturnConsequenceKind.PersistedEvent);
        }

        if (isHttpEndpoint && slot == 0 && !emptyResponse)
        {
            return Consequence(WolverineReturnConsequenceKind.HttpResponse);
        }

        return IsPayload(named)
            ? Consequence(WolverineReturnConsequenceKind.Cascade)
            : Consequence(WolverineReturnConsequenceKind.Unknown);
    }

    static ITypeSymbol? StorageActionEntityType(INamedTypeSymbol type)
    {
        var metadataName = DotNetSubjectIds.MetadataName(type.OriginalDefinition);
        if (string.Equals(metadataName, WellKnownTypes.WolverineUnitOfWork, StringComparison.Ordinal) ||
            string.Equals(metadataName, WellKnownTypes.WolverineStorageAction, StringComparison.Ordinal))
        {
            return type.TypeArguments[0];
        }

        return type.AllInterfaces
            .FirstOrDefault(@interface => DotNetSubjectIds.MetadataName(@interface.OriginalDefinition) == WellKnownTypes.WolverineStorageAction)?
            .TypeArguments[0];
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

    static bool IsSagaState(INamedTypeSymbol type, DotNetProjectCompilation project) =>
        WolverineSagaTypes.IsSagaState(type, project);

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
