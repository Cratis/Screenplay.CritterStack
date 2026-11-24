// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Cratis.Screenplay.Generation.DotNet;
using Microsoft.CodeAnalysis;

namespace Cratis.CritterStack.Screenplay.Wolverine;

sealed record WolverineSourceEntryPoint(IMethodSymbol Method, HttpEndpoint? Endpoint);

internal static partial class WolverineFacts
{
    public static IReadOnlyList<WolverineSourceEntryPoint> ObserveEntryPoints(
        DotNetProjectCompilation project,
        WolverineHandlerDiscoveryPolicy policy) =>
    [
        .. CandidateMethods(project)
            .Select(method => new WolverineSourceEntryPoint(method, EndpointFor(method)))
            .Where(entry => entry.Endpoint is not null || IsHandler(entry.Method.ContainingType, entry.Method, policy))
    ];

    static IEnumerable<IMethodSymbol> CandidateMethods(DotNetProjectCompilation project) =>
        new DotNetArtifactCatalog(project.Compilation).Types
            .Where(type => IsCandidateType(type, project))
            .SelectMany(type => SourceMethods(type, project));

    static IEnumerable<IMethodSymbol> SourceMethods(INamedTypeSymbol type, DotNetProjectCompilation project) =>
        type.GetMembers().OfType<IMethodSymbol>()
            .Where(method => IsPublicSourceMethod(method, project) && !IsIgnored(method));

    static bool IsCandidateType(INamedTypeSymbol type, DotNetProjectCompilation project) =>
        IsPublicSourceType(type, project) && !IsIgnored(type) && !WolverineSagaFacts.IsSagaType(type, project);
}
