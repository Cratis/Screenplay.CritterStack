// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Cratis.CritterStack.Screenplay.Canonical.CommunityKitchen.Kitchen.Cards;
using JasperFx.Events.Projections;
using Marten;

namespace Cratis.CritterStack.Screenplay.Canonical.CommunityKitchen;

/// <summary>
/// Configures the kitchen's Marten store.
/// </summary>
public static class KitchenConfiguration
{
    /// <summary>
    /// Registers the volunteer-facing card as an inline projection.
    /// </summary>
    /// <param name="options">The Marten store options.</param>
    public static void Configure(StoreOptions options) =>
        options.Projections.Add<BatchCardProjection>(ProjectionLifecycle.Inline);
}
