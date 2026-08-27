// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Cratis.CritterStack.Screenplay.for_CritterStackScreenplayGenerator;

public class when_generating_targeted_wolverine_event_streams : given.a_wolverine_targeted_event_stream_application
{
    GeneratedScreenplayDefinition _result = null!;

    void Because() => _result = new CritterStackScreenplayGenerator().Generate(
        [Project],
        new CritterStackScreenplayOptions { Domain = "Transfers" });

    [Fact] void should_succeed() => _result.IsSuccess.ShouldBeTrue();
    [Fact] void should_discover_append_one_for_the_first_model() => EventNames.ShouldContain("AccountDebited");
    [Fact] void should_discover_params_append_many() => EventNames.ShouldContainOnly(["AccountAdjusted", "AccountApproved", "AccountClosed", "AccountDebited", "AccountReviewed", "BoundaryEvent", "DerivedStreamEvent", "FundsDeposited", "FundsMoved", "FundsWithdrawn", "GeneratedMemberEvent", "LegacyAppended", "OrderConfirmed", "OrderCredited", "OrderPacked", "OrderShipped", "RouteAppended", "SagaSiblingAppended", "SameNamedAttributeEvent", "UnmarkedEvent"]);
    [Fact] void should_discover_a_direct_array() => EventNames.ShouldContain("AccountAdjusted");
    [Fact] void should_discover_a_collection_expression() => EventNames.ShouldContain("OrderPacked");
    [Fact] void should_discover_a_direct_collection_initializer() => EventNames.ShouldContain("AccountReviewed");
    [Fact] void should_bind_the_account_event_to_the_account_receiver() => AppendFor("Transfer", "AccountDebited").Key.Target.ShouldEqual(SubjectOf(ArtifactKind.Aggregate, "Account"));
    [Fact] void should_bind_the_order_event_to_the_order_receiver() => AppendFor("Transfer", "OrderCredited").Key.Target.ShouldEqual(SubjectOf(ArtifactKind.Aggregate, "Order"));
    [Fact] void should_not_leak_an_order_event_to_the_first_stream() => AppendsFrom("Transfer").Any(_ => _.Key.Target == SubjectOf(ArtifactKind.Aggregate, "Account") && _.Key.Discriminator?.Contains("OrderCredited", StringComparison.Ordinal) == true).ShouldBeFalse();
    [Fact] void should_not_leak_an_account_event_to_the_second_stream() => AppendsFrom("Transfer").Any(_ => _.Key.Target == SubjectOf(ArtifactKind.Aggregate, "Order") && _.Key.Discriminator?.Contains("AccountDebited", StringComparison.Ordinal) == true).ShouldBeFalse();
    [Fact] void should_retain_the_first_different_model_identity() => AppendFor("Transfer", "AccountDebited").Definitions.Single().SourceMember.ShouldEqual("accountId");
    [Fact] void should_retain_the_second_different_model_identity() => AppendFor("Transfer", "OrderCredited").Definitions.Single().SourceMember.ShouldEqual("orderId");
    [Fact] void should_retain_two_reads_for_different_models() => ReadsFrom("Transfer").Select(_ => _.Definitions.Single().SourceMember).ShouldContainOnly(["accountId", "orderId"]);
    [Fact] void should_retain_two_reads_for_the_same_model() => ReadsFrom("MoveFunds").Select(_ => _.Definitions.Single().SourceMember).ShouldContainOnly(["fromId", "toId"]);
    [Fact] void should_distinguish_same_model_reads() => ReadsFrom("MoveFunds").Select(_ => _.Key.Discriminator).Distinct().Count().ShouldEqual(2);
    [Fact] void should_bind_the_source_same_model_event() => AppendFor("MoveFunds", "FundsWithdrawn").Definitions.Single().SourceMember.ShouldEqual("fromId");
    [Fact] void should_bind_the_destination_same_model_event() => AppendFor("MoveFunds", "FundsDeposited").Definitions.Single().SourceMember.ShouldEqual("toId");
    [Fact] void should_distinguish_same_model_appends() => AppendsFrom("MoveFunds").Select(_ => _.Key.Discriminator).Distinct().Count().ShouldEqual(4);
    [Fact] void should_distinguish_the_same_event_appended_to_two_same_model_parameters() => AppendsFrom("MoveFunds").Count(_ => _.Key.Discriminator?.Contains("FundsMoved", StringComparison.Ordinal) == true).ShouldEqual(2);
    [Fact] void should_retain_both_identities_for_the_same_event_and_model() => AppendsFrom("MoveFunds").Where(_ => _.Key.Discriminator?.Contains("FundsMoved", StringComparison.Ordinal) == true).Select(_ => _.Definitions.Single().SourceMember).ShouldContainOnly(["fromId", "toId"]);
    [Fact] void should_not_globally_mark_a_different_model_stream_identity() => Command("Transfer").Variants.Single().Definition.Properties.Any(_ => _.IsIdentifier).ShouldBeFalse();
    [Fact] void should_not_globally_mark_a_same_model_stream_identity() => Command("MoveFunds").Variants.Single().Definition.Properties.Any(_ => _.IsIdentifier).ShouldBeFalse();
    [Fact] void should_create_aggregate_artifacts_for_stream_targets() => AggregateNames.ShouldContainOnly(["Account", "Order"]);
    [Fact] void should_not_invent_stream_artifacts() => _result.Graph.Artifacts.Any(_ => _.Variants.Any(variant => variant.Definition.Name.Contains("IEventStream", StringComparison.Ordinal))).ShouldBeFalse();
    [Fact] void should_not_invent_wolverine_read_models_for_stream_targets() => _result.Graph.Artifacts.Any(_ => _.Key.Kind == ArtifactKind.ReadModel && (_.Variants[0].Definition.Name == "Account" || _.Variants[0].Definition.Name == "Order")).ShouldBeFalse();
    [Fact] void should_treat_an_unmarked_stream_append_as_a_write() => AppendFor("UnmarkedAppend", "UnmarkedEvent").Key.Target.ShouldEqual(SubjectOf(ArtifactKind.Aggregate, "Account"));
    [Fact] void should_not_invent_loading_for_an_unmarked_stream() => ReadsFrom("UnmarkedAppend").ShouldBeEmpty();
    [Fact] void should_not_invent_identity_metadata_for_an_unmarked_stream() => AppendFor("UnmarkedAppend", "UnmarkedEvent").Definitions.Single().SourceMember.ShouldBeNull();
    [Fact] void should_retain_a_loaded_stream_without_a_direct_append() => ReadsFrom("InspectAccount").Single().Definitions.Single().SourceMember.ShouldEqual("accountId");
    [Fact] void should_retain_both_ambiguous_conventional_version_bindings() => ReadsFrom("ConventionalVersionCommand").Select(_ => _.Definitions.Single().SourceMember).ShouldContainOnly(["fromId", "toId"]);
    [Fact] void should_report_ambiguous_conventional_version_for_each_binding() => Diagnostics(WolverineDiagnosticCodes.StreamVersionOmitted).Count(_ => _.Message.Contains("ConventionalVersionCommand", StringComparison.Ordinal)).ShouldEqual(2);
    [Fact] void should_make_conventional_version_diagnostics_parameter_specific() => Diagnostics(WolverineDiagnosticCodes.StreamVersionOmitted).Where(_ => _.Message.Contains("ConventionalVersionCommand", StringComparison.Ordinal)).Select(_ => _.Message).ShouldContainOnly([
        "The conventional Version member for 'ConventionalVersionCommand' cannot be attributed safely to stream parameter 'source' because the handler loads multiple streams",
        "The conventional Version member for 'ConventionalVersionCommand' cannot be attributed safely to stream parameter 'destination' because the handler loads multiple streams"]);
    [Fact] void should_ignore_an_unrelated_identity_attribute() => ReadsFrom("FalseIdentityCommand").Single().Definitions.Single().SourceMember.ShouldBeNull();
    [Fact] void should_recognize_the_legacy_marten_stream_interface() => AppendFor("LegacyAppend", "LegacyAppended").Definitions.Single().SourceMember.ShouldEqual("accountId");
    [Fact] void should_recognize_legacy_write_aggregate_loading() => ReadsFrom("LegacyAppend").Single().Definitions.Single().SourceMember.ShouldEqual("accountId");
    [Fact] void should_bind_commandless_http_stream_identity() => AppendFor("RouteAppend", "RouteAppended").Definitions.Single().SourceMember.ShouldEqual("accountId");
    [Fact] void should_retain_commandless_http_stream_loading() => ReadsFrom("RouteAppend").Single().Definitions.Single().SourceMember.ShouldEqual("accountId");
    [Fact] void should_not_leak_the_commandless_stream_parameter_into_properties() => Command("RouteAppend").Variants.Single().Definition.Properties.Any(_ => _.Name.Contains("stream", StringComparison.OrdinalIgnoreCase)).ShouldBeFalse();
    [Fact] void should_keep_a_commandless_http_return_out_of_persisted_events() => EventNames.ShouldNotContain("RouteFollowUp");
    [Fact] void should_keep_a_commandless_http_response_out_of_message_artifacts() => HasArtifactNamed("RouteFollowUp").ShouldBeFalse();
    [Fact] void should_discover_a_parameter_implementing_the_exact_stream_interface() => AppendFor("DerivedStreamAppend", "DerivedStreamEvent").Key.Target.ShouldEqual(SubjectOf(ArtifactKind.Aggregate, "Account"));
    [Fact] void should_not_treat_an_unrelated_same_named_attribute_as_loading_metadata() => ReadsFrom("SameNamedAttributeAppend").ShouldBeEmpty();
    [Fact] void should_still_prove_direct_writes_for_an_unrelated_same_named_attribute() => AppendFor("SameNamedAttributeAppend", "SameNamedAttributeEvent").Definitions.Single().SourceMember.ShouldBeNull();
    [Fact] void should_keep_an_ordinary_stream_handler_return_as_a_cascade() => CascadesFrom("Transfer", "TransferFollowUp").Count.ShouldEqual(1);
    [Fact] void should_not_treat_an_ordinary_stream_handler_return_as_an_event() => EventNames.ShouldNotContain("TransferFollowUp");
    [Fact] void should_keep_boundary_cascades() => CascadesFrom("BoundaryCommand", "BoundaryCascade").Count.ShouldEqual(1);
    [Fact] void should_exclude_response_side_effect_and_saga_slots() => HasArtifactNamed("BoundaryResponse", "BoundaryEffect", "BoundarySaga").ShouldBeFalse();
    [Fact] void should_exclude_a_saga_only_handler() => HasArtifactNamed("SagaOnlyTrigger", "TransferSaga").ShouldBeFalse();
    [Fact] void should_exclude_saga_state_while_retaining_an_unrelated_return() => CascadesFrom("SagaMixed", "SagaFollowUp").Count.ShouldEqual(1);
    [Fact] void should_exclude_saga_state_from_an_explicit_append_while_retaining_an_ordinary_sibling_event() => AppendFor("SagaMixedAppend", "SagaSiblingAppended").Key.Target.ShouldEqual(SubjectOf(ArtifactKind.Aggregate, "Account"));
    [Fact] void should_not_append_saga_state_from_a_mixed_explicit_append() => AppendsFrom("SagaMixedAppend").Count.ShouldEqual(1);
    [Fact] void should_not_use_a_generated_partial_base_to_classify_saga_state() => CascadesFrom("GeneratedSaga", "GeneratedBaseSaga").Count.ShouldEqual(1);
    [Fact] void should_not_infer_from_an_unrelated_same_named_api() => EventNames.ShouldNotContain("UnrelatedEvent");
    [Fact] void should_keep_the_unrelated_api_return_as_a_cascade() => CascadesFrom("Unrelated", "UnrelatedCascade").Count.ShouldEqual(1);
    [Fact] void should_not_guess_the_receiver_alias_event() => EventNames.ShouldNotContain("AliasedEvent");
    [Fact] void should_not_guess_an_event_before_an_opaque_spread() => EventNames.ShouldNotContain("OpaqueLeadingEvent");
    [Fact] void should_not_guess_object_dynamic_variable_or_helper_payloads() => EventNames.Any(_ => new[] { "OpaqueObjectEvent", "DynamicEvent", "VariableEvent", "HelperEvent", "NestedContainerEvent" }.Contains(_, StringComparer.Ordinal)).ShouldBeFalse();
    [Fact] void should_report_each_unresolved_exact_append_once() => Diagnostics(WolverineDiagnosticCodes.EventWriteTargetUnresolved).Count.ShouldEqual(9);
    [Fact] void should_locate_each_unresolved_append_in_authored_source() => Diagnostics(WolverineDiagnosticCodes.EventWriteTargetUnresolved).All(_ => _.Source?.Path == "Transfers/Handlers.cs").ShouldBeTrue();
    [Fact] void should_anchor_each_unresolved_append_at_its_authored_occurrence() => Diagnostics(WolverineDiagnosticCodes.EventWriteTargetUnresolved).All(_ => _.Source!.StartLine > 0 && _.Source.StartColumn > 0).ShouldBeTrue();
    [Fact] void should_preserve_each_unresolved_append_occurrence_without_collapsing_locations() => Diagnostics(WolverineDiagnosticCodes.EventWriteTargetUnresolved).Select(_ => (_.Source!.StartLine, _.Source.StartColumn)).Distinct().Count().ShouldEqual(9);
    [Fact] void should_report_unresolved_appends_in_authored_declaration_order() => UnresolvedAppendStartLines.SequenceEqual(UnresolvedAppendStartLines.Order()).ShouldBeTrue();
    [Fact] void should_not_promote_a_member_receiver_append_to_an_event() => EventNames.ShouldNotContain("MemberReceiverEvent");
    [Fact] void should_not_promote_an_object_round_trip_receiver_append_to_an_event() => EventNames.ShouldNotContain("ObjectRoundTripEvent");
    [Fact] void should_not_flatten_a_nested_payload_container() => EventNames.ShouldNotContain("NestedContainerEvent");
    [Fact] void should_recognize_an_event_with_a_computed_constructor_value_by_type() => EventNames.ShouldContain("OrderConfirmed");
    [Fact] void should_report_each_attributed_multiple_stream_binding_once() => Diagnostics(WolverineDiagnosticCodes.MultipleStreamMetadataOmitted).Count.ShouldEqual(6);
    [Fact] void should_locate_each_multiple_stream_metadata_loss() => Diagnostics(WolverineDiagnosticCodes.MultipleStreamMetadataOmitted).All(_ => _.Source?.Path == "Transfers/Handlers.cs").ShouldBeTrue();
    [Fact] void should_report_version_and_concurrency_metadata_per_binding() => Diagnostics(WolverineDiagnosticCodes.StreamVersionOmitted).Count.ShouldEqual(7);
    [Fact] void should_not_activate_generated_handlers_from_unlisted_trees() => HasArtifactNamed("GeneratedCommand", "GeneratedEvent").ShouldBeFalse();
    [Fact] void should_not_emit_diagnostics_from_unlisted_trees() => _result.Diagnostics.Any(_ => _.Source?.Path == "Transfers/GeneratedButNotNamed.cs").ShouldBeFalse();
    [Fact] void should_not_use_generated_partial_identity_members() => ReadsFrom("GeneratedMemberCommand").Single().Definitions.Single().SourceMember.ShouldBeNull();
    [Fact] void should_not_use_generated_partial_version_members() => Diagnostics(WolverineDiagnosticCodes.StreamVersionOmitted).Count.ShouldEqual(7);
    [Fact] void should_not_mark_generated_partial_identity_members_globally() => Command("GeneratedMemberCommand").Variants.Single().Definition.Properties.Any(_ => _.IsIdentifier).ShouldBeFalse();
    [Fact] void should_produce_every_proven_event() => EventNames.All(eventName => Produces(eventName).Count == 1).ShouldBeTrue();

