// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Cratis.CritterStack.Screenplay.Canonical.CommunityKitchen.Kitchen;

/// <summary>
/// A community kitchen planned one batch of soup.
/// </summary>
/// <param name="Recipe">The soup to prepare.</param>
/// <param name="TargetPortions">The intended number of portions.</param>
public sealed record BatchPlanned(string Recipe, int TargetPortions);

/// <summary>
/// Volunteers prepared more portions for the serving table.
/// </summary>
/// <param name="Portions">The additional portions prepared.</param>
public sealed record PortionsPrepared(int Portions);

/// <summary>
/// A batch reached the serving table.
/// </summary>
/// <param name="Portions">The number of portions available to serve.</param>
public sealed record BatchServed(int Portions);

/// <summary>
/// The target for a batch was adjusted.
/// </summary>
/// <param name="TargetPortions">The new target.</param>
public sealed record TargetAdjusted(int TargetPortions);
