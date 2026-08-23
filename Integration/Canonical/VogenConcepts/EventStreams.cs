// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using JasperFx.Events;
using Wolverine.Persistence.EventSourcing;

namespace Cratis.CritterStack.Screenplay.Canonical.VogenConcepts;

/// <summary>
/// Transfers value between two order streams.
/// </summary>
/// <param name="SourceOrder">The source order.</param>
/// <param name="DestinationOrder">The destination order.</param>
/// <param name="Amount">The amount to transfer.</param>
public sealed record TransferBetweenOrders(OrderKey SourceOrder, OrderKey DestinationOrder, decimal Amount);

/// <summary>
/// Value was debited from an order.
/// </summary>
/// <param name="Amount">The debited amount.</param>
public sealed record OrderDebited(decimal Amount);

/// <summary>
/// Value was credited to an order.
/// </summary>
/// <param name="Amount">The credited amount.</param>
public sealed record OrderCredited(decimal Amount);

/// <summary>
/// Handles transfers between distinct order streams.
/// </summary>
public static class TransferBetweenOrdersHandler
{
    /// <summary>
    /// Appends each event to the stream identified by its receiver parameter.
    /// </summary>
    /// <param name="command">The transfer command.</param>
    /// <param name="source">The source order stream.</param>
    /// <param name="destination">The destination order stream.</param>
    public static void Handle(
        TransferBetweenOrders command,
        [WriteModel(nameof(TransferBetweenOrders.SourceOrder))] IEventStream<Order> source,
        [WriteModel(nameof(TransferBetweenOrders.DestinationOrder))] IEventStream<Order> destination)
    {
        source.AppendOne(new OrderDebited(command.Amount));
        destination.AppendOne(new OrderCredited(command.Amount));
    }
}