    IReadOnlyList<string> EventNames =>
    [
        .. _result.Graph.Artifacts
            .Where(_ => _.Key.Kind == ArtifactKind.Event)
            .Select(_ => _.Variants[0].Definition.Name)
            .Order(StringComparer.Ordinal)
    ];

    IReadOnlyList<string> AggregateNames =>
    [
        .. _result.Graph.Artifacts
            .Where(_ => _.Key.Kind == ArtifactKind.Aggregate)
            .Select(_ => _.Variants[0].Definition.Name)
            .Order(StringComparer.Ordinal)
    ];

    ResolvedArtifact Command(string name) => _result.Graph.Artifacts.Single(_ =>
        _.Key.Kind == ArtifactKind.Command && _.Variants[0].Definition.Name == name);

    SubjectId SubjectOf(ArtifactKind kind, string name) => _result.Graph.Artifacts.Single(_ =>
        _.Key.Kind == kind && _.Variants[0].Definition.Name == name).Key.Subject;

    IReadOnlyList<ResolvedRelationship> AppendsFrom(string commandName) =>
    [
        .. _result.Graph.Relationships.Where(_ =>
            _.Key.Kind == RelationshipKind.Appends && _.Key.Source == SubjectOf(ArtifactKind.Command, commandName))
    ];

