// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Cratis.CritterStack.Screenplay.for_CritterStackScreenplayGenerator;

public class when_generating_a_wolverine_marten_application : given.a_wolverine_marten_application
{
    GeneratedScreenplayDefinition _result = null!;

    void Because() => _result = new CritterStackScreenplayGenerator().Generate(
        [Project],
        new CritterStackScreenplayOptions { Domain = "Helpdesk" });

    [Fact] void should_succeed() => _result.IsSuccess.ShouldBeTrue();
    [Fact] void should_generate_log_incident() => _result.Source.ShouldContain("command LogIncident");
    [Fact] void should_generate_categorise_incident() => _result.Source.ShouldContain("command CategoriseIncident");
    [Fact] void should_generate_close_incident() => _result.Source.ShouldContain("command CloseIncident");
    [Fact] void should_generate_archive_incident() => _result.Source.ShouldContain("command ArchiveIncident");
    [Fact] void should_generate_the_plain_response_endpoint() => _result.Source.ShouldContain("command CheckIncident");
    [Fact] void should_generate_the_direct_append_handler() => _result.Source.ShouldContain("command AppendIncidentNote");
    [Fact] void should_generate_the_explicit_current_handler() => _result.Source.ShouldContain("command ExplicitCommand");
    [Fact] void should_ignore_a_handler_marked_with_the_current_ignore_attribute() => _result.Source.ShouldNotContain("command IgnoredCommand");
    [Fact] void should_ignore_a_method_marked_with_the_current_ignore_attribute() => _result.Source.ShouldNotContain("command MethodIgnoredCommand");
    [Fact] void should_not_activate_an_open_generic_handler() => _result.Source.ShouldNotContain("command GenericCommand");
    [Fact] void should_not_activate_an_abstract_handler() => _result.Source.ShouldNotContain("command AbstractCommand");
    [Fact] void should_generate_the_logged_event() => _result.Source.ShouldContain("event IncidentLogged");
    [Fact] void should_generate_the_categorised_event() => _result.Source.ShouldContain("event IncidentCategorised");
    [Fact] void should_generate_the_closed_event() => _result.Source.ShouldContain("event IncidentClosed");
    [Fact] void should_generate_the_external_archived_event() => _result.Source.ShouldContain("event Archived");
    [Fact] void should_generate_the_directly_appended_event() => _result.Source.ShouldContain("event IncidentNoteAppended");
    [Fact] void should_generate_the_explicit_handler_event() => _result.Source.ShouldContain("event ExplicitEvent");
    [Fact] void should_not_treat_the_direct_append_handler_return_as_an_event() => _result.Source.ShouldNotContain("event NotifyIncidentNote");
    [Fact] void should_not_generate_the_ignored_handler_event() => _result.Source.ShouldNotContain("event IgnoredEvent");
    [Fact] void should_not_generate_the_ignored_method_event() => _result.Source.ShouldNotContain("event MethodIgnoredEvent");
    [Fact] void should_not_generate_the_generic_handler_event() => _result.Source.ShouldNotContain("event GenericEvent");
    [Fact] void should_not_generate_the_abstract_handler_event() => _result.Source.ShouldNotContain("event AbstractEvent");
    [Fact] void should_not_treat_updated_aggregate_as_an_event() => _result.Source.ShouldNotContain("event UpdatedAggregate");
    [Fact] void should_not_treat_a_current_side_effect_as_an_event() => _result.Source.ShouldNotContain("event AuditEffect");
    [Fact] void should_not_treat_a_plain_http_response_as_an_event() => _result.Source.ShouldNotContain("event CheckIncidentResponse");
    [Fact] void should_not_treat_a_directly_sent_message_as_an_event() => _result.Source.ShouldNotContain("event SendIncidentNotification");
    [Fact] void should_not_treat_a_directly_published_message_as_an_event() => _result.Source.ShouldNotContain("event PublishIncidentNotification");
    [Fact] void should_not_treat_a_request_reply_message_as_an_event() => _result.Source.ShouldNotContain("event RequestIncidentStatus");
    [Fact] void should_not_treat_a_scheduled_message_as_an_event() => _result.Source.ShouldNotContain("event ScheduleIncidentReview");
    [Fact] void should_not_treat_an_unrelated_bus_message_as_an_event() => _result.Source.ShouldNotContain("event UnrelatedBusMessage");
    [Fact] void should_not_invent_a_command_for_a_pure_automation_trigger() => _result.Source.ShouldNotContain("command IncidentEscalated");
    [Fact] void should_not_treat_a_pure_automation_message_as_an_event() => _result.Source.ShouldNotContain("event NotifyEscalation");
    [Fact] void should_record_a_pure_bus_handler_as_a_reaction() => _result.Graph.Artifacts.Any(_ => _.Key.Kind == ArtifactKind.Reaction).ShouldBeTrue();
    [Fact] void should_record_the_pure_automation_trigger_as_a_message() => _result.Graph.Artifacts.Any(_ => _.Key.Kind == ArtifactKind.Message && _.Key.Subject.Value.EndsWith("/IncidentService.IncidentEscalated", StringComparison.Ordinal)).ShouldBeTrue();
    [Fact] void should_generate_get_incident_as_a_query() => _result.Source.ShouldContain("query GetIncident => Incident?");
    [Fact] void should_record_the_document_delete() => _result.Graph.Relationships.Any(_ => _.Key.Kind == RelationshipKind.Deletes).ShouldBeTrue();
    [Fact] void should_record_the_outgoing_message() => _result.Graph.Relationships.Any(_ => _.Key.Kind == RelationshipKind.Cascades).ShouldBeTrue();
    [Fact] void should_record_the_direct_handler_return_as_a_cascade() => _result.Graph.Relationships.Any(_ => _.Key.Kind == RelationshipKind.Cascades && _.Key.Target.Value.EndsWith("/IncidentService.NotifyIncidentNote", StringComparison.Ordinal)).ShouldBeTrue();
    [Fact] void should_not_record_the_plain_http_response_as_a_cascade() => _result.Graph.Relationships.Any(_ => _.Key.Kind == RelationshipKind.Cascades && _.Key.Target.Value.EndsWith("/IncidentService.CheckIncidentResponse", StringComparison.Ordinal)).ShouldBeFalse();
    [Fact] void should_record_direct_send_separately() => _result.Graph.Relationships.Any(_ => _.Key.Kind == RelationshipKind.Publishes && _.Key.Discriminator == "send").ShouldBeTrue();
    [Fact] void should_record_direct_publish_separately() => _result.Graph.Relationships.Any(_ => _.Key.Kind == RelationshipKind.Publishes && _.Key.Discriminator == "publish").ShouldBeTrue();
    [Fact] void should_record_request_reply_separately() => _result.Graph.Relationships.Any(_ => _.Key.Kind == RelationshipKind.Publishes && _.Key.Discriminator == "request-reply").ShouldBeTrue();
    [Fact] void should_record_scheduling_separately() => _result.Graph.Relationships.Any(_ => _.Key.Kind == RelationshipKind.Publishes && _.Key.Discriminator == "scheduled").ShouldBeTrue();
    [Fact] void should_record_delivery_option_scheduling_separately() => _result.Graph.Relationships.Any(_ => _.Key.Kind == RelationshipKind.Publishes && _.Key.Discriminator == "scheduled-publish").ShouldBeTrue();
    [Fact] void should_record_topic_broadcast_separately() => _result.Graph.Relationships.Any(_ => _.Key.Kind == RelationshipKind.Publishes && _.Key.Discriminator == "broadcast-topic").ShouldBeTrue();
    [Fact] void should_ignore_an_unrelated_send_method() => _result.Graph.Relationships.Any(_ => _.Key.Kind == RelationshipKind.Publishes && _.Key.Target.Value.EndsWith("/IncidentService.UnrelatedBusMessage", StringComparison.Ordinal)).ShouldBeFalse();
    [Fact] void should_record_the_pure_automation_handler_relationship() => _result.Graph.Relationships.Any(_ => _.Key.Kind == RelationshipKind.Handles && _.Key.Target.Value.EndsWith("/IncidentService.IncidentEscalated", StringComparison.Ordinal)).ShouldBeTrue();
    [Fact] void should_record_the_pure_automation_publish() => _result.Graph.Relationships.Any(_ => _.Key.Kind == RelationshipKind.Publishes && _.Key.Target.Value.EndsWith("/IncidentService.NotifyEscalation", StringComparison.Ordinal)).ShouldBeTrue();
    [Fact] void should_report_delayed_delivery_as_language_loss() => _result.Diagnostics.Select(_ => _.Code).ShouldContain(WolverineDiagnosticCodes.DelayedMessageOmitted);
    [Fact] void should_report_direct_delivery_as_language_loss() => _result.Diagnostics.Select(_ => _.Code).ShouldContain("WOLVERINE0006");
    [Fact] void should_report_reaction_lowering_as_language_loss() => _result.Diagnostics.Select(_ => _.Code).ShouldContain("GEN0004");
    [Fact] void should_report_http_metadata_as_language_loss() => _result.Diagnostics.Select(_ => _.Code).ShouldContain(WolverineDiagnosticCodes.HttpMetadataOmitted);
    [Fact] void should_report_route_identity_as_language_loss() => _result.Diagnostics.Select(_ => _.Code).ShouldContain(WolverineDiagnosticCodes.RouteIdentityOmitted);
    [Fact] void should_report_stream_version_as_language_loss() => _result.Diagnostics.Select(_ => _.Code).ShouldContain(WolverineDiagnosticCodes.StreamVersionOmitted);
    [Fact] void should_use_project_qualified_subject_ids() => _result.Graph.Artifacts.All(_ => _.Key.Subject.Value.StartsWith("dotnet://IncidentService/", StringComparison.Ordinal)).ShouldBeTrue();
}
