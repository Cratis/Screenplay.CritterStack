// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Immutable;

using Cratis.Screenplay.Generation;

namespace Cratis.CritterStack.Screenplay;

/// <summary>
/// Descriptor values shared by the atomic Critter Stack adapters.
/// </summary>
static class CritterStackAdapterDescriptors
{
    public static ImmutableArray<AdapterHostCapability> HostCapabilities { get; } =
    [
        AdapterHostCapability.AuthoredSource,
        AdapterHostCapability.StableSourceLocations,
        AdapterHostCapability.SemanticAnalysis
    ];

    public static ImmutableArray<GenerationFactCapability> EmittedFacts { get; } =
    [
        GenerationFactCapability.Artifact,
        GenerationFactCapability.ArtifactPlacement,
        GenerationFactCapability.ConceptRepresentation,
        GenerationFactCapability.Relationship
    ];
}
