// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using JasperFx.Events.EventModeling;
using Marten;
using Wolverine.Persistence.EventSourcing;

namespace Cratis.CritterStack.Screenplay.Canonical.VogenConcepts;

/// <summary>
/// Decision state for batch-fetched order streams.
/// </summary>
public sealed class BatchOrder
{
    /// <summary>
    /// Gets or sets the stream identity.
    /// </summary>
    public Guid Id { get; set; }
}

/// <summary>
/// Rebalances two order streams.
/// </summary>
/// <param name="FromId">The source identity.</param>
/// <param name="ToId">The destination identity.</param>
public sealed record RebalanceOrders(Guid FromId, Guid ToId);

/// <summary>
/// Cancels a runtime-sized group of orders.
/// </summary>
/// <param name="Ids">The order identities.</param>
public sealed record CancelOrders(IReadOnlyList<Guid> Ids);

/// <summary>
/// A batch order was debited.
/// </summary>
public sealed record BatchOrderDebited;

/// <summary>
/// A batch order was credited.
/// </summary>
public sealed record BatchOrderCredited;

/// <summary>
/// A batch order was cancelled.
/// </summary>
public sealed record BatchOrderCancelled;

/// <summary>
/// Applies an automatically initiated order rebalance.
/// </summary>
public static class RebalanceOrdersHandler
{
    /// <summary>
    /// Fetches two streams and appends each event to its own slot.
    /// </summary>
    /// <param name="command">The order identities.</param>
    /// <param name="session">The document session.</param>
    /// <param name="cancellation">The cancellation token.</param>
    /// <returns>The asynchronous operation.</returns>
    [SlicePattern(SlicePattern.Automation)]
    public static async Task Handle(RebalanceOrders command, IDocumentSession session, CancellationToken cancellation)
    {
        var streams = await session.Events.FetchManyForWriting<BatchOrder>([command.FromId, command.ToId], cancellation);
        streams[0].AppendOne(new BatchOrderDebited());
        streams[1].AppendOne(new BatchOrderCredited());
    }
}

/// <summary>
/// Cancels each fetched order.
/// </summary>
public static class CancelOrdersHandler
{
    /// <summary>
    /// Appends a cancellation to each stream in the fetched family.
    /// </summary>
    /// <param name="command">The order identities.</param>
    /// <param name="session">The document session.</param>
    /// <returns>The asynchronous operation.</returns>
    public static async Task Handle(CancelOrders command, IDocumentSession session)
    {
        var streams = await session.Events.FetchManyForWriting<BatchOrder>(command.Ids).ConfigureAwait(false);
        foreach (var stream in streams)
        {
            stream.AppendOne(new BatchOrderCancelled());
        }
    }
}
