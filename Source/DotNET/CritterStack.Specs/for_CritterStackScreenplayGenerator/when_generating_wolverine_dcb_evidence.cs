// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Cratis.CritterStack.Screenplay.for_CritterStackScreenplayGenerator;

public class when_generating_wolverine_dcb_evidence : given.a_wolverine_dcb_application
{
    GeneratedScreenplayDefinition _result = null!;

    void Because() => _result = new CritterStackScreenplayGenerator().Generate(
        [Project],
        new CritterStackScreenplayOptions { Domain = "Accounts" });

    [Fact] void should_succeed() => Assert.True(_result.IsSuccess, string.Join(Environment.NewLine, _result.Diagnostics.Where(_ => _.Severity == GenerationDiagnosticSeverity.Error).Select(_ => $"{_.Code}: {_.Message}")));
    [Fact] void should_admit_sync_task_and_value_task_companions() => CommandNames.ShouldContainOnly(["BoundaryAccount", "BranchedQuery", "ChangeAccount", "CollectedAccount", "DerivedAttribute", "ForOnly", "FromConditionsQuery", "MixedWrappedAccount", "NoteAccount", "OpaquePayload", "OpaqueQuery", "ReviewAccount", "ValueTaskWrappedAccount", "WrappedAccount"]);
    [Fact] void should_create_only_the_dcb_state_aggregate() => AggregateNames.ShouldContainOnly(["AccountState"]);
    [Fact] void should_capture_nullable_state_return() => EventNames.ShouldContain("AccountNoted");
    [Fact] void should_capture_value_task_companion_return() => EventNames.ShouldContain("AccountReviewed");
    [Fact] void should_capture_an_authored_derived_attribute() => EventNames.ShouldContain("AccountDerivedChanged");
    [Fact] void should_capture_direct_boundary_append_one() => EventNames.ShouldContain("AccountFlagged");
    [Fact] void should_capture_safe_boundary_append_many_collections() => EventNames.ShouldContainOnly(["AccountAudited", "AccountBranchedQueryChanged", "AccountChanged", "AccountClosed", "AccountCollected", "AccountCollectedAgain", "AccountCredited", "AccountDebited", "AccountDerivedChanged", "AccountEscalated", "AccountFlagged", "AccountForOnlyChanged", "AccountFromConditionsChanged", "AccountNoted", "AccountOpened", "AccountOpaqueQueryChanged", "AccountReviewed", "AccountSiblingEvent", "AccountValueTaskWrapped", "AccountWrapped", "AccountWrappedAgain", "AccountWrapperEvent"]);
    [Fact] void should_capture_a_supported_persistence_wrapper() => Produces("AccountWrapped").Count.ShouldEqual(1);
    [Fact] void should_capture_persistence_wrapper_and_ordinary_tuple_siblings() => new[] { "AccountWrapperEvent", "AccountSiblingEvent" }.All(name => Produces(name).Count == 1).ShouldBeTrue();
    [Fact] void should_capture_a_value_task_wrapped_boundary_event() => Produces("AccountValueTaskWrapped").Count.ShouldEqual(1);
    [Fact] void should_capture_a_safe_direct_event_collection_return() => new[] { "AccountCollected", "AccountCollectedAgain" }.All(name => Produces(name).Count == 1).ShouldBeTrue();
    [Fact] void should_capture_ordinary_returns_after_valid_state_admission() => Produces("AccountChanged").Single().Definitions.Single().Key.Discriminator.ShouldEqual("declarative");
    [Fact] void should_mark_boundary_appends_as_imperative() => Produces("AccountFlagged").Single().Definitions.Single().Key.Discriminator.ShouldEqual("imperative");
    [Fact] void should_not_capture_an_opaque_payload_helper() => EventNames.ShouldNotContain("AccountOpaquePayload");
    [Fact] void should_not_capture_an_opaque_boundary_payload_local() => EventNames.ShouldNotContain("AccountOpaqueBoundaryPayload");
    [Fact] void should_not_capture_the_boundary_return() => EventNames.ShouldNotContain("BoundaryReturn");
    [Fact] void should_preserve_the_boundary_return_as_a_cascade() => CascadesFrom("BoundaryAccount", "BoundaryReturn").Count.ShouldEqual(1);
    [Fact] void should_not_classify_state_tag_or_query_types_as_events() => EventNames.Any(_ => new[] { "AccountState", "AccountId", "CustomerId", "EventTagQuery" }.Contains(_, StringComparer.Ordinal)).ShouldBeFalse();
    [Fact] void should_not_classify_responses_effects_sagas_or_wrapper_objects_as_events() => EventNames.Any(_ => new[] { "AccountResponse", "AccountEffect", "AccountSaga", "OutgoingMessages", "EventsToAppend" }.Contains(_, StringComparer.Ordinal)).ShouldBeFalse();
    [Fact] void should_establish_a_base_read_for_for_alone() => ReadsFrom("ForOnly").Count.ShouldEqual(1);
    [Fact] void should_preserve_the_for_alone_source_member_on_the_boundary_read() => ReadsFrom("ForOnly").Single().Definitions.Single().SourceMember.ShouldEqual("accountId");
    [Fact] void should_not_fabricate_one_identity_for_a_multi_tag_boundary() => ReadsFrom("ChangeAccount").Single(_ => !_.Key.Discriminator!.Contains(":condition:", StringComparison.Ordinal)).Definitions.Single().SourceMember.ShouldBeNull();
    [Fact] void should_not_turn_for_alone_into_a_condition() => ReadsFrom("ForOnly").Any(_ => _.Key.Discriminator?.Contains(":condition:", StringComparison.Ordinal) == true).ShouldBeFalse();
    [Fact] void should_preserve_all_ordered_change_conditions() => ConditionReadsFrom("ChangeAccount").Select(_ => ConditionOrdinal(_.Key.Discriminator!)).ShouldContainOnly([0, 1, 2, 3, 4]);
    [Fact] void should_preserve_or_tag_only_condition() => ConditionRead("ChangeAccount", 0).Key.Discriminator.ShouldContain("event:any");
    [Fact] void should_preserve_explicit_or_event_type() => ConditionRead("ChangeAccount", 1).Key.Discriminator.ShouldContain(SubjectOf(ArtifactKind.Event, "AccountOpened").Value);
    [Fact] void should_preserve_and_event_types_after_the_current_tag() => new[] { ConditionRead("ChangeAccount", 2), ConditionRead("ChangeAccount", 3) }.All(_ => _.Key.Discriminator?.Contains("CustomerId", StringComparison.Ordinal) == true).ShouldBeTrue();
    [Fact] void should_preserve_the_first_direct_request_source_member() => ConditionRead("ChangeAccount", 0).Definitions.Single().SourceMember.ShouldEqual("accountId");
    [Fact] void should_preserve_the_customer_direct_request_source_member() => ConditionRead("ChangeAccount", 1).Definitions.Single().SourceMember.ShouldEqual("customerId");
    [Fact] void should_only_read_the_dcb_aggregate() => _result.Graph.Relationships.Where(_ => _.Key.Kind == RelationshipKind.Reads).All(_ => _.Key.Target == SubjectOf(ArtifactKind.Aggregate, "AccountState")).ShouldBeTrue();
    [Fact] void should_not_create_reads_to_historical_events() => _result.Graph.Relationships.Any(_ => _.Key.Kind == RelationshipKind.Reads && EventSubjects.Contains(_.Key.Target)).ShouldBeFalse();
    [Fact] void should_create_historical_event_artifacts_without_production() => Produces("AccountOpened").ShouldBeEmpty();
    [Fact] void should_not_invent_dcb_topology() => _result.Graph.Relationships.Any(_ => _.Key.Kind is RelationshipKind.Appends or RelationshipKind.StartsStream or RelationshipKind.Builds).ShouldBeFalse();
    [Fact] void should_not_invent_reducers_read_models_or_projections() => _result.Graph.Artifacts.Any(_ => _.Key.Kind is ArtifactKind.Reducer or ArtifactKind.ReadModel or ArtifactKind.Projection).ShouldBeFalse();
    [Fact] void should_report_one_boundary_loss_per_admitted_handler() => Diagnostics(WolverineDiagnosticCodes.DcbBoundaryOmitted).Count.ShouldEqual(14);
    [Fact] void should_report_each_unresolved_query_once() => Diagnostics(WolverineDiagnosticCodes.DcbQueryUnresolved).Count.ShouldEqual(3);
    [Fact] void should_locate_dcb_diagnostics_in_authored_source() => Diagnostics(WolverineDiagnosticCodes.DcbBoundaryOmitted).Concat(Diagnostics(WolverineDiagnosticCodes.DcbQueryUnresolved)).All(_ => _.Source?.Path == "Accounts/Dcb.cs").ShouldBeTrue();
    [Fact] void should_not_fabricate_conditions_for_opaque_queries() => ConditionReadsFrom("OpaqueQuery").Concat(ConditionReadsFrom("BranchedQuery")).Concat(ConditionReadsFrom("FromConditionsQuery")).ShouldBeEmpty();
    [Fact] void should_not_admit_missing_multiple_invalid_or_unrelated_parameters() => CommandNames.Any(_ => new[] { "MissingCompanion", "MultipleModels", "InvalidModel", "InvalidBoundary", "UnrelatedAttribute", "GeneratedCompanion", "MismatchedCompanion", "NoRequestCompanion" }.Contains(_, StringComparer.Ordinal)).ShouldBeFalse();
    [Fact] void should_not_capture_non_dcb_returns_as_events() => EventNames.ShouldNotContain("AccountNonDcbChanged");
    [Fact] void should_not_admit_an_unrelated_companion_for_the_handler_request() => EventNames.ShouldNotContain("AccountMismatchedCompanionChanged");
    [Fact] void should_not_use_generated_companions_or_handlers() => (CommandNames.Contains("GeneratedCompanion", StringComparer.Ordinal) || EventNames.Contains("AccountGeneratedCompanionChanged", StringComparer.Ordinal) || HasArtifactNamed("GeneratedCommand", "GeneratedEvent")).ShouldBeFalse();
    [Fact] void should_not_emit_generated_source_diagnostics() => _result.Diagnostics.Any(_ => _.Source?.Path == "Accounts/Generated.cs").ShouldBeFalse();
    [Fact] void should_use_stable_parameter_and_condition_discriminators() => ReadsFrom("ChangeAccount").All(_ => _.Key.Discriminator?.StartsWith("wolverine:dcb:1:state", StringComparison.Ordinal) == true).ShouldBeTrue();
    [Fact] void should_produce_every_declarative_or_appended_event_only_once() => new[] { "AccountChanged", "AccountNoted", "AccountReviewed", "AccountFlagged", "AccountEscalated", "AccountAudited", "AccountWrapped", "AccountWrappedAgain", "AccountWrapperEvent", "AccountSiblingEvent", "AccountValueTaskWrapped", "AccountCollected", "AccountCollectedAgain", "AccountForOnlyChanged", "AccountDerivedChanged", "AccountOpaqueQueryChanged", "AccountBranchedQueryChanged", "AccountFromConditionsChanged" }.All(name => Produces(name).Count == 1).ShouldBeTrue();

