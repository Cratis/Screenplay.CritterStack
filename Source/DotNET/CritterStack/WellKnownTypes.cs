// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Cratis.CritterStack.Screenplay;

static class WellKnownTypes
{
    public const string MartenDocumentStore = "Marten.IDocumentStore";
    public const string MartenStoreOptions = "Marten.StoreOptions";
    public const string MartenProjectionOptions = "Marten.Events.Projections.ProjectionOptions";
    public const string MartenSingleStreamProjectionOneId = "Marten.Events.Aggregation.SingleStreamProjection`1";
    public const string MartenSingleStreamProjectionTwoIds = "Marten.Events.Aggregation.SingleStreamProjection`2";
    public const string MartenMultiStreamProjection = "Marten.Events.Projections.MultiStreamProjection`2";
    public const string MartenEventProjection = "Marten.Events.Projections.EventProjection";
    public const string MartenLegacyEventStream = "Marten.Events.Aggregation.IEventStream`1";
    public const string JasperFxEventStream = "JasperFx.Events.IEventStream`1";
    public const string JasperFxProjectionLifecycle = "JasperFx.Events.Projections.ProjectionLifecycle";
    public const string JasperFxSnapshotLifecycle = "JasperFx.Events.Projections.SnapshotLifecycle";
    public const string MartenProjectionLifecycle = "Marten.Events.Projections.ProjectionLifecycle";
    public const string MartenSnapshotLifecycle = "Marten.Events.Projections.SnapshotLifecycle";
    public const string WolverineOptions = "Wolverine.WolverineOptions";
    public const string WolverineHandlerAttribute = "Wolverine.Attributes.WolverineHandlerAttribute";
    public const string WolverineLegacyHandlerAttribute = "Wolverine.WolverineHandlerAttribute";
    public const string WolverineIgnoreAttribute = "Wolverine.Attributes.WolverineIgnoreAttribute";
    public const string WolverineHttpMethodAttribute = "Wolverine.Http.WolverineHttpMethodAttribute";
    public const string WolverineEmptyResponseAttribute = "Wolverine.Http.EmptyResponseAttribute";
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
    public const string WolverineReturnType = "Wolverine.Configuration.IWolverineReturnType";
    public const string WolverineLegacyReturnType = "Wolverine.Http.IWolverineReturnType";
    public const string WolverineResponseAware = "Wolverine.IResponseAware";
    public const string WolverineLegacyResponseAware = "Wolverine.Http.IResponseAware";
}
