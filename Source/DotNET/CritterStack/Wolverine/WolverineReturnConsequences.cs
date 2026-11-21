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
    ITypeSymbol? EntityType = null,
    WolverineMartenOperation? MartenOperation = null);

sealed record WolverineMartenOperation(string FactoryName, INamedTypeSymbol ModelType);

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

    static readonly HashSet<string> _martenFactories =
    [
        "Store", "Insert", "Update", "Delete", "StoreMany", "StartStream"
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
        return [.. WolverineReturnTypes.CreatedValues(method).Select((type, slot) =>
            MartenOperation(method, project, type, slot) ?? Classify(type, slot, context))];
    }

    public static bool IsTimeoutMessage(INamedTypeSymbol type) =>
        WolverineReturnTypes.IsAssignableTo(type, WellKnownTypes.WolverineTimeoutMessage);

    static WolverineReturnConsequence? MartenOperation(IMethodSymbol method, DotNetProjectCompilation project, ITypeSymbol type, int slot)
    {
        if (type is not INamedTypeSymbol named || !WolverineReturnTypes.IsAssignableTo(named, WellKnownTypes.WolverineMartenOp))
        {
            return null;
        }

        var factories = WolverineReturnTypes.MartenFactories(method, project, slot);
        if (factories.Count == 0 || factories.Any(_ => _ is null))
        {
            return null;
        }

        var operation = SharedOperation([.. factories.OfType<IMethodSymbol>().Select(MartenOperation)]);
        return operation is null ? null : new(slot, type, WolverineReturnConsequenceKind.StorageAction, MartenOperation: operation);
    }

    static WolverineMartenOperation? MartenOperation(IMethodSymbol factory) =>
        _martenFactories.Contains(factory.Name) &&
        factory.TypeArguments is [INamedTypeSymbol modelType]
            ? new(factory.Name, modelType)
            : null;

    static WolverineMartenOperation? SharedOperation(WolverineMartenOperation?[] operations)
    {
        var first = operations[0];
        return first is not null && operations.All(operation => SameOperation(operation, first)) ? first : null;
    }

    static bool SameOperation(WolverineMartenOperation? operation, WolverineMartenOperation first) =>
        operation is not null && operation.FactoryName == first.FactoryName &&
        SymbolEqualityComparer.Default.Equals(operation.ModelType, first.ModelType);

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
    /// <param name="named">The returned type.</param>
    /// <param name="project">The project compilation.</param>
    /// <returns>The kind, or <see langword="null"/> when a later stage decides.</returns>
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

        return WolverineReturnTypes.IsAssignableTo(named, WellKnownTypes.WolverineSideEffect) ? WolverineReturnConsequenceKind.SideEffect : null;
    }

    /// <summary>
    /// The kinds decided after a storage action: persistence operations, responses, aggregate events, the HTTP body and cascades.
    /// </summary>
    /// <param name="named">The returned type.</param>
    /// <param name="slot">The position of the value among the created values.</param>
    /// <param name="context">The classification context.</param>
    /// <returns>The kind.</returns>
    static WolverineReturnConsequenceKind ResponseKind(INamedTypeSymbol named, int slot, WolverineReturnContext context)
    {
        if (_persistenceOperations.Contains(DotNetSubjectIds.MetadataName(named.OriginalDefinition)))
        {
            return WolverineReturnConsequenceKind.PersistenceOperation;
        }

        if (WolverineReturnTypes.IsHttpResponseType(named))
        {
            return WolverineReturnConsequenceKind.HttpResponse;
        }

        if (IsAggregateEvent(named, context))
        {
            return WolverineReturnConsequenceKind.PersistedEvent;
        }

        if (IsHttpBody(slot, context))
        {
            return WolverineReturnConsequenceKind.HttpResponse;
        }

        return IsPayload(named) ? WolverineReturnConsequenceKind.Cascade : WolverineReturnConsequenceKind.Unknown;
    }

    static bool IsAggregateEvent(INamedTypeSymbol named, WolverineReturnContext context) =>
        context.AggregateWorkflow && !context.HasEventStream && IsPayload(named);

    static bool IsHttpBody(int slot, WolverineReturnContext context) =>
        context.IsHttpEndpoint && slot == 0 && !context.EmptyResponse;

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

    static bool IsPayload(INamedTypeSymbol type) =>
        type.SpecialType == SpecialType.None &&
        !WolverineReturnTypes.IsSpecialReturn(type) &&
        !DotNetSubjectIds.MetadataName(type.OriginalDefinition).StartsWith("System.", StringComparison.Ordinal);

    static bool IsSagaState(INamedTypeSymbol type, DotNetProjectCompilation project) =>
        WolverineSagaTypes.IsSagaState(type, project);
}
