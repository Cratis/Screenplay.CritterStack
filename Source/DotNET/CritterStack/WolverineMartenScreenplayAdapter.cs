// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Cratis.Screenplay.Generation;
using Cratis.Screenplay.Generation.DotNet;

namespace Cratis.CritterStack.Screenplay;

/// <summary>
/// Admits authored source that uses the exact Wolverine-Marten integration APIs together with Marten and Wolverine.
/// Admission and descriptor only; emission is not yet cut over.
/// </summary>
sealed class WolverineMartenScreenplayAdapter : IDescribedDotNetScreenplayAdapter, IDotNetScreenplayAdapter
{
    const string AdapterId = "wolverine-marten";
    const string AdapterVersion = "1.0.0";

    public AdapterIdentity Identity { get; } = new() { Id = AdapterId, Version = AdapterVersion };

    public AdapterDescriptor Descriptor { get; } = new()
    {
        Identity = new AdapterIdentity { Id = AdapterId, Version = AdapterVersion },
        SourceLanguage = AdapterSourceLanguage.CSharp,
        Category = AdapterCategory.Integration,
        CompatibleGenerationVersions = new GenerationVersionRange { MinimumInclusive = new Version(0, 23, 0) },
        RequiredHostCapabilities = CritterStackAdapterDescriptors.HostCapabilities,
        RequiredApiCapabilities =
        [
            CritterStackAdapterApiCapabilities.MartenApplication,
            CritterStackAdapterApiCapabilities.WolverineApplication,
            CritterStackAdapterApiCapabilities.WolverineMartenIntegration
        ],
        EmittedFactCapabilities = CritterStackAdapterDescriptors.EmittedFacts
    };

    public bool CanAnalyze(DotNetAnalysisContext context) => Probe(context) is AdapterProbeApplicable;

    public AdapterProbeResult Probe(DotNetAnalysisContext context)
    {
        try
        {
            var integration = CritterStackAdapterProbe.EvidenceFor(
                context,
                CritterStackAdapterProbe.IsIntegrationApi,
                CritterStackAdapterApiCapabilities.WolverineMartenIntegration,
                "Wolverine-Marten integration",
                includeAttributes: true);
            if (integration.Count == 0)
            {
                return new AdapterProbeNotApplicable();
            }

            var marten = CritterStackAdapterProbe.EvidenceFor(
                context,
                CritterStackAdapterProbe.IsMartenApi,
                CritterStackAdapterApiCapabilities.MartenApplication,
                "Marten",
                includeAttributes: false);
            var wolverine = CritterStackAdapterProbe.EvidenceFor(
                context,
                CritterStackAdapterProbe.IsWolverineApi,
                CritterStackAdapterApiCapabilities.WolverineApplication,
                "Wolverine",
                includeAttributes: true);
            return marten.Count == 0 || wolverine.Count == 0
                ? new AdapterProbeNotApplicable()
                : CritterStackAdapterProbe.Applicable(
                    [.. integration, .. marten, .. wolverine],
                    CritterStackAdapterProbe.IntegrationUnsafeSourceMapping,
                    "Wolverine-Marten integration");
        }
        catch (DotNetSourceTreeNotMapped)
        {
            return CritterStackAdapterProbe.UnsafeSourceMapping(CritterStackAdapterProbe.IntegrationUnsafeSourceMapping, "Wolverine-Marten integration");
        }
    }

    public AdapterContribution Analyze(DotNetAnalysisContext context, DotNetAdapterOptions options) =>
        throw new NotSupportedException("Atomic Wolverine-Marten emission is not part of the foundation phase; only the descriptor and probe are available.");
}
