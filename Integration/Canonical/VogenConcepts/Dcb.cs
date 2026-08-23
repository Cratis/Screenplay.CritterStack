// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using JasperFx.Events.Tags;
using Wolverine.Persistence.EventSourcing;

namespace Cratis.CritterStack.Screenplay.Canonical.VogenConcepts;

/// <summary>
/// Reviews the dynamic consistency boundary for one order tag.
/// </summary>
/// <param name="Order">The tagged order.</param>
public sealed record ReviewOrderBoundary(OrderKey Order);

/// <summary>
/// State projected from events matching an order's dynamic consistency boundary.
/// </summary>
public sealed class OrderBoundary
{
    /// <summary>
    /// Gets or sets the number of matched events.
    /// </summary>
    public int EventCount { get; set; }
}

/// <summary>
/// Records that an order boundary was reviewed.
/// </summary>
/// <param name="Order">The reviewed order.</param>
public sealed record OrderBoundaryReviewed(OrderKey Order);

/// <summary>
/// Handles an order review through Wolverine's store-agnostic DCB model binding.
/// </summary>
public static class ReviewOrderBoundaryHandler
{
    /// <summary>
    /// Defines the exact order-tag and historical event condition for the boundary.
    /// </summary>
    /// <param name="command">The review command.</param>
    /// <returns>The boundary query.</returns>
    public static EventTagQuery Before(ReviewOrderBoundary command) =>
        EventTagQuery.For<OrderKey>(command.Order)
            .AndEventsOfType<OrderRegistered>();

    /// <summary>
    /// Reviews the current boundary state and appends an event declaratively.
    /// </summary>
    /// <param name="command">The review command.</param>
    /// <param name="boundary">The current boundary state, if matching events exist.</param>
    /// <returns>The event to append through the DCB boundary.</returns>
    public static OrderBoundaryReviewed Handle(
        ReviewOrderBoundary command,
        [DcbModel] OrderBoundary? boundary) =>
        new(command.Order);
}
