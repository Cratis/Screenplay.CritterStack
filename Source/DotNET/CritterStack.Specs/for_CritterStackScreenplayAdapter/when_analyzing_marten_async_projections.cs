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
    [Fact] void should_emit_the_exact_event_projection() => Artifacts(ArtifactKind.Projection).Select(NameOf).ShouldContainOnly("DistanceProjection");
    [Fact] void should_emit_each_exact_event_projection_document_target() => Artifacts(ArtifactKind.Document).Select(NameOf).ShouldContainOnly("Distance", "TripSummary", "TripIndex", "ActiveTrip", "TripArchive", "TripCleanup");
    [Fact] void should_not_treat_the_create_return_as_an_event() => Artifacts(ArtifactKind.Event).Select(NameOf).ShouldNotContain("Distance");
    [Fact] void should_not_emit_a_teardown_only_document_target() => Artifacts(ArtifactKind.Document).Select(NameOf).ShouldNotContain("TeardownOnly");
    [Fact] void should_not_infer_an_event_mapping_for_an_arbitrary_helper() => Artifacts(ArtifactKind.Document).Select(NameOf).ShouldNotContain("HiddenDocument");
    [Fact] void should_preserve_both_consumed_event_types() => TargetsFromDistanceProjection(RelationshipKind.Consumes, ArtifactKind.Event).ShouldContainOnly("Travel", "TripEnded");
    [Fact] void should_build_every_exact_document_target() => TargetsFromDistanceProjection(RelationshipKind.Builds, ArtifactKind.Document).ShouldContainOnly("Distance", "TripSummary", "TripIndex", "ActiveTrip", "TripArchive", "TripCleanup");
    [Fact] void should_preserve_create_store_and_insert_as_store_operations() => TargetsFromDistanceProjection(RelationshipKind.Stores, ArtifactKind.Document).ShouldContainOnly("Distance", "TripSummary", "TripIndex");
    [Fact] void should_preserve_the_update_operation() => TargetsFromDistanceProjection(RelationshipKind.Updates, ArtifactKind.Document).ShouldContainOnly("ActiveTrip");
    [Fact] void should_preserve_delete_and_delete_where_operations() => TargetsFromDistanceProjection(RelationshipKind.Deletes, ArtifactKind.Document).ShouldContainOnly("TripArchive", "TripCleanup");
    [Fact] void should_not_fabricate_a_distance_read_model() => ReadModels.Select(NameOf).ShouldNotContain("Distance");
    [Fact] void should_report_each_omitted_non_inline_lifecycle() => Contribution.Diagnostics.Count(_ => _.Code == MartenDiagnosticCodes.ProjectionLifecycleOmitted).ShouldEqual(5);
    [Fact] void should_report_the_multi_stream_grouping_gap() => Contribution.Diagnostics.Count(_ => _.Code == MartenDiagnosticCodes.MultiStreamGroupingOmitted).ShouldEqual(1);
    [Fact] void should_report_arbitrary_event_projection_value_flow_as_loss() => Contribution.Diagnostics.Single(_ => _.Code == MartenDiagnosticCodes.EventProjectionOmitted).Message.ShouldContain("arbitrary document body, value, and predicate flow");
    [Fact] void should_report_each_ordinary_document_state_gap() => Contribution.Diagnostics.Count(_ => _.Code == MartenDiagnosticCodes.DocumentModelOmitted).ShouldEqual(6);

    IReadOnlyList<ResolvedArtifact> ReadModels => Artifacts(ArtifactKind.ReadModel);

    IReadOnlyList<ResolvedArtifact> Artifacts(ArtifactKind kind) => [.. _graph.Artifacts.Where(_ => _.Key.Kind == kind)];

    IReadOnlyList<string> TargetsFromDistanceProjection(RelationshipKind kind, ArtifactKind targetKind)
    {
        var projection = Artifacts(ArtifactKind.Projection).Single(_ => NameOf(_) == "DistanceProjection");
        var targets = _graph.Relationships
            .Where(_ => _.Key.Kind == kind && _.Key.Source == projection.Key.Subject)
            .Select(_ => _.Key.Target)
            .ToHashSet();

        return
        [
            .. Artifacts(targetKind)
                .Where(_ => targets.Contains(_.Key.Subject))
                .Select(NameOf)
        ];
    }

    static string NameOf(ResolvedArtifact artifact) => artifact.Variants.Single().Definition.Name;

    ArtifactFact Reducer(string name) => Contribution.Facts
        .OfType<ArtifactFact>()
        .Single(_ => _.Definition.Key.Kind == ArtifactKind.Reducer && _.Definition.Name == name);
}
