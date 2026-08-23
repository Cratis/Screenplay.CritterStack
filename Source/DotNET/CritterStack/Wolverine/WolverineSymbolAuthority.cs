// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Cratis.Screenplay.Generation.DotNet;
using Microsoft.CodeAnalysis;

namespace Cratis.CritterStack.Screenplay.Wolverine;

static class WolverineSymbolAuthority
{
    public static bool IsAuthoredOrMetadataSymbol(
        ISymbol symbol,
        DotNetProjectCompilation project) => symbol.Locations.All(location =>
        !location.IsInSource ||
        (location.SourceTree is not null &&
         project.AuthoredSyntaxTrees.Contains(location.SourceTree) &&
         !DotNetGeneratedSource.IsGenerated(location.SourceTree)));
}
