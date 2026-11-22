// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Reflection;

using Cratis.CritterStack.Screenplay.Wolverine;
using Cratis.Screenplay.Generation;
using Cratis.Screenplay.Generation.DotNet;

namespace Cratis.CritterStack.Screenplay;

/// <summary>
/// Analyzes Marten and Wolverine application source into framework-neutral Screenplay semantic facts.
/// </summary>
public sealed class CritterStackScreenplayAdapter : IDotNetScreenplayAdapter
{
    static readonly AdapterIdentity _identity = new()
    {
        Id = "cratis.critter-stack",
        Version = typeof(CritterStackScreenplayAdapter).Assembly
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion ?? "0.0.0"
    };

    /// <inheritdoc/>
    public AdapterIdentity Identity => _identity;

    /// <summary>
    /// Gets the atomic adapters under construction; not yet part of any public roster or of this adapter's analysis.
    /// </summary>
    internal static IReadOnlyList<IDescribedDotNetScreenplayAdapter> AtomicAdapters { get; } =
    [
        new MartenScreenplayAdapter(),
        new WolverineScreenplayAdapter(),
        new WolverineMartenScreenplayAdapter()
    ];

    /// <inheritdoc/>
    public bool CanAnalyze(DotNetAnalysisContext context) => context.Projects.Any(project =>
        project.Compilation.GetTypeByMetadataName(WellKnownTypes.MartenStoreOptions) is not null ||
        project.Compilation.GetTypeByMetadataName(WellKnownTypes.MartenDocumentStore) is not null ||
        project.Compilation.GetTypeByMetadataName(WellKnownTypes.WolverineOptions) is not null);

    /// <inheritdoc/>
    public AdapterContribution Analyze(DotNetAnalysisContext context, DotNetAdapterOptions options) =>
        ConceptTypeReferenceBinder.WithoutMissingConcepts(
            [AnalyzeForComposition(context, options, useSharedPlacement: context.Projects.Any(_ => _.SourceContext is not null))])[0];

    internal AdapterContribution AnalyzeCompatibility(DotNetAnalysisContext context, DotNetAdapterOptions options) =>
        ConceptTypeReferenceBinder.WithoutMissingConcepts([AnalyzeForComposition(context, options, useSharedPlacement: false)])[0];

    /// <summary>
    /// Analyzes while retaining type subjects that independently composed adapters may contribute concepts for.
    /// </summary>
    /// <param name="context">The analysis context.</param>
    /// <param name="options">The adapter options.</param>
    /// <param name="useSharedPlacement">Whether to derive shared source placement.</param>
    /// <param name="implementations">The opt-in, selected-project authored implementation manifest.</param>
    /// <returns>The contribution; the composer must remove type subjects without a contributed concept.</returns>
    internal AdapterContribution AnalyzeForComposition(
        DotNetAnalysisContext context,
        DotNetAdapterOptions options,
        bool useSharedPlacement,
        AuthoredImplementationManifest? implementations = null)
    {
        var facts = new List<GenerationFact>();
        var diagnostics = new List<GenerationDiagnostic>();
        var placements = new List<CritterStackPlacementIntent>();
        var hasSourceContext = context.Projects.Any(_ => _.SourceContext is not null);
        var subjects = new CritterStackSubjectResolver(hasSourceContext ? context : null, context);
        var (stores, ownership) = Marten.MartenStoreScopeDiscovery.DiscoverOwnership(context, Identity, subjects);
        facts.AddRange(stores.Facts);
        diagnostics.AddRange(stores.Diagnostics);

        foreach (var project in context.Projects)
        {
            var subjectCheckpoint = subjects.Checkpoint();
            var projectFacts = new List<GenerationFact>();
            var projectDiagnostics = new List<GenerationDiagnostic>();
            var projectPlacements = new List<CritterStackPlacementIntent>();

            var marten = Marten.MartenFacts.Discover(project, options, Identity, subjects, stores);
            projectFacts.AddRange(marten.Facts);
            projectDiagnostics.AddRange(marten.Diagnostics);
            projectPlacements.AddRange(marten.Placements ?? []);

            var wolverine = WolverineFacts.Discover(project, options, Identity, subjects, ownership, implementations);
            var documents = Marten.MartenDocumentFacts.Discover(project, Identity, subjects, marten.Documents, ownership, failureStageMethods: wolverine.FailureStageMethods);
            projectFacts.AddRange(documents.Facts);
            projectDiagnostics.AddRange(documents.Diagnostics);

            projectFacts.AddRange(wolverine.Facts);
            projectDiagnostics.AddRange(wolverine.Diagnostics);
            projectPlacements.AddRange(wolverine.Placements ?? []);

            if (subjects.HasBlockingDiagnosticsSince(subjectCheckpoint))
            {
                continue;
            }

            facts.AddRange(projectFacts);
            diagnostics.AddRange(projectDiagnostics);
            placements.AddRange(projectPlacements);
        }

        diagnostics.AddRange(subjects.Diagnostics);
        facts.AddRange(useSharedPlacement
            ? CritterStackSourcePlacement.Derive(context, options, placements, diagnostics)
            : CritterStackSourcePlacement.Compatibility(placements));

        return EnumConceptFacts.AddTo(context, new AdapterContribution
        {
            Adapter = Identity,
            Facts = facts,
            Diagnostics = diagnostics
        });
    }
}
