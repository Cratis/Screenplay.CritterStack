// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Cratis.CritterStack.Screenplay.for_CritterStackScreenplayAdapter;

public class when_analyzing_marten_async_projections : given.a_marten_async_projection_application
{
    ResolvedApplicationGraph _graph = null!;

    void Because() => _graph = new GenerationResolver().Resolve([Contribution]);

    [Fact] void should_use_configured_evidence_for_instance_registrations() => Reducer("DayProjection").Evidence.Strength.ShouldEqual(EvidenceStrength.Configured);
    [Fact] void should_use_configured_evidence_for_generic_registrations() => Reducer("TripProjection").Evidence.Strength.ShouldEqual(EvidenceStrength.Configured);
    [Fact] void should_use_configured_evidence_for_snapshot_registrations() => Reducer("JournalSnapshot").Evidence.Strength.ShouldEqual(EvidenceStrength.Configured);
    [Fact] void should_use_configured_evidence_for_live_registrations() => Reducer("LiveJournalSnapshot").Evidence.Strength.ShouldEqual(EvidenceStrength.Configured);
    [Fact] void should_discover_the_self_aggregating_snapshot() => ReadModels.Select(_ => _.Variants.Single().Definition.Name).ShouldContain("Journal");
    [Fact] void should_discover_the_live_aggregation() => ReadModels.Select(_ => _.Variants.Single().Definition.Name).ShouldContain("LiveJournal");
    [Fact] void should_discover_the_single_stream_projection() => ReadModels.Select(_ => _.Variants.Single().Definition.Name).ShouldContain("Trip");
    [Fact] void should_discover_the_multi_stream_projection() => ReadModels.Select(_ => _.Variants.Single().Definition.Name).ShouldContain("Day");
    [Fact] void should_report_each_omitted_non_inline_lifecycle() => Contribution.Diagnostics.Count(_ => _.Code == MartenDiagnosticCodes.ProjectionLifecycleOmitted).ShouldEqual(5);
    [Fact] void should_report_the_multi_stream_grouping_gap() => Contribution.Diagnostics.Count(_ => _.Code == MartenDiagnosticCodes.MultiStreamGroupingOmitted).ShouldEqual(1);
    [Fact] void should_report_the_event_projection_gap() => Contribution.Diagnostics.Count(_ => _.Code == MartenDiagnosticCodes.EventProjectionOmitted).ShouldEqual(1);

    IReadOnlyList<ResolvedArtifact> ReadModels => [.. _graph.Artifacts.Where(_ => _.Key.Kind == ArtifactKind.ReadModel)];

    ArtifactFact Reducer(string name) => Contribution.Facts
        .OfType<ArtifactFact>()
        .Single(_ => _.Definition.Key.Kind == ArtifactKind.Reducer && _.Definition.Name == name);
}
