// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Cratis.CritterStack.Screenplay;

/// <summary>
/// Defines options controlling Screenplay generation from Critter Stack source.
/// </summary>
public sealed record CritterStackScreenplayOptions
{
    /// <summary>
    /// Gets the generated Screenplay domain name. The single project or assembly name is used when omitted.
    /// </summary>
    public string? Domain { get; init; }

    /// <summary>
    /// Gets an optional project-relative directory beneath which source placement begins.
    /// </summary>
    public string? FeatureRoot { get; init; }

    /// <summary>
    /// Gets an optional module that all discovered artifacts should be placed beneath.
    /// </summary>
    public string? Module { get; init; }

    /// <summary>
    /// Gets the number of leading namespace segments to omit from inferred features.
    /// </summary>
    public int NamespaceSegmentsToSkip { get; init; }
}
