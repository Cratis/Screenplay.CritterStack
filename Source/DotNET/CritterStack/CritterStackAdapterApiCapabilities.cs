// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Cratis.Screenplay.Generation;

namespace Cratis.CritterStack.Screenplay;

/// <summary>
/// Defines exact source API capabilities required by the atomic CritterStack adapters.
/// </summary>
public static class CritterStackAdapterApiCapabilities
{
    /// <summary>
    /// Authored application source uses exact Marten APIs.
    /// </summary>
    public static AdapterApiCapability MartenApplication { get; } = new() { Id = "marten.application" };

    /// <summary>
    /// Authored application source uses exact Wolverine APIs.
    /// </summary>
    public static AdapterApiCapability WolverineApplication { get; } = new() { Id = "wolverine.application" };

    /// <summary>
    /// Authored application source uses exact Wolverine-Marten integration APIs.
    /// </summary>
    public static AdapterApiCapability WolverineMartenIntegration { get; } = new() { Id = "wolverine-marten.integration" };
}