    IReadOnlyList<string> CommandNames => ArtifactNames(ArtifactKind.Command);
    IReadOnlyList<string> EventNames => ArtifactNames(ArtifactKind.Event);
    IReadOnlyList<string> AggregateNames => ArtifactNames(ArtifactKind.Aggregate);
    IReadOnlyList<SubjectId> EventSubjects => [.. _result.Graph.Artifacts.Where(_ => _.Key.Kind == ArtifactKind.Event).Select(_ => _.Key.Subject)];

    IReadOnlyList<string> ArtifactNames(ArtifactKind kind) =>
    [
        .. _result.Graph.Artifacts
            .Where(_ => _.Key.Kind == kind)
            .Select(_ => _.Variants[0].Definition.Name)
            .Order(StringComparer.Ordinal)
    ];

    SubjectId SubjectOf(ArtifactKind kind, string name) => _result.Graph.Artifacts.Single(_ =>
        _.Key.Kind == kind && _.Variants.Any(variant => variant.Definition.Name == name)).Key.Subject;

    IReadOnlyList<ResolvedRelationship> ReadsFrom(string commandName) =>
    [
        .. _result.Graph.Relationships.Where(_ =>
            _.Key.Kind == RelationshipKind.Reads &&
            _.Key.Source == SubjectOf(ArtifactKind.Command, commandName))
    ];

