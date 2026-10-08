// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Marten;

namespace Cratis.CritterStack.Screenplay.Canonical.CommunityKitchen.Kitchen.Preparation;

/// <summary>
/// Records a completed round of preparation.
/// </summary>
/// <param name="Id">The batch identity.</param>
/// <param name="Portions">The additional portions.</param>
public sealed record PreparePortions(Guid Id, int Portions);

/// <summary>
/// Handles preparation through an explicit event append.
/// </summary>
public static class PreparePortionsHandler
{
    /// <summary>
    /// Appends and saves the preparation fact.
    /// </summary>
    /// <param name="command">The preparation details.</param>
    /// <param name="session">The event session.</param>
    /// <returns>A task for the persisted preparation.</returns>
    public static async Task Handle(PreparePortions command, IDocumentSession session)
    {
        session.Events.Append(command.Id, new PortionsPrepared(command.Portions));
        await session.SaveChangesAsync();
    }
}

/// <summary>
/// Adjusts the target of a batch identified by its recipe slot.
/// </summary>
/// <param name="Slot">The recipe slot whose batch is adjusted.</param>
/// <param name="TargetPortions">The new target.</param>
public sealed record AdjustTarget(string Slot, int TargetPortions);

/// <summary>
/// Handles target adjustments for a stream whose identity is computed at runtime.
/// </summary>
public static class AdjustTargetHandler
{
    /// <summary>
    /// Appends the adjusted target to the batch derived from the slot.
    /// </summary>
    /// <param name="command">The adjustment.</param>
    /// <param name="session">The event session.</param>
    /// <returns>A task for the persisted adjustment.</returns>
    public static async Task Handle(AdjustTarget command, IDocumentSession session)
    {
        session.Events.Append(BatchFor(command.Slot), new TargetAdjusted(command.TargetPortions));
        await session.SaveChangesAsync();
    }

    static Guid BatchFor(string slot) => Guid.Parse(slot);
}
