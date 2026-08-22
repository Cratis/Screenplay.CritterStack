// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Cratis.CritterStack.Screenplay.for_CritterStackScreenplayAdapter;

public class when_analyzing_marten_projection_and_subscription_configuration : given.a_marten_projection_metadata_application
{
    static readonly HashSet<string> _evidenceDiagnosticCodes =
    [
        MartenDiagnosticCodes.ProjectionMetadataOmitted,
        MartenDiagnosticCodes.DaemonConfigurationOmitted,
        MartenDiagnosticCodes.SubscriptionConfigurationOmitted,
        MartenDiagnosticCodes.CustomProcessingOmitted
    ];

    [Fact] void should_preserve_a_raw_custom_projection_as_a_neutral_projection() => ProjectionNames.ShouldContain("RawProjection");
    [Fact] void should_preserve_a_service_resolved_projection_as_a_neutral_projection() => ProjectionNames.ShouldContain("ServiceProjection");
    [Fact] void should_not_turn_raw_custom_projections_into_reducers() => ArtifactNames(ArtifactKind.Reducer).ShouldNotContain("RawProjection");
    [Fact] void should_not_place_raw_custom_projections_in_a_slice() => Graph.Placements.Any(_ => _.Artifact.Subject == ProjectionSubject("RawProjection")).ShouldBeFalse();
    [Fact] void should_not_link_arbitrary_custom_projection_consequences() => RelationshipsFrom("RawProjection").ShouldBeEmpty();
    [Fact] void should_preserve_the_exact_projection_name() => ProjectionMetadata.Any(_ => _.Message.Contains("daemon name 'orders-summary'", StringComparison.Ordinal)).ShouldBeTrue();
    [Fact] void should_preserve_the_exact_projection_version() => ProjectionMetadata.Any(_ => _.Message.Contains("daemon version '3'", StringComparison.Ordinal)).ShouldBeTrue();
    [Fact] void should_preserve_the_exact_registration_name() => ProjectionMetadata.Any(_ => _.Message.Contains("registers daemon name 'raw-projection'", StringComparison.Ordinal)).ShouldBeTrue();
    [Fact] void should_preserve_service_registration_metadata() => ProjectionMetadata.Count(_ => _.Message.Contains("ServiceProjection", StringComparison.Ordinal) && (_.Message.Contains("service-projection", StringComparison.Ordinal) || _.Message.Contains("version '2'", StringComparison.Ordinal))).ShouldEqual(2);
    [Fact] void should_preserve_exact_non_inline_lifecycle_registration() => Contribution.Diagnostics.Count(_ => _.Code == MartenDiagnosticCodes.ProjectionLifecycleOmitted && (_.Message.Contains("OrderSummaryProjection", StringComparison.Ordinal) || _.Message.Contains("ServiceProjection", StringComparison.Ordinal))).ShouldEqual(2);
    [Fact] void should_preserve_exact_daemon_mode() => DaemonConfiguration.Any(_ => _.Message.Contains("mode 'Solo'", StringComparison.Ordinal)).ShouldBeTrue();
    [Fact] void should_preserve_subscription_registrations() => SubscriptionConfiguration.Count(_ => _.Message.Contains("is registered", StringComparison.Ordinal)).ShouldEqual(4);
    [Fact] void should_preserve_subscription_name_and_version() => SubscriptionConfiguration.Count(_ => _.Message.Contains("InvoiceSubscription", StringComparison.Ordinal) && (_.Message.Contains("name 'invoices'", StringComparison.Ordinal) || _.Message.Contains("version '4'", StringComparison.Ordinal))).ShouldEqual(2);
    [Fact] void should_preserve_the_archived_event_policy() => SubscriptionConfiguration.Any(_ => _.Message.Contains("InvoiceSubscription", StringComparison.Ordinal) && _.Message.Contains("archived-event policy 'true'", StringComparison.Ordinal)).ShouldBeTrue();
    [Fact] void should_preserve_event_type_filters_as_configuration_only() => SubscriptionConfiguration.Any(_ => _.Message.Contains("event-type filter for 'OrderOpened'", StringComparison.Ordinal)).ShouldBeTrue();
    [Fact] void should_preserve_stream_type_filters_as_configuration_only() => SubscriptionConfiguration.Any(_ => _.Message.Contains("stream-type filter for 'StreamMarker'", StringComparison.Ordinal)).ShouldBeTrue();
    [Fact] void should_preserve_exact_sequence_start() => SubscriptionConfiguration.Any(_ => _.Message.Contains("starting sequence '42' for database 'blue'", StringComparison.Ordinal)).ShouldBeTrue();
    [Fact] void should_preserve_exact_present_start() => SubscriptionConfiguration.Any(_ => _.Message.Contains("starting position 'present'", StringComparison.Ordinal)).ShouldBeTrue();
    [Fact] void should_not_classify_a_filter_type_as_an_event() => ArtifactNames(ArtifactKind.Event).ShouldNotContain("StreamMarker");
    [Fact] void should_not_classify_subscription_document_operations() => ArtifactNames(ArtifactKind.Document).ShouldNotContain("HiddenDocument");
    [Fact] void should_not_infer_subscription_automations() => ArtifactNames(ArtifactKind.Reaction).ShouldBeEmpty();
    [Fact] void should_not_infer_subscription_messages() => ArtifactNames(ArtifactKind.Message).ShouldBeEmpty();
    [Fact] void should_not_infer_subscription_handlers() => ArtifactNames(ArtifactKind.Handler).ShouldBeEmpty();
    [Fact] void should_report_arbitrary_processing_without_inference() => Contribution.Diagnostics.Count(_ => _.Code == MartenDiagnosticCodes.CustomProcessingOmitted).ShouldBeGreaterThan(3);
    [Fact] void should_anchor_configuration_evidence_in_authored_source() => Contribution.Diagnostics.Where(_ => _evidenceDiagnosticCodes.Contains(_.Code)).All(_ => _.Source?.Path == "Orders/Configuration.cs").ShouldBeTrue();

    IReadOnlyList<string> ProjectionNames => ArtifactNames(ArtifactKind.Projection);
    IReadOnlyList<GenerationDiagnostic> ProjectionMetadata => [.. Contribution.Diagnostics.Where(_ => _.Code == MartenDiagnosticCodes.ProjectionMetadataOmitted)];
    IReadOnlyList<GenerationDiagnostic> DaemonConfiguration => [.. Contribution.Diagnostics.Where(_ => _.Code == MartenDiagnosticCodes.DaemonConfigurationOmitted)];
    IReadOnlyList<GenerationDiagnostic> SubscriptionConfiguration => [.. Contribution.Diagnostics.Where(_ => _.Code == MartenDiagnosticCodes.SubscriptionConfigurationOmitted)];

    IReadOnlyList<string> ArtifactNames(ArtifactKind kind) =>
    [
        .. Graph.Artifacts
            .Where(_ => _.Key.Kind == kind)
            .SelectMany(_ => _.Variants)
            .Select(_ => _.Definition.Name)
    ];

    IReadOnlyList<ResolvedRelationship> RelationshipsFrom(string artifactName) =>
        [.. Graph.Relationships.Where(_ => _.Key.Source == ProjectionSubject(artifactName))];

    SubjectId ProjectionSubject(string artifactName) => Graph.Artifacts
        .Single(_ => _.Key.Kind == ArtifactKind.Projection && _.Variants.Any(variant => variant.Definition.Name == artifactName))
        .Key.Subject;
}