    ResolvedRelationship AppendFor(string commandName, string eventName) => AppendsFrom(commandName).Single(_ =>
        _.Key.Discriminator?.Contains(eventName, StringComparison.Ordinal) == true);

    IReadOnlyList<ResolvedRelationship> ReadsFrom(string commandName) =>
    [
        .. _result.Graph.Relationships.Where(_ =>
            _.Key.Kind == RelationshipKind.Reads && _.Key.Source == SubjectOf(ArtifactKind.Command, commandName))
    ];

    IReadOnlyList<ResolvedRelationship> CascadesFrom(string sourceName, string targetName)
    {
        var source = _result.Graph.Artifacts.Single(_ => _.Variants[0].Definition.Name == sourceName).Key.Subject;
        var target = _result.Graph.Artifacts.Single(_ => _.Key.Kind == ArtifactKind.Message && _.Variants[0].Definition.Name == targetName).Key.Subject;
        return [.. _result.Graph.Relationships.Where(_ => _.Key.Kind == RelationshipKind.Cascades && _.Key.Source == source && _.Key.Target == target)];
    }

    IReadOnlyList<ResolvedRelationship> Produces(string eventName) =>
    [
        .. _result.Graph.Relationships.Where(_ =>
            _.Key.Kind == RelationshipKind.Produces && _.Key.Target == SubjectOf(ArtifactKind.Event, eventName))
    ];

    bool HasArtifactNamed(params string[] names) => _result.Graph.Artifacts.Any(_ =>
        _.Variants.Any(variant => names.Contains(variant.Definition.Name, StringComparer.Ordinal)));

    IReadOnlyList<GenerationDiagnostic> Diagnostics(string code) => [.. _result.Diagnostics.Where(_ => _.Code == code)];

    IReadOnlyList<int> UnresolvedAppendStartLines =>
    [
        .. Diagnostics(WolverineDiagnosticCodes.EventWriteTargetUnresolved).Select(_ => _.Source!.StartLine)
    ];
}
