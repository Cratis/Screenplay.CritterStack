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
    SideEffect
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
                    Classify(type, slot, isHttpEndpoint, aggregateWorkflow, hasEventStream, emptyResponse)))
        ];
    }

    static WolverineReturnConsequenceKind Classify(
        ITypeSymbol type,
        int slot,
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

    static bool IsAssignableTo(INamedTypeSymbol type, string metadataName) =>
        DotNetSubjectIds.MetadataName(type.OriginalDefinition) == metadataName ||
        type.AllInterfaces.Any(@interface => DotNetSubjectIds.MetadataName(@interface.OriginalDefinition) == metadataName);
}
