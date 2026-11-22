// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Cratis.CritterStack.Screenplay.Marten;
using Cratis.Screenplay.Generation;
using Cratis.Screenplay.Generation.DotNet;
using Microsoft.CodeAnalysis;

namespace Cratis.CritterStack.Screenplay;

/// <summary>
/// Analyzes authored Marten application source into framework-neutral Screenplay facts.
/// </summary>
public sealed class MartenScreenplayAdapter : IDescribedDotNetScreenplayAdapter, IDotNetScreenplayAdapter
{
    const string AdapterId = "marten";
    const string AdapterVersion = "1.0.0";

    /// <inheritdoc/>
    public AdapterIdentity Identity { get; } = new() { Id = AdapterId, Version = AdapterVersion };

    /// <inheritdoc/>
    public AdapterDescriptor Descriptor { get; } = new()
    {
        Identity = new AdapterIdentity { Id = AdapterId, Version = AdapterVersion },
        SourceLanguage = AdapterSourceLanguage.CSharp,
        Category = AdapterCategory.EventStore,
        CompatibleGenerationVersions = new GenerationVersionRange
        {
            MinimumInclusive = new Version(0, 23, 0)
        },
        RequiredHostCapabilities =
        [
            AdapterHostCapability.AuthoredSource,
            AdapterHostCapability.StableSourceLocations,
            AdapterHostCapability.SemanticAnalysis
        ],
        RequiredApiCapabilities = [CritterStackAdapterApiCapabilities.MartenApplication],
        EmittedFactCapabilities =
        [
            GenerationFactCapability.Artifact,
            GenerationFactCapability.ArtifactPlacement,
            GenerationFactCapability.ConceptRepresentation,
            GenerationFactCapability.Relationship
        ]
    };

    /// <inheritdoc/>
    public bool CanAnalyze(DotNetAnalysisContext context) => Probe(context) is AdapterProbeApplicable;

    /// <inheritdoc/>
    public AdapterProbeResult Probe(DotNetAnalysisContext context)
    {
        try
        {
            return CritterStackAdapterProbe.Applicable(
                CritterStackAdapterProbe.EvidenceFor(
                    context,
                    CritterStackAdapterProbe.IsMartenApi,
                    CritterStackAdapterApiCapabilities.MartenApplication,
                    "Marten",
                    includeAttributes: false),
                MartenDiagnosticCodes.UnsafeSourceMapping,
                "Marten");
        }
        catch (DotNetSourceTreeNotMapped)
        {
            return CritterStackAdapterProbe.UnsafeSourceMapping(MartenDiagnosticCodes.UnsafeSourceMapping, "Marten");
        }
    }

    /// <inheritdoc/>
    public AdapterContribution Analyze(DotNetAnalysisContext context, DotNetAdapterOptions options)
    {
        var facts = new List<GenerationFact>();
        var diagnostics = new List<GenerationDiagnostic>();
        var placements = new List<CritterStackPlacementIntent>();
        var subjects = new CritterStackSubjectResolver(context);
        var (stores, ownership) = MartenStoreScopeDiscovery.DiscoverOwnership(context, Identity, subjects);
        facts.AddRange(stores.Facts);
        diagnostics.AddRange(stores.Diagnostics);

        foreach (var project in context.Projects)
        {
            var subjectCheckpoint = subjects.Checkpoint();
            var marten = MartenFacts.Discover(project, options, Identity, subjects, stores);
            var documents = MartenDocumentFacts.Discover(
                project,
                Identity,
                subjects,
                marten.Documents,
                ownership,
                includeIntegrationApis: false);
            if (subjects.HasBlockingDiagnosticsSince(subjectCheckpoint))
            {
                continue;
            }

            facts.AddRange(marten.Facts);
            facts.AddRange(documents.Facts);
            diagnostics.AddRange(marten.Diagnostics);
            diagnostics.AddRange(documents.Diagnostics);
            placements.AddRange(marten.Placements ?? []);
        }

        diagnostics.AddRange(subjects.Diagnostics);
        facts.AddRange(CritterStackSourcePlacement.Derive(context, options, placements, diagnostics));

        return EnumConceptFacts.AddTo(context, new AdapterContribution
        {
            Adapter = Identity,
            Facts = facts,
            Diagnostics = diagnostics
        });
    }
}
