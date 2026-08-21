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
    public AdapterContribution Analyze(DotNetAnalysisContext context, DotNetAdapterOptions options)
    {
        var facts = new List<GenerationFact>();
        var diagnostics = new List<GenerationDiagnostic>();

        foreach (var project in context.Projects)
        {
            var marten = Marten.MartenFacts.Discover(project, options, Identity);
            facts.AddRange(marten.Facts);
            diagnostics.AddRange(marten.Diagnostics);

            var documents = Marten.MartenDocumentFacts.Discover(project, Identity);
            facts.AddRange(documents.Facts);
            diagnostics.AddRange(documents.Diagnostics);

            var wolverine = Wolverine.WolverineFacts.Discover(project, options, Identity);
            facts.AddRange(wolverine.Facts);
            diagnostics.AddRange(wolverine.Diagnostics);
        }

        return new()
        {
            Adapter = Identity,
            Facts = facts,
            Diagnostics = diagnostics
        };
    }
}
