// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Cratis.CritterStack.Screenplay.for_CritterStackScreenplayGenerator;

public class when_generating_wolverine_saga_evidence : given.a_wolverine_saga_application
{
    GeneratedScreenplayDefinition _result = null!;
    GeneratedScreenplayDefinition _repeat = null!;

    void Because()
    {
        var generator = new CritterStackScreenplayGenerator();
        var options = new CritterStackScreenplayOptions { Domain = "Orders" };
        _result = generator.Generate([Project], options);
        _repeat = generator.Generate([Project], options);
    }

    [Fact] void should_succeed() => Assert.True(_result.IsSuccess, string.Join(Environment.NewLine, _result.Diagnostics.Where(_ => _.Severity == GenerationDiagnosticSeverity.Error).Select(_ => $"{_.Code}: {_.Message}")));
    [Fact] void should_admit_only_public_concrete_closed_authored_sagas_with_roles() => SagaNames.ShouldContainOnly(["BehaviorSaga", "CorrelationSaga", "FilteredSaga", "GeneratedCorrelationSaga", "RoleSaga"]);
    [Fact] void should_capture_only_authored_public_saga_state_properties() => Artifact(ArtifactKind.Saga, "RoleSaga").Variants.Single().Definition.Properties.Select(_ => _.Name).ShouldContainOnly(["id", "status"]);
    [Fact] void should_leave_sagas_unplaced() => _result.Graph.Placements.Any(_ => _.Artifact.Kind == ArtifactKind.Saga).ShouldBeFalse();
    [Fact] void should_leave_saga_handlers_unplaced() => _result.Graph.Placements.Any(_ => _.Artifact.Kind == ArtifactKind.Handler).ShouldBeFalse();
    [Fact] void should_admit_every_current_saga_role_spelling() => RoleSagaHandlerNames.Where(name => !name.EndsWith("Async", StringComparison.Ordinal)).ShouldContainOnly(["Consume", "Consumes", "Handle", "Handles", "NotFound", "Orchestrate", "Orchestrates", "Start", "StartOrHandle", "Starts", "StartsOrHandles"]);
    [Fact] void should_admit_every_async_twin() => RoleSagaHandlerNames.Where(name => name.EndsWith("Async", StringComparison.Ordinal)).ShouldContainOnly(["ConsumeAsync", "ConsumesAsync", "HandleAsync", "HandlesAsync", "NotFoundAsync", "OrchestrateAsync", "OrchestratesAsync", "StartAsync", "StartOrHandleAsync", "StartsAsync", "StartsOrHandlesAsync"]);
    [Fact] void should_classify_start_roles_stably() => RoleRelationships("RoleSaga", "start").Count.ShouldEqual(4);
    [Fact] void should_classify_start_or_handle_roles_stably() => RoleRelationships("RoleSaga", "start-or-handle").Count.ShouldEqual(4);
    [Fact] void should_classify_existing_roles_stably() => RoleRelationships("RoleSaga", "orchestrate").Count.ShouldEqual(12);
    [Fact] void should_classify_not_found_roles_stably() => RoleRelationships("RoleSaga", "not-found").Count.ShouldEqual(2);
    [Fact] void should_put_correlation_on_the_message_target() => SagaHandles.All(_ => _.Definitions.Single().SourceMember is null).ShouldBeTrue();
    [Fact] void should_honor_exact_saga_identity_before_all_names() => TargetMember("CorrelationSaga.Handle(AttributeIdentityMessage)").ShouldEqual("explicitIdentity");
    [Fact] void should_honor_exact_parameter_identity_before_names() => TargetMember("CorrelationSaga.Handle(ParameterIdentityMessage)").ShouldEqual("selected");
    [Fact] void should_honor_the_full_saga_type_name_before_the_short_name() => TargetMember("CorrelationSaga.Handle(FullNameIdentityMessage)").ShouldEqual("correlationSagaId");
    [Fact] void should_honor_the_saga_suffix_stripped_name() => TargetMember("CorrelationSaga.Handle(ShortNameIdentityMessage)").ShouldEqual("correlationId");
    [Fact] void should_honor_saga_id_before_id() => TargetMember("CorrelationSaga.Handle(SagaIdentityMessage)").ShouldEqual("sagaId");
    [Fact] void should_honor_case_insensitive_id_last() => TargetMember("CorrelationSaga.Handle(CaseInsensitiveIdentityMessage)").ShouldEqual("iD");
    [Fact] void should_leave_runtime_correlation_without_an_invented_member() => TargetMember("CorrelationSaga.Handle(RuntimeIdentityMessage)").ShouldBeNull();
    [Fact] void should_use_exact_evidence_for_saga_identity() => HandleRelationship("CorrelationSaga.Handle(AttributeIdentityMessage)").Evidence.Single().Strength.ShouldEqual(EvidenceStrength.Exact);
    [Fact] void should_use_exact_evidence_for_parameter_identity() => HandleRelationship("CorrelationSaga.Handle(ParameterIdentityMessage)").Evidence.Single().Strength.ShouldEqual(EvidenceStrength.Exact);
    [Fact] void should_use_conventional_evidence_for_named_correlation() => HandleRelationship("CorrelationSaga.Handle(FullNameIdentityMessage)").Evidence.Single().Strength.ShouldEqual(EvidenceStrength.Conventional);
    [Fact] void should_create_one_handler_artifact_per_admitted_method() => SagaHandlers.Count.ShouldEqual(40);
    [Fact] void should_keep_overloaded_handle_methods_distinct() => SagaHandlers.Where(_ => _.Variants.Single().Definition.Name.StartsWith("CorrelationSaga.Handle(", StringComparison.Ordinal)).Select(_ => _.Key.Subject).Distinct().Count().ShouldEqual(7);
    [Fact] void should_not_merge_overloaded_handler_relationships() => SagaHandles.Count(_ => HandlerName(_.Key.Source).StartsWith("CorrelationSaga.Handle(", StringComparison.Ordinal)).ShouldEqual(7);
    [Fact] void should_not_create_a_message_for_returned_saga_state() => ArtifactNames(ArtifactKind.Message).Any(name => new[] { "BehaviorSaga", "CorrelationSaga", "RoleSaga" }.Contains(name, StringComparer.Ordinal)).ShouldBeFalse();
    [Fact] void should_not_create_an_event_for_returned_saga_state() => ArtifactNames(ArtifactKind.Event).Any(name => new[] { "BehaviorSaga", "CorrelationSaga", "RoleSaga" }.Contains(name, StringComparer.Ordinal)).ShouldBeFalse();
    [Fact] void should_not_create_a_command_aggregate_or_read_model_for_saga_state() => _result.Graph.Artifacts.Any(_ => _.Variants.Any(variant => variant.Definition.Name == "BehaviorSaga") && _.Key.Kind is ArtifactKind.Command or ArtifactKind.Aggregate or ArtifactKind.ReadModel).ShouldBeFalse();
    [Fact] void should_not_produce_or_cascade_saga_state() => _result.Graph.Relationships.Any(_ => _.Key.Kind is RelationshipKind.Produces or RelationshipKind.Cascades && SagaSubjects.Contains(_.Key.Target)).ShouldBeFalse();
    [Fact] void should_preserve_non_state_tuple_slots_as_messages() => ArtifactNames(ArtifactKind.Message).ShouldContain("OrdinaryCascade");
    [Fact] void should_preserve_non_state_tuple_slots_as_cascades() => CascadesFrom("BehaviorSaga.StartOrHandle(MixedBehavior)", "OrdinaryCascade").Count.ShouldEqual(1);
    [Fact] void should_preserve_cascades_beside_saga_state_and_persistence_slots() => CascadesFrom("BehaviorSaga.Handle(PersistenceTrigger)", "OrdinaryCascade").Count.ShouldEqual(1);
    [Fact] void should_not_turn_persistence_wrappers_into_events_or_messages() => new[] { ArtifactKind.Event, ArtifactKind.Message }.All(kind => !ArtifactNames(kind).Contains("EventsToAppend", StringComparer.Ordinal)).ShouldBeTrue();
    [Fact] void should_treat_ordinary_saga_returns_as_cascades_not_events() => (CascadesFrom("BehaviorSaga.Handles(CascadeTrigger)", "OrdinaryCascade").Count == 1 && !ArtifactNames(ArtifactKind.Event).Contains("OrdinaryCascade", StringComparer.Ordinal)).ShouldBeTrue();
    [Fact] void should_treat_timeout_returns_as_delayed_messages() => CascadesFrom("BehaviorSaga.Handle(TimeoutTrigger)", "TimeoutNotice").Count.ShouldEqual(1);
    [Fact] void should_report_timeout_delivery_loss() => _result.Diagnostics.Any(_ => _.Code == WolverineDiagnosticCodes.DelayedMessageOmitted && _.Subject == Handler("BehaviorSaga.Handle(TimeoutTrigger)").Key.Subject).ShouldBeTrue();
    [Fact] void should_not_treat_timeout_messages_as_events() => ArtifactNames(ArtifactKind.Event).ShouldNotContain("TimeoutNotice");
    [Fact] void should_preserve_direct_send_semantics() => PublishesFrom("BehaviorSaga.Consume(BusTrigger)", "DirectSend", "send").Count.ShouldEqual(1);
    [Fact] void should_preserve_direct_publish_semantics() => PublishesFrom("BehaviorSaga.Consume(BusTrigger)", "DirectPublish", "publish").Count.ShouldEqual(1);
    [Fact] void should_preserve_direct_schedule_semantics() => PublishesFrom("BehaviorSaga.Consume(BusTrigger)", "DirectSchedule", "scheduled").Count.ShouldEqual(1);
    [Fact] void should_preserve_outgoing_message_cascades() => new[] { "OutgoingImmediate", "OutgoingDelayed" }.All(message => CascadesFrom("BehaviorSaga.Consumes(OutgoingTrigger)", message).Count == 1).ShouldBeTrue();
    [Fact] void should_not_turn_responses_or_side_effects_into_messages() => ArtifactNames(ArtifactKind.Message).Any(name => new[] { "SagaResponse", "SagaEffect" }.Contains(name, StringComparer.Ordinal)).ShouldBeFalse();
    [Fact] void should_not_invent_side_effect_topology_to_saga_state() => _result.Graph.Relationships.Any(_ => _.Key.Kind == RelationshipKind.SideEffect && SagaSubjects.Contains(_.Key.Target)).ShouldBeFalse();
    [Fact] void should_preserve_explicit_document_operations() => new[] { RelationshipKind.Stores, RelationshipKind.Updates, RelationshipKind.Deletes }.All(kind => _result.Graph.Relationships.Any(_ => _.Key.Kind == kind && _.Key.Target == Artifact(ArtifactKind.Document, "AuditDocument").Key.Subject)).ShouldBeTrue();
    [Fact] void should_not_invent_lifecycle_document_operations() => _result.Graph.Relationships.Any(_ => _.Key.Kind is RelationshipKind.Stores or RelationshipKind.Updates or RelationshipKind.Deletes && SagaSubjects.Contains(_.Key.Target)).ShouldBeFalse();
    [Fact] void should_report_workflow_loss_once_per_admitted_saga() => Diagnostics(WolverineDiagnosticCodes.SagaWorkflowOmitted).Count.ShouldEqual(5);
    [Fact] void should_report_exact_completion_conditionally_without_a_delete() => Diagnostics(WolverineDiagnosticCodes.SagaWorkflowOmitted).Single(_ => _.Subject == Artifact(ArtifactKind.Saga, "BehaviorSaga").Key.Subject).Message.ShouldContain("MarkCompleted");
    [Fact] void should_not_confuse_an_unrelated_completion_method_with_wolverine_lifecycle() => Diagnostics(WolverineDiagnosticCodes.SagaWorkflowOmitted).Single(_ => _.Subject == Artifact(ArtifactKind.Saga, "CorrelationSaga").Key.Subject).Message.ShouldNotContain("MarkCompleted");
    [Fact] void should_report_runtime_correlation_for_each_fallback_handler() => Diagnostics(WolverineDiagnosticCodes.SagaCorrelationRuntime).Count.ShouldEqual(2);
    [Fact] void should_locate_all_saga_diagnostics_in_authored_source() => Diagnostics(WolverineDiagnosticCodes.SagaWorkflowOmitted).Concat(Diagnostics(WolverineDiagnosticCodes.SagaCorrelationRuntime)).All(_ => _.Source?.Path == "Orders/Sagas.cs").ShouldBeTrue();
    [Fact] void should_deduplicate_saga_diagnostics_by_subject_and_code() => _result.Diagnostics.Where(_ => _.Code == WolverineDiagnosticCodes.SagaWorkflowOmitted || _.Code == WolverineDiagnosticCodes.SagaCorrelationRuntime).GroupBy(_ => (_.Code, _.Subject)).All(_ => _.Count() == 1).ShouldBeTrue();
    [Fact] void should_generate_diagnostics_deterministically() => DiagnosticSignatures(_result).ShouldContainOnly(DiagnosticSignatures(_repeat));
    [Fact] void should_not_admit_ignored_generic_abstract_internal_named_or_generated_sagas() => SagaNames.Any(name => new[] { "IgnoredSaga", "LegacyIgnoredSaga", "GenericSaga", "AbstractSaga", "InternalSaga", "NamedOnlySaga", "GeneratedOnlySaga" }.Contains(name, StringComparer.Ordinal)).ShouldBeFalse();
    [Fact] void should_not_admit_ignored_generic_static_primitive_or_parameterless_methods() => new[] { "IgnoredMethodMessage", "StaticExistingMessage", "GenericMethodMessage" }.Any(message => ArtifactNames(ArtifactKind.Message).Contains(message, StringComparer.Ordinal)).ShouldBeFalse();
    [Fact] void should_not_admit_a_saga_whose_base_originates_in_generated_source() => (SagaNames.Contains("GeneratedBaseSaga", StringComparer.Ordinal) || ArtifactNames(ArtifactKind.Message).Contains("GeneratedBaseMessage", StringComparer.Ordinal)).ShouldBeFalse();
    [Fact] void should_not_admit_generated_role_methods() => ArtifactNames(ArtifactKind.Message).ShouldNotContain("GeneratedRoleMessage");
    [Fact] void should_not_use_generated_correlation_members() => TargetMember("GeneratedCorrelationSaga.Handle(GeneratedCorrelationMessage)").ShouldBeNull();
    [Fact] void should_not_emit_generated_source_diagnostics() => _result.Diagnostics.Any(_ => _.Source?.Path == "Orders/Generated.g.cs").ShouldBeFalse();

