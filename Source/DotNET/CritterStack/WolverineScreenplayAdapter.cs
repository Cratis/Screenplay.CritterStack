// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Cratis.Screenplay.Generation;
using Cratis.Screenplay.Generation.DotNet;

namespace Cratis.CritterStack.Screenplay;

/// <summary>
/// Admits authored Wolverine application source without requiring Marten. Admission and descriptor only; emission is not yet cut over.
/// </summary>
sealed class WolverineScreenplayAdapter : IDescribedDotNetScreenplayAdapter, IDotNetScreenplayAdapter
{
    const string AdapterId = "wolverine";
    const string AdapterVersion = "1.0.0";

    public AdapterIdentity Identity { get; } = new() { Id = AdapterId, Version = AdapterVersion };

    public AdapterDescriptor Descriptor { get; } = new()
    {
        Identity = new AdapterIdentity { Id = AdapterId, Version = AdapterVersion },
        SourceLanguage = AdapterSourceLanguage.CSharp,
        Category = AdapterCategory.ApplicationFramework,
        CompatibleGenerationVersions = new GenerationVersionRange { MinimumInclusive = new Version(0, 23, 0) },
        RequiredHostCapabilities = CritterStackAdapterDescriptors.HostCapabilities,
        RequiredApiCapabilities = [CritterStackAdapterApiCapabilities.WolverineApplication],
        EmittedFactCapabilities =
        [
            .. CritterStackAdapterDescriptors.EmittedFacts,
            GenerationFactCapability.ArtifactDeclaration,
            GenerationFactCapability.ArtifactMemberDeclaration,
            GenerationFactCapability.ArtifactMemberTypeUse,
            GenerationFactCapability.ArtifactMemberRole,
            GenerationFactCapability.TypeUseBindingPolicy,
            GenerationFactCapability.EntryPoint
        ]
    };

    public bool CanAnalyze(DotNetAnalysisContext context) => Probe(context) is AdapterProbeApplicable;

    public AdapterProbeResult Probe(DotNetAnalysisContext context)
    {
        try
        {
            return CritterStackAdapterProbe.Applicable(
                CritterStackAdapterProbe.EvidenceFor(
                    context,
                    CritterStackAdapterProbe.IsWolverineApi,
                    CritterStackAdapterApiCapabilities.WolverineApplication,
                    "Wolverine",
                    includeAttributes: true),
                CritterStackAdapterProbe.WolverineUnsafeSourceMapping,
                "Wolverine");
        }
        catch (DotNetSourceTreeNotMapped)
        {
            return CritterStackAdapterProbe.UnsafeSourceMapping(CritterStackAdapterProbe.WolverineUnsafeSourceMapping, "Wolverine");
        }
    }

    public AdapterContribution Analyze(DotNetAnalysisContext context, DotNetAdapterOptions options) =>
        throw new NotSupportedException("Atomic Wolverine emission is not part of the foundation phase; only the descriptor and probe are available.");
}
