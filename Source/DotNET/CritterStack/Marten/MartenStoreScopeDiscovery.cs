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
}