    IReadOnlyList<string> SagaNames => ArtifactNames(ArtifactKind.Saga);
    IReadOnlyList<ResolvedArtifact> SagaHandlers => [.. SagaHandles.Select(_ => Handler(_.Key.Source)).DistinctBy(_ => _.Key.Subject)];
    IReadOnlyList<ResolvedRelationship> SagaHandles => [.. _result.Graph.Relationships.Where(_ => _.Key.Kind == RelationshipKind.Handles && _.Key.Discriminator?.StartsWith("wolverine:saga:", StringComparison.Ordinal) == true)];
    IReadOnlyList<SubjectId> SagaSubjects => [.. _result.Graph.Artifacts.Where(_ => _.Key.Kind == ArtifactKind.Saga).Select(_ => _.Key.Subject)];
    IReadOnlyList<string> RoleSagaHandlerNames =>
    [
        .. SagaHandlers
            .Select(_ => _.Variants.Single().Definition.Name)
            .Where(_ => _.StartsWith("RoleSaga.", StringComparison.Ordinal))
            .Select(_ => _["RoleSaga.".Length.._.IndexOf('(')])
            .Order(StringComparer.Ordinal)
    ];

    IReadOnlyList<string> ArtifactNames(ArtifactKind kind) =>
    [
        .. _result.Graph.Artifacts
            .Where(_ => _.Key.Kind == kind)
            .SelectMany(_ => _.Variants)
            .Select(_ => _.Definition.Name)
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal)
    ];

    ResolvedArtifact Artifact(ArtifactKind kind, string name) => _result.Graph.Artifacts.Single(_ =>
        _.Key.Kind == kind && _.Variants.Any(variant => variant.Definition.Name == name));

    ResolvedArtifact Handler(string name) => Artifact(ArtifactKind.Handler, name);
    ResolvedArtifact Handler(SubjectId subject) => _result.Graph.Artifacts.Single(_ => _.Key.Kind == ArtifactKind.Handler && _.Key.Subject == subject);
    string HandlerName(SubjectId subject) => Handler(subject).Variants.Single().Definition.Name;

    IReadOnlyList<ResolvedRelationship> RoleRelationships(string sagaName, string role) =>
    [
        .. SagaHandles.Where(_ =>
            HandlerName(_.Key.Source).StartsWith($"{sagaName}.", StringComparison.Ordinal) &&
            _.Key.Discriminator == $"wolverine:saga:{role}")
    ];

    ResolvedRelationship HandleRelationship(string handlerName) => SagaHandles.Single(_ => _.Key.Source == Handler(handlerName).Key.Subject);
    string? TargetMember(string handlerName) => HandleRelationship(handlerName).Definitions.Single().TargetMember;

    IReadOnlyList<ResolvedRelationship> CascadesFrom(string handlerName, string messageName) =>
    [
        .. _result.Graph.Relationships.Where(_ =>
            _.Key.Kind == RelationshipKind.Cascades &&
            _.Key.Source == Handler(handlerName).Key.Subject &&
            _.Key.Target == Artifact(ArtifactKind.Message, messageName).Key.Subject)
    ];

    IReadOnlyList<ResolvedRelationship> PublishesFrom(string handlerName, string messageName, string discriminator) =>
    [
        .. _result.Graph.Relationships.Where(_ =>
            _.Key.Kind == RelationshipKind.Publishes &&
            _.Key.Source == Handler(handlerName).Key.Subject &&
            _.Key.Target == Artifact(ArtifactKind.Message, messageName).Key.Subject &&
            _.Key.Discriminator == discriminator)
    ];

    IReadOnlyList<GenerationDiagnostic> Diagnostics(string code) => [.. _result.Diagnostics.Where(_ => _.Code == code)];

    static IReadOnlyList<string> DiagnosticSignatures(GeneratedScreenplayDefinition result) =>
    [
        .. result.Diagnostics
            .Where(_ => _.Code == WolverineDiagnosticCodes.SagaWorkflowOmitted || _.Code == WolverineDiagnosticCodes.SagaCorrelationRuntime)
            .Select(_ => $"{_.Code}|{_.Subject.Value}|{_.Source?.Path}|{_.Source?.StartLine}|{_.Source?.StartColumn}|{_.Message}")
            .Order(StringComparer.Ordinal)
    ];
}
