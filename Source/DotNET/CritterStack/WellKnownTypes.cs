// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Cratis.CritterStack.Screenplay;

static class WellKnownTypes
{
    public const string MartenDocumentStore = "Marten.IDocumentStore";
    public const string MartenStoreOptions = "Marten.StoreOptions";
    public const string MartenSingleStreamProjectionOneId = "Marten.Events.Aggregation.SingleStreamProjection`1";
    public const string MartenSingleStreamProjectionTwoIds = "Marten.Events.Aggregation.SingleStreamProjection`2";
    public const string MartenMultiStreamProjection = "Marten.Events.Projections.MultiStreamProjection`2";
    public const string MartenEventProjection = "Marten.Events.Projections.EventProjection";
    public const string WolverineOptions = "Wolverine.WolverineOptions";
    public const string WolverineHttpMethodAttribute = "Wolverine.Http.WolverineHttpMethodAttribute";
    public const string WolverineAggregateHandlerAttribute = "Wolverine.Persistence.EventSourcing.DeciderFunctionAttribute";
    public const string WolverineWriteModelAttribute = "Wolverine.Persistence.EventSourcing.WriteModelAttribute";
    public const string WolverineLegacyAggregateHandlerAttribute = "Wolverine.Marten.AggregateHandlerAttribute";
    public const string WolverineLegacyWriteAggregateAttribute = "Wolverine.Marten.WriteAggregateAttribute";
    public const string WolverineHttpAggregateAttribute = "Wolverine.Http.Marten.AggregateAttribute";
    public const string WolverineEntityAttribute = "Wolverine.Persistence.EntityAttribute";
    public const string WolverineEvents = "Wolverine.Marten.Events";
    public const string WolverineEventsToAppend = "Wolverine.Persistence.EventSourcing.EventsToAppend";
    public const string WolverineOutgoingMessages = "Wolverine.OutgoingMessages";
    public const string WolverineStartStream = "Wolverine.Marten.IStartStream";
    public const string WolverineSideEffect = "Wolverine.ISideEffect";
}
