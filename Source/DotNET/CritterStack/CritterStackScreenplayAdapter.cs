// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Reflection;

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

    /// <inheritdoc/>
    public bool CanAnalyze(DotNetAnalysisContext context) => context.Projects.Any(project =>
        project.Compilation.GetTypeByMetadataName(WellKnownTypes.MartenStoreOptions) is not null ||
        project.Compilation.GetTypeByMetadataName(WellKnownTypes.MartenDocumentStore) is not null ||
        project.Compilation.GetTypeByMetadataName(WellKnownTypes.WolverineOptions) is not null);

    /// <inheritdoc/>
    public AdapterContribution Analyze(DotNetAnalysisContext context, DotNetAdapterOptions options) =>
        Analyze(context, options, useSharedPlacement: context.Projects.Any(_ => _.SourceContext is not null));

    internal AdapterContribution AnalyzeCompatibility(DotNetAnalysisContext context, DotNetAdapterOptions options) =>
        Analyze(context, options, useSharedPlacement: false);

    AdapterContribution Analyze(
        DotNetAnalysisContext context,
        DotNetAdapterOptions options,
        bool useSharedPlacement)
    {
        var facts = new List<GenerationFact>();
        var diagnostics = new List<GenerationDiagnostic>();
        var placements = new List<CritterStackPlacementIntent>();
        var hasSourceContext = context.Projects.Any(_ => _.SourceContext is not null);
        var subjects = new CritterStackSubjectResolver(hasSourceContext ? context : null);

        foreach (var project in context.Projects)
        {
            var subjectCheckpoint = subjects.Checkpoint();
            var projectFacts = new List<GenerationFact>();
            var projectDiagnostics = new List<GenerationDiagnostic>();
            var projectPlacements = new List<CritterStackPlacementIntent>();

            var marten = Marten.MartenFacts.Discover(project, options, Identity, subjects);
            projectFacts.AddRange(marten.Facts);
            projectDiagnostics.AddRange(marten.Diagnostics);
            projectPlacements.AddRange(marten.Placements ?? []);

            var documents = Marten.MartenDocumentFacts.Discover(project, Identity, subjects, marten.Documents);
            projectFacts.AddRange(documents.Facts);
            projectDiagnostics.AddRange(documents.Diagnostics);

            var wolverine = Wolverine.WolverineFacts.Discover(project, options, Identity, subjects);
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

        return new()
        {
            Adapter = Identity,
            Facts = facts,
            Diagnostics = diagnostics
        };
    }
}