    IReadOnlyList<ResolvedRelationship> ConditionReadsFrom(string commandName) =>
        [.. ReadsFrom(commandName).Where(_ => _.Key.Discriminator?.Contains(":condition:", StringComparison.Ordinal) == true)];

    ResolvedRelationship ConditionRead(string commandName, int ordinal) =>
        ConditionReadsFrom(commandName).Single(_ => ConditionOrdinal(_.Key.Discriminator!) == ordinal);

    static int ConditionOrdinal(string discriminator)
    {
        const string marker = ":condition:";
        var start = discriminator.IndexOf(marker, StringComparison.Ordinal) + marker.Length;
        var end = discriminator.IndexOf(':', start);
        return int.Parse(discriminator[start..end], System.Globalization.CultureInfo.InvariantCulture);
    }

    IReadOnlyList<ResolvedRelationship> Produces(string eventName) =>
    [
        .. _result.Graph.Relationships.Where(_ =>
            _.Key.Kind == RelationshipKind.Produces &&
            _.Key.Target == SubjectOf(ArtifactKind.Event, eventName))
    ];

    IReadOnlyList<ResolvedRelationship> CascadesFrom(string commandName, string messageName) =>
    [
        .. _result.Graph.Relationships.Where(_ =>
            _.Key.Kind == RelationshipKind.Cascades &&
            _.Key.Source == SubjectOf(ArtifactKind.Command, commandName) &&
            _.Key.Target == SubjectOf(ArtifactKind.Message, messageName))
    ];

    bool HasArtifactNamed(params string[] names) => _result.Graph.Artifacts.Any(_ =>
        _.Variants.Any(variant => names.Contains(variant.Definition.Name, StringComparer.Ordinal)));

    IReadOnlyList<GenerationDiagnostic> Diagnostics(string code) => [.. _result.Diagnostics.Where(_ => _.Code == code)];
}
