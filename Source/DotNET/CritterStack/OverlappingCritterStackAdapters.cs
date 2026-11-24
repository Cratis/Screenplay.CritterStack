// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Cratis.CritterStack.Screenplay;

/// <summary>
/// Exception raised when a combined Critter Stack adapter is registered alongside an atomic adapter.
/// </summary>
public sealed class OverlappingCritterStackAdapters : ArgumentException
{
    /// <summary>
    /// Initializes a new instance of the <see cref="OverlappingCritterStackAdapters"/> class.
    /// </summary>
    public OverlappingCritterStackAdapters()
        : base("The combined Critter Stack adapter cannot be composed with Marten, Wolverine, or Wolverine-Marten adapters; select either the combined adapter or the independent adapters", "adapters")
    {
    }
}
