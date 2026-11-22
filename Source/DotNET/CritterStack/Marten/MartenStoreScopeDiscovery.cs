// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Cratis.Screenplay.Generation;
using Cratis.Screenplay.Generation.DotNet;

namespace Cratis.CritterStack.Screenplay.Marten;

static class MartenStoreScopeDiscovery
{
    public static MartenStoreIndex Discover(
        DotNetAnalysisContext context,
        AdapterIdentity adapter,
        CritterStackSubjectResolver subjects) => MartenStoreIndex.Create(context, adapter, subjects);

    /// <summary>
    /// Builds the per-run store index and backend ownership resolver; nothing is shared between runs.
    /// </summary>
    /// <param name="context">The analysis context.</param>
    /// <param name="adapter">The adapter that takes responsibility for the emitted store facts.</param>
    /// <param name="subjects">The run-local subject resolver.</param>
    /// <returns>The store index and the ownership resolver built over it and the source routing index.</returns>
    public static (MartenStoreIndex Stores, MartenBackendOwnershipResolver Ownership) DiscoverOwnership(
        DotNetAnalysisContext context,
        AdapterIdentity adapter,
        CritterStackSubjectResolver subjects)
    {
        var stores = Discover(context, adapter, subjects);
        var routing = Wolverine.WolverineStorageRouting.Discover(context, stores);
        return (stores, new MartenBackendOwnershipResolver(stores, routing));
    }
}
