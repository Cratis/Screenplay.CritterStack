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

sealed record WolverineReturnContext(
    DotNetProjectCompilation Project,
    bool IsHttpEndpoint,
    bool AggregateWorkflow,
    bool HasEventStream,
    bool EmptyResponse);

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
        var context = new WolverineReturnContext(
            project,
            isHttpEndpoint,
            aggregateWorkflow,
            hasEventStream,
            DotNetSymbols.HasAttribute(method, WellKnownTypes.WolverineEmptyResponseAttribute));
        return [.. WolverineReturnTypes.CreatedValues(method).Select((type, slot) => Classify(type, slot, context))];
    }

    public static bool IsTimeoutMessage(INamedTypeSymbol type) =>
        IsAssignableTo(type, WellKnownTypes.WolverineTimeoutMessage);

    static WolverineReturnConsequence Classify(ITypeSymbol type, int slot, WolverineReturnContext context)
    {
        if (type is not INamedTypeSymbol named)
        {
            return new(slot, type, WolverineReturnConsequenceKind.Unknown);
        }

        if (MessagingKind(named, context.Project) is { } messaging)
        {
            return new(slot, type, messaging);
        }

        if (StorageActionEntityType(named) is { } entityType)
        {
            return new(slot, type, WolverineReturnConsequenceKind.StorageAction, entityType);
        }

        return new(slot, type, ResponseKind(named, slot, context));
    }

    /// <summary>
    /// The kinds decided before a storage action: saga state, timeouts, outgoing messages and side effects, in that order.
    /// </summary>
    static WolverineReturnConsequenceKind? MessagingKind(INamedTypeSymbol named, DotNetProjectCompilation project)
    {
        if (IsSagaState(named, project))
        {
            return WolverineReturnConsequenceKind.SagaState;
        }

        if (IsTimeoutMessage(named))
        {
            return WolverineReturnConsequenceKind.Cascade;
        }

        if (DotNetSubjectIds.MetadataName(named.OriginalDefinition) == WellKnownTypes.WolverineOutgoingMessages)
        {
            return WolverineReturnConsequenceKind.OutgoingMessages;
        }

        return IsAssignableTo(named, WellKnownTypes.WolverineSideEffect) ? WolverineReturnConsequenceKind.SideEffect : null;
    }

    /// <summary>
    /// The kinds decided after a storage action: persistence operations, responses, aggregate events, the HTTP body and cascades.
    /// </summary>
    static WolverineReturnConsequenceKind ResponseKind(INamedTypeSymbol named, int slot, WolverineReturnContext context)
    {
        if (_persistenceOperations.Contains(DotNetSubjectIds.MetadataName(named.OriginalDefinition)))
        {
            return WolverineReturnConsequenceKind.PersistenceOperation;
        }

        if (IsResponse(named))
        {
            return WolverineReturnConsequenceKind.HttpResponse;
        }

        if (context.AggregateWorkflow && !context.HasEventStream && IsPayload(named))
        {
            return WolverineReturnConsequenceKind.PersistedEvent;
        }

        if (context.IsHttpEndpoint && slot == 0 && !context.EmptyResponse)
        {
            return WolverineReturnConsequenceKind.HttpResponse;
        }

        return IsPayload(named) ? WolverineReturnConsequenceKind.Cascade : WolverineReturnConsequenceKind.Unknown;
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
