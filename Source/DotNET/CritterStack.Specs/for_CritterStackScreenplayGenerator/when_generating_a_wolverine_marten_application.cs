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
    [Fact] void should_record_a_single_handler_return_as_a_reaction() => _result.Graph.Artifacts.Any(_ => _.Key.Kind == ArtifactKind.Reaction && _.Variants[0].Definition.Name == "ReturnOnly").ShouldBeTrue();
    [Fact] void should_record_the_return_only_trigger_as_a_message() => _result.Graph.Artifacts.Any(_ => _.Key.Kind == ArtifactKind.Message && _.Key.Subject.Value.EndsWith("/IncidentService.ReturnOnlyTrigger", StringComparison.Ordinal)).ShouldBeTrue();
    [Fact] void should_record_the_return_only_handler_relationship() => _result.Graph.Relationships.Any(_ => _.Key.Kind == RelationshipKind.Handles && _.Key.Target.Value.EndsWith("/IncidentService.ReturnOnlyTrigger", StringComparison.Ordinal)).ShouldBeTrue();
    [Fact] void should_record_the_outgoing_return_handler_as_a_reaction() => _result.Graph.Artifacts.Any(_ => _.Key.Kind == ArtifactKind.Reaction && _.Variants[0].Definition.Name == "OutgoingReturn").ShouldBeTrue();
    [Fact] void should_record_the_outgoing_return_handler_relationship() => _result.Graph.Relationships.Any(_ => _.Key.Kind == RelationshipKind.Handles && _.Key.Target.Value.EndsWith("/IncidentService.OutgoingReturnTrigger", StringComparison.Ordinal)).ShouldBeTrue();
    [Fact] void should_record_the_event_looking_return_as_a_message() => _result.Graph.Artifacts.Any(_ => _.Key.Kind == ArtifactKind.Message && _.Key.Subject.Value.EndsWith("/IncidentService.ReturnOnlyEventHappened", StringComparison.Ordinal)).ShouldBeTrue();
    [Fact] void should_record_the_single_return_cascade_once() => _result.Graph.Relationships.Count(_ => _.Key.Kind == RelationshipKind.Cascades && _.Key.Target.Value.EndsWith("/IncidentService.ReturnOnlyEventHappened", StringComparison.Ordinal)).ShouldEqual(1);
    [Fact] void should_record_the_first_tuple_return_as_a_cascade() => _result.Graph.Relationships.Any(_ => _.Key.Kind == RelationshipKind.Cascades && _.Key.Target.Value.EndsWith("/IncidentService.FirstTupleCascade", StringComparison.Ordinal) && _.Key.Discriminator == "return-slot:0").ShouldBeTrue();
    [Fact] void should_record_the_second_tuple_return_as_a_cascade() => _result.Graph.Relationships.Any(_ => _.Key.Kind == RelationshipKind.Cascades && _.Key.Target.Value.EndsWith("/IncidentService.SecondTupleCascade", StringComparison.Ordinal) && _.Key.Discriminator == "return-slot:1").ShouldBeTrue();
    [Fact] void should_record_immediate_outgoing_messages_as_cascades() => _result.Graph.Relationships.Any(_ => _.Key.Kind == RelationshipKind.Cascades && _.Key.Target.Value.EndsWith("/IncidentService.ImmediateOutgoingCascade", StringComparison.Ordinal) && _.Key.Discriminator == "immediate").ShouldBeTrue();
    [Fact] void should_record_delayed_outgoing_messages_as_cascades() => _result.Graph.Relationships.Any(_ => _.Key.Kind == RelationshipKind.Cascades && _.Key.Target.Value.EndsWith("/IncidentService.DelayedOutgoingCascade", StringComparison.Ordinal) && _.Key.Discriminator == "delayed").ShouldBeTrue();
    [Fact] void should_preserve_direct_bus_consequences_with_return_cascades() => _result.Graph.Relationships.Any(_ => _.Key.Kind == RelationshipKind.Publishes && _.Key.Target.Value.EndsWith("/IncidentService.MixedPublishedMessage", StringComparison.Ordinal) && _.Key.Discriminator == "publish").ShouldBeTrue();
    [Fact] void should_record_return_cascades_with_direct_bus_consequences() => _result.Graph.Relationships.Any(_ => _.Key.Kind == RelationshipKind.Cascades && _.Key.Target.Value.EndsWith("/IncidentService.MixedReturnedCascade", StringComparison.Ordinal)).ShouldBeTrue();
    [Fact] void should_exclude_response_and_side_effect_slots_while_retaining_cascades() => _result.Graph.Relationships.Any(_ => _.Key.Kind == RelationshipKind.Cascades && _.Key.Target.Value.EndsWith("/IncidentService.CascadeAfterExcludedSlots", StringComparison.Ordinal) && _.Key.Discriminator == "return-slot:2").ShouldBeTrue();
    [Fact] void should_not_activate_a_response_only_handler() => _result.Graph.Artifacts.Any(_ => _.Key.Kind == ArtifactKind.Message && _.Key.Subject.Value.EndsWith("/IncidentService.ResponseOnlyTrigger", StringComparison.Ordinal)).ShouldBeFalse();
    [Fact] void should_not_activate_a_side_effect_only_handler() => _result.Graph.Artifacts.Any(_ => _.Key.Kind == ArtifactKind.Message && _.Key.Subject.Value.EndsWith("/IncidentService.SideEffectOnlyTrigger", StringComparison.Ordinal)).ShouldBeFalse();
    [Fact] void should_not_activate_a_current_return_wrapper_only_handler() => _result.Graph.Artifacts.Any(_ => _.Key.Kind == ArtifactKind.Message && _.Key.Subject.Value.EndsWith("/IncidentService.CurrentWrapperOnlyTrigger", StringComparison.Ordinal)).ShouldBeFalse();
    [Fact] void should_not_activate_a_legacy_return_wrapper_only_handler() => _result.Graph.Artifacts.Any(_ => _.Key.Kind == ArtifactKind.Message && _.Key.Subject.Value.EndsWith("/IncidentService.LegacyWrapperOnlyTrigger", StringComparison.Ordinal)).ShouldBeFalse();
    [Fact] void should_not_activate_a_current_persistence_wrapper_only_handler() => _result.Graph.Artifacts.Any(_ => _.Key.Kind == ArtifactKind.Message && _.Key.Subject.Value.EndsWith("/IncidentService.PersistenceWrapperOnlyTrigger", StringComparison.Ordinal)).ShouldBeFalse();
    [Fact] void should_not_activate_a_legacy_persistence_wrapper_only_handler() => _result.Graph.Artifacts.Any(_ => _.Key.Kind == ArtifactKind.Message && _.Key.Subject.Value.EndsWith("/IncidentService.LegacyPersistenceWrapperOnlyTrigger", StringComparison.Ordinal)).ShouldBeFalse();
    [Fact] void should_not_activate_a_return_automation_with_document_persistence() => _result.Graph.Artifacts.Any(_ => _.Key.Kind == ArtifactKind.Message && _.Key.Subject.Value.EndsWith("/IncidentService.StoreAndReturnTrigger", StringComparison.Ordinal)).ShouldBeFalse();
    [Fact] void should_not_cascade_a_return_with_document_persistence() => _result.Graph.Relationships.Any(_ => _.Key.Kind == RelationshipKind.Cascades && _.Key.Target.Value.EndsWith("/IncidentService.StoreReturnCascade", StringComparison.Ordinal)).ShouldBeFalse();
    [Fact] void should_recognize_the_exact_current_handler_attribute() => _result.Graph.Artifacts.Any(_ => _.Key.Kind == ArtifactKind.Reaction && _.Variants[0].Definition.Name == "CurrentExplicitReturnActions").ShouldBeTrue();
    [Fact] void should_recognize_the_exact_legacy_handler_attribute() => _result.Graph.Artifacts.Any(_ => _.Key.Kind == ArtifactKind.Reaction && _.Variants[0].Definition.Name == "LegacyExplicitActions").ShouldBeTrue();
    [Fact] void should_not_activate_an_ignored_return_handler() => _result.Graph.Artifacts.Any(_ => _.Key.Kind == ArtifactKind.Message && _.Key.Subject.Value.EndsWith("/IncidentService.IgnoredReturnTrigger", StringComparison.Ordinal)).ShouldBeFalse();
    [Fact] void should_not_activate_an_open_generic_return_handler() => _result.Graph.Artifacts.Any(_ => _.Key.Kind == ArtifactKind.Message && _.Key.Subject.Value.EndsWith("/IncidentService.GenericReturnTrigger", StringComparison.Ordinal)).ShouldBeFalse();
    [Fact] void should_not_activate_an_abstract_return_handler() => _result.Graph.Artifacts.Any(_ => _.Key.Kind == ArtifactKind.Message && _.Key.Subject.Value.EndsWith("/IncidentService.AbstractReturnTrigger", StringComparison.Ordinal)).ShouldBeFalse();
    [Fact] void should_not_activate_an_internal_return_handler() => _result.Graph.Artifacts.Any(_ => _.Key.Kind == ArtifactKind.Message && _.Key.Subject.Value.EndsWith("/IncidentService.InternalReturnTrigger", StringComparison.Ordinal)).ShouldBeFalse();
    [Fact] void should_not_activate_an_invalid_return_method() => _result.Graph.Artifacts.Any(_ => _.Key.Kind == ArtifactKind.Message && _.Key.Subject.Value.EndsWith("/IncidentService.InvalidReturnCascade", StringComparison.Ordinal)).ShouldBeFalse();
    [Fact] void should_not_treat_compound_middleware_as_an_independent_automation() => _result.Graph.Artifacts.Any(_ => _.Key.Kind == ArtifactKind.Message && _.Key.Subject.Value.EndsWith("/IncidentService.MiddlewareTrigger", StringComparison.Ordinal)).ShouldBeFalse();
    [Fact] void should_not_invent_commands_for_return_automations() => _result.Graph.Artifacts.Any(_ => _.Key.Kind == ArtifactKind.Command && _.Key.Subject.Value.EndsWith("/IncidentService.ReturnOnlyTrigger", StringComparison.Ordinal)).ShouldBeFalse();
    [Fact] void should_not_invent_events_for_return_automations() => _result.Graph.Artifacts.Any(_ => _.Key.Kind == ArtifactKind.Event && _.Key.Subject.Value.EndsWith("/IncidentService.ReturnOnlyEventHappened", StringComparison.Ordinal)).ShouldBeFalse();
    [Fact] void should_not_produce_return_automation_messages() => _result.Graph.Relationships.Any(_ => _.Key.Kind == RelationshipKind.Produces && _.Key.Target.Value.EndsWith("/IncidentService.ReturnOnlyEventHappened", StringComparison.Ordinal)).ShouldBeFalse();
    [Fact] void should_place_return_automations_stably() => _result.Graph.Placements.Single(_ => _.Artifact.Kind == ArtifactKind.Reaction && _.Artifact.Subject == _result.Graph.Artifacts.Single(artifact => artifact.Key.Kind == ArtifactKind.Reaction && artifact.Variants[0].Definition.Name == "ReturnOnly").Key.Subject).EffectiveVariants.Single().Placement.Slice.ShouldEqual("ReturnOnly");
    [Fact] void should_place_return_automations_in_the_trigger_feature() => _result.Graph.Placements.Single(_ => _.Artifact.Kind == ArtifactKind.Reaction && _.Artifact.Subject == _result.Graph.Artifacts.Single(artifact => artifact.Key.Kind == ArtifactKind.Reaction && artifact.Variants[0].Definition.Name == "ReturnOnly").Key.Subject).EffectiveVariants.Single().Placement.Features.ShouldContain("ReturnOnlyTrigger");
    [Fact] void should_place_return_automations_as_automation_slices() => _result.Graph.Placements.Single(_ => _.Artifact.Kind == ArtifactKind.Reaction && _.Artifact.Subject == _result.Graph.Artifacts.Single(artifact => artifact.Key.Kind == ArtifactKind.Reaction && artifact.Variants[0].Definition.Name == "ReturnOnly").Key.Subject).EffectiveVariants.Single().Placement.SliceKind.ShouldEqual(GenerationSliceKind.Automation);
    [Fact] void should_report_return_reaction_lowering_as_explicit_loss() => _result.Diagnostics.Any(_ => _.Code == "GEN0004" && _.Subject == _result.Graph.Artifacts.Single(artifact => artifact.Key.Kind == ArtifactKind.Reaction && artifact.Variants[0].Definition.Name == "ReturnOnly").Key.Subject).ShouldBeTrue();
    [Fact] void should_report_outgoing_delay_loss_once_for_the_return_automation() => _result.Diagnostics.Count(_ => _.Code == WolverineDiagnosticCodes.DelayedMessageOmitted && _.Subject == _result.Graph.Artifacts.Single(artifact => artifact.Key.Kind == ArtifactKind.Reaction && artifact.Variants[0].Definition.Name == "OutgoingReturn").Key.Subject).ShouldEqual(1);
    [Fact] void should_report_delayed_delivery_as_language_loss() => _result.Diagnostics.Select(_ => _.Code).ShouldContain(WolverineDiagnosticCodes.DelayedMessageOmitted);
    [Fact] void should_report_direct_delivery_as_language_loss() => _result.Diagnostics.Select(_ => _.Code).ShouldContain("WOLVERINE0006");
    [Fact] void should_report_reaction_lowering_as_language_loss() => _result.Diagnostics.Select(_ => _.Code).ShouldContain("GEN0004");
    [Fact] void should_report_http_metadata_as_language_loss() => _result.Diagnostics.Select(_ => _.Code).ShouldContain(WolverineDiagnosticCodes.HttpMetadataOmitted);
    [Fact] void should_report_route_identity_as_language_loss() => _result.Diagnostics.Select(_ => _.Code).ShouldContain(WolverineDiagnosticCodes.RouteIdentityOmitted);
    [Fact] void should_report_stream_version_as_language_loss() => _result.Diagnostics.Select(_ => _.Code).ShouldContain(WolverineDiagnosticCodes.StreamVersionOmitted);
    [Fact] void should_use_project_qualified_subject_ids() => _result.Graph.Artifacts.All(_ => _.Key.Subject.Value.StartsWith("dotnet://IncidentService/", StringComparison.Ordinal)).ShouldBeTrue();
}
