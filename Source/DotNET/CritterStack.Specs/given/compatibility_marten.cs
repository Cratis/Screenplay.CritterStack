// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Cratis.CritterStack.Screenplay.given;

/// <summary>Exercises the compatibility source readers without the atomic adapter's granular document path.</summary>
static class compatibility_marten
{
    internal static AdapterContribution Analyze(DotNetAnalysisContext context, DotNetAdapterOptions options)
    {
        var adapter = new MartenScreenplayAdapter().Identity;
        var subjects = new CritterStackSubjectResolver(context);
        var (stores, ownership) = MartenStoreScopeDiscovery.DiscoverOwnership(context, adapter, subjects);
        List<GenerationFact> facts = [.. stores.Facts];
        List<GenerationDiagnostic> diagnostics = [.. stores.Diagnostics];
        List<CritterStackPlacementIntent> placements = [];
        foreach (var project in context.Projects)
        {
            var checkpoint = subjects.Checkpoint();
            var marten = MartenFacts.Discover(project, options, adapter, subjects, stores);
            var documents = MartenDocumentFacts.Discover(project, adapter, subjects, marten.Documents, ownership, includeIntegrationApis: false);
            if (subjects.HasBlockingDiagnosticsSince(checkpoint)) continue;
            facts.AddRange(marten.Facts);
            facts.AddRange(documents.Facts);
            diagnostics.AddRange(marten.Diagnostics);
            diagnostics.AddRange(documents.Diagnostics);
            placements.AddRange(marten.Placements ?? []);
        }

        diagnostics.AddRange(subjects.Diagnostics);
        facts.AddRange(CritterStackSourcePlacement.Derive(context, options, placements, diagnostics));
        return EnumConceptFacts.AddTo(context, new() { Adapter = adapter, Facts = facts, Diagnostics = diagnostics });
    }
}
