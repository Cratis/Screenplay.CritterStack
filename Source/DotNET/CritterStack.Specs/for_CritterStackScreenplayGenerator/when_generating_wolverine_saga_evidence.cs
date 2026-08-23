// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Cratis.CritterStack.Screenplay.for_CritterStackScreenplayGenerator;

public class when_generating_wolverine_saga_evidence : given.a_wolverine_saga_application
{
    const string SagaLifecycleRealization = "WOLVERINE0016";

    GeneratedScreenplayDefinition _result = null!;
    GeneratedScreenplayDefinition _repeat = null!;
    GeneratedScreenplayDefinition _generatedOnlySagaResult = null!;

    void Because()
    {
        var generator = new CritterStackScreenplayGenerator();
        var options = new CritterStackScreenplayOptions { Domain = "Orders" };
        _result = generator.Generate([Project], options);
        _repeat = generator.Generate([Project], options);
        _generatedOnlySagaResult = generator.Generate(
            [given.a_generated_source_only_wolverine_saga_application.CreateProject()],
            options);
    }

    [Fact] void should_succeed() => Assert.True(_result.IsSuccess, string.Join(Environment.NewLine, _result.Diagnostics.Where(_ => _.Severity == GenerationDiagnosticSeverity.Error).Select(_ => $"{_.Code}: {_.Message}")));
    [Fact] void should_admit_only_public_concrete_closed_authored_sagas_with_chain_admitted_roles() => SagaNames.ShouldContainOnly(["BehaviorSaga", "ContextualStaticSaga", "CorrelationSaga", "ExistingOnlySaga", "ExistingPrivateConstructorSaga", "FilteredSaga", "GeneratedCorrelationSaga", "ReturnedCreationSaga", "RoleSaga"]);
    [Fact] void should_capture_only_authored_public_saga_state_properties() => Artifact(ArtifactKind.Saga, "RoleSaga").Variants.Single().Definition.Properties.Select(_ => _.Name).ShouldContainOnly(["id", "status"]);
    [Fact] void should_capture_only_authored_message_properties() => Artifact(ArtifactKind.Message, "GeneratedCorrelationMessage").Variants.Single().Definition.Properties.ShouldBeEmpty();
    [Fact] void should_capture_only_authored_returned_cascade_properties() => Artifact(ArtifactKind.Message, "OrdinaryCascade").Variants.Single().Definition.Properties.Select(_ => _.Name).ShouldContainOnly(["behaviorSagaId"]);
    [Fact] void should_capture_only_authored_direct_bus_message_properties() => Artifact(ArtifactKind.Message, "DirectSend").Variants.Single().Definition.Properties.Select(_ => _.Name).ShouldContainOnly(["behaviorSagaId"]);
    [Fact] void should_capture_only_authored_outgoing_message_properties() => Artifact(ArtifactKind.Message, "OutgoingImmediate").Variants.Single().Definition.Properties.Select(_ => _.Name).ShouldContainOnly(["behaviorSagaId"]);
    [Fact] void should_leave_sagas_unplaced() => _result.Graph.Placements.Any(_ => _.Artifact.Kind == ArtifactKind.Saga).ShouldBeFalse();
    [Fact] void should_leave_saga_handlers_unplaced() => _result.Graph.Placements.Any(_ => _.Artifact.Kind == ArtifactKind.Handler).ShouldBeFalse();
    [Fact] void should_admit_chain_establishing_current_saga_role_spellings() => RoleSagaHandlerNames.Where(name => !name.EndsWith("Async", StringComparison.Ordinal)).ShouldContainOnly(["Consume", "Consumes", "Handle", "Handles", "NotFound", "Orchestrate", "Orchestrates", "Start", "StartOrHandle", "StartsOrHandles"]);
    [Fact] void should_admit_chain_establishing_async_twins() => RoleSagaHandlerNames.Where(name => name.EndsWith("Async", StringComparison.Ordinal)).ShouldContainOnly(["ConsumeAsync", "ConsumesAsync", "HandleAsync", "HandlesAsync", "OrchestrateAsync", "OrchestratesAsync", "StartAsync", "StartOrHandleAsync", "StartsOrHandlesAsync"]);
    [Fact] void should_admit_context_dependent_static_roles_for_the_same_message() => ContextualStaticHandlerNames.ShouldContainOnly(["NotFoundAsync", "Start", "Starts", "StartsAsync"]);
    [Fact] void should_classify_start_roles_stably() => RoleRelationships("RoleSaga", "start").Count.ShouldEqual(2);
    [Fact] void should_classify_start_or_handle_roles_stably() => RoleRelationships("RoleSaga", "start-or-handle").Count.ShouldEqual(4);
    [Fact] void should_classify_existing_roles_stably() => RoleRelationships("RoleSaga", "orchestrate").Count.ShouldEqual(12);
    [Fact] void should_classify_not_found_roles_stably() => RoleRelationships("RoleSaga", "not-found").Count.ShouldEqual(1);
    [Fact] void should_put_correlation_on_the_message_target() => SagaHandles.All(_ => _.Definitions.Single().SourceMember is null).ShouldBeTrue();
    [Fact] void should_honor_exact_saga_identity_before_all_names() => TargetMember("CorrelationSaga.Handle(AttributeIdentityMessage)").ShouldEqual("explicitIdentity");
    [Fact] void should_honor_exact_parameter_identity_before_names() => TargetMember("CorrelationSaga.Handle(ParameterIdentityMessage)").ShouldEqual("selected");
    [Fact] void should_inspect_saga_identity_from_across_all_handler_parameters() => TargetMember("CorrelationSaga.Handle(SecondaryParameterIdentityMessage, string)").ShouldEqual("selected");
    [Fact] void should_fail_closed_for_conflicting_saga_identity_from_parameters() => ConflictingIdentityHandlers.All(handler => TargetMember(handler) is null).ShouldBeTrue();
    [Fact] void should_honor_the_full_saga_type_name_before_the_short_name() => TargetMember("CorrelationSaga.Handle(FullNameIdentityMessage)").ShouldEqual("correlationSagaId");
    [Fact] void should_honor_the_saga_suffix_stripped_name() => TargetMember("CorrelationSaga.Handle(ShortNameIdentityMessage)").ShouldEqual("correlationId");
    [Fact] void should_honor_saga_id_before_id() => TargetMember("CorrelationSaga.Handle(SagaIdentityMessage)").ShouldEqual("sagaId");
    [Fact] void should_honor_case_insensitive_id_last() => TargetMember("CorrelationSaga.Handle(CaseInsensitiveIdentityMessage)").ShouldEqual("iD");
    [Fact] void should_not_retry_the_full_saga_name_after_a_missing_explicit_name() => TargetMember("CorrelationSaga.Handle(MissingExplicitIdentityMessage)").ShouldEqual("correlationId");
    [Fact] void should_find_inherited_public_properties_for_correlation() => TargetMember("CorrelationSaga.Handle(InheritedPropertyIdentityMessage)").ShouldEqual("correlationSagaId");
    [Fact] void should_find_inherited_public_fields_for_correlation() => TargetMember("CorrelationSaga.Handle(InheritedFieldIdentityMessage)").ShouldEqual("sagaId");
    [Fact] void should_fail_closed_for_ambiguous_inherited_correlation_members() => TargetMember("CorrelationSaga.Handle(AmbiguousIdentityMessage)").ShouldBeNull();
    [Fact] void should_leave_runtime_correlation_without_an_invented_member() => TargetMember("CorrelationSaga.Handle(RuntimeIdentityMessage)").ShouldBeNull();
    [Fact] void should_use_exact_evidence_for_saga_identity() => HandleRelationship("CorrelationSaga.Handle(AttributeIdentityMessage)").Evidence.Single().Strength.ShouldEqual(EvidenceStrength.Exact);
    [Fact] void should_use_exact_evidence_for_parameter_identity() => HandleRelationship("CorrelationSaga.Handle(ParameterIdentityMessage)").Evidence.Single().Strength.ShouldEqual(EvidenceStrength.Exact);
    [Fact] void should_use_exact_evidence_for_saga_identity_from_on_a_later_parameter() => HandleRelationship("CorrelationSaga.Handle(SecondaryParameterIdentityMessage, string)").Evidence.Single().Strength.ShouldEqual(EvidenceStrength.Exact);
    [Fact] void should_locate_conflicting_saga_identity_from_diagnostics_at_the_authored_parameter() => ConflictingIdentityDiagnostics.All(_ => string.Equals(_.Source?.Path, "Orders/Sagas.cs", StringComparison.Ordinal) && _.Source?.StartLine > 0).ShouldBeTrue();
    [Fact] void should_locate_conflicting_saga_identity_from_diagnostics_deterministically() => ConflictingIdentityDiagnosticSignatures(_result).ShouldContainOnly(ConflictingIdentityDiagnosticSignatures(_repeat));
    [Fact] void should_use_conventional_evidence_for_named_correlation() => HandleRelationship("CorrelationSaga.Handle(FullNameIdentityMessage)").Evidence.Single().Strength.ShouldEqual(EvidenceStrength.Conventional);
    [Fact] void should_create_one_handler_artifact_per_admitted_method() => SagaHandlers.Count.ShouldEqual(55);
    [Fact] void should_keep_overloaded_handle_methods_distinct() => SagaHandlers.Where(_ => _.Variants.Single().Definition.Name.StartsWith("CorrelationSaga.Handle(", StringComparison.Ordinal)).Select(_ => _.Key.Subject).Distinct().Count().ShouldEqual(13);
    [Fact] void should_not_merge_overloaded_handler_relationships() => SagaHandles.Count(_ => HandlerName(_.Key.Source).StartsWith("CorrelationSaga.Handle(", StringComparison.Ordinal)).ShouldEqual(13);
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
    [Fact] void should_preserve_direct_send_semantics() => PublishesFrom("BehaviorSaga.Consume(BusTrigger, Wolverine.IMessageBus)", "DirectSend", "send").Count.ShouldEqual(1);
    [Fact] void should_preserve_direct_publish_semantics() => PublishesFrom("BehaviorSaga.Consume(BusTrigger, Wolverine.IMessageBus)", "DirectPublish", "publish").Count.ShouldEqual(1);
    [Fact] void should_preserve_direct_schedule_semantics() => PublishesFrom("BehaviorSaga.Consume(BusTrigger, Wolverine.IMessageBus)", "DirectSchedule", "scheduled").Count.ShouldEqual(1);
    [Fact] void should_preserve_direct_request_semantics() => PublishesFrom("BehaviorSaga.Consume(BusTrigger, Wolverine.IMessageBus)", "DirectRequest", "request-reply").Count.ShouldEqual(1);
    [Fact] void should_exclude_saga_state_from_all_direct_bus_paths_while_preserving_ordinary_siblings() => _result.Graph.Relationships.Any(_ => _.Key.Kind == RelationshipKind.Publishes && SagaSubjects.Contains(_.Key.Target)).ShouldBeFalse();
    [Fact] void should_preserve_outgoing_message_cascades() => new[] { "OutgoingImmediate", "OutgoingDelayed" }.All(message => CascadesFrom("BehaviorSaga.Consumes(OutgoingTrigger)", message).Count == 1).ShouldBeTrue();
    [Fact] void should_not_turn_responses_or_side_effects_into_messages() => ArtifactNames(ArtifactKind.Message).Any(name => new[] { "SagaResponse", "SagaEffect" }.Contains(name, StringComparer.Ordinal)).ShouldBeFalse();
    [Fact] void should_not_turn_saga_state_into_http_queries_or_read_models() => _result.Graph.Artifacts.Any(_ => _.Key.Kind is ArtifactKind.Query or ArtifactKind.ReadModel && _.Variants.Any(variant => new[] { "Direct", "TaskResult", "Collection", "BehaviorSaga" }.Contains(variant.Definition.Name, StringComparer.Ordinal))).ShouldBeFalse();
    [Fact] void should_not_return_saga_state_from_http_queries() => _result.Graph.Relationships.Any(_ => _.Key.Kind == RelationshipKind.Returns && SagaSubjects.Contains(_.Key.Target)).ShouldBeFalse();
    [Fact] void should_preserve_ordinary_http_queries_beside_rejected_saga_queries() => ArtifactNames(ArtifactKind.ReadModel).ShouldContain("OrdinaryQueryModel");
    [Fact] void should_not_invent_side_effect_topology_to_saga_state() => _result.Graph.Relationships.Any(_ => _.Key.Kind == RelationshipKind.SideEffect && SagaSubjects.Contains(_.Key.Target)).ShouldBeFalse();
    [Fact] void should_preserve_explicit_document_operations() => new[] { RelationshipKind.Stores, RelationshipKind.Updates, RelationshipKind.Deletes }.All(kind => _result.Graph.Relationships.Any(_ => _.Key.Kind == kind && _.Key.Target == Artifact(ArtifactKind.Document, "AuditDocument").Key.Subject)).ShouldBeTrue();
    [Fact] void should_migrate_non_saga_marten_handlers_to_signature_safe_subjects() => NeutralDocumentHandlerSubjects.ShouldContainOnly([
        "dotnet://Orders/Orders/Orders.NeutralDocumentOperations#method:M%3AOrders.NeutralDocumentOperations.Apply%28Orders.DocumentOperationMessage%2CMarten.IDocumentSession%29",
        "dotnet://Orders/Orders/Orders.NeutralDocumentOperations#method:M%3AOrders.NeutralDocumentOperations.Apply%28Orders.DocumentOperationMessage%2CSystem.String%2CMarten.IDocumentSession%29"]);
    [Fact] void should_keep_non_saga_marten_overloads_separate() => NeutralDocumentHandlerSubjects.Distinct(StringComparer.Ordinal).Count().ShouldEqual(2);
    [Fact] void should_ignore_generated_non_saga_marten_overloads() => NeutralDocumentHandlerNames.Any(_ => _.Contains("int", StringComparison.Ordinal)).ShouldBeFalse();
    [Fact] void should_converge_marten_and_wolverine_on_the_same_saga_handler_identity() => DocumentRelationshipsFrom("BehaviorSaga.Orchestrate(DocumentTrigger, Marten.IDocumentSession)").Count.ShouldEqual(3);
    [Fact] void should_not_invent_lifecycle_document_operations() => _result.Graph.Relationships.Any(_ => _.Key.Kind is RelationshipKind.Stores or RelationshipKind.Updates or RelationshipKind.Deletes && SagaSubjects.Contains(_.Key.Target)).ShouldBeFalse();
    [Fact] void should_report_lifecycle_realization_once_per_admitted_saga() => Diagnostics(SagaLifecycleRealization).Count.ShouldEqual(9);
    [Fact] void should_report_lifecycle_as_provenance_for_ordinary_event_modeling() => Diagnostics(SagaLifecycleRealization).All(_ => _.Severity == GenerationDiagnosticSeverity.Information && _.Message.Contains("realization/provenance", StringComparison.Ordinal) && _.Message.Contains("ordinary Event Modeling building blocks", StringComparison.Ordinal)).ShouldBeTrue();
    [Fact] void should_report_exact_completion_realization_without_a_delete() => Diagnostics(SagaLifecycleRealization).Single(_ => _.Subject == Artifact(ArtifactKind.Saga, "BehaviorSaga").Key.Subject).Message.ShouldContain("MarkCompleted");
    [Fact] void should_not_confuse_an_unrelated_completion_method_with_wolverine_lifecycle() => Diagnostics(SagaLifecycleRealization).Single(_ => _.Subject == Artifact(ArtifactKind.Saga, "CorrelationSaga").Key.Subject).Message.ShouldNotContain("MarkCompleted");
    [Fact] void should_report_runtime_correlation_for_each_fallback_or_ambiguous_handler() => Diagnostics(WolverineDiagnosticCodes.SagaCorrelationRuntime).Count.ShouldEqual(5);
    [Fact] void should_report_ambiguous_correlation_deterministically() => Diagnostics(WolverineDiagnosticCodes.SagaCorrelationRuntime).Single(_ => _.Subject == Handler("CorrelationSaga.Handle(AmbiguousIdentityMessage)").Key.Subject).Message.ShouldContain("multiple public members");
    [Fact] void should_report_every_authored_rejected_role_shape() => Diagnostics(WolverineDiagnosticCodes.SagaRoleUnresolved).Count.ShouldEqual(15);
    [Fact] void should_locate_all_saga_diagnostics_in_authored_source() => Diagnostics(SagaLifecycleRealization).Concat(Diagnostics(WolverineDiagnosticCodes.SagaCorrelationRuntime)).Concat(Diagnostics(WolverineDiagnosticCodes.SagaRoleUnresolved)).All(_ => _.Source?.Path == "Orders/Sagas.cs").ShouldBeTrue();
    [Fact] void should_deduplicate_saga_diagnostics_by_subject_and_code() => _result.Diagnostics.Where(_ => _.Code == SagaLifecycleRealization || _.Code == WolverineDiagnosticCodes.SagaCorrelationRuntime || _.Code == WolverineDiagnosticCodes.SagaRoleUnresolved).GroupBy(_ => (_.Code, _.Subject)).All(_ => _.Count() == 1).ShouldBeTrue();
    [Fact] void should_generate_diagnostics_deterministically() => DiagnosticSignatures(_result).ShouldContainOnly(DiagnosticSignatures(_repeat));
    [Fact] void should_generate_byte_identical_screenplay() => _repeat.Source.ShouldEqual(_result.Source);
    [Fact] void should_retain_generated_screenplay_hash() => ScreenplayHash(_result).ShouldEqual("49A5C25A2D1A1ADAE5A56EFAE1855F901E03473F2C25ABFFE85AA0DDA4041A86");
    [Fact] void should_not_admit_ignored_generic_abstract_internal_named_or_generated_sagas() => SagaNames.Any(name => new[] { "IgnoredSaga", "LegacyIgnoredSaga", "GenericSaga", "AbstractSaga", "InternalSaga", "NamedOnlySaga", "GeneratedOnlySaga" }.Contains(name, StringComparer.Ordinal)).ShouldBeFalse();
    [Fact] void should_not_admit_ignored_generic_static_primitive_parameterless_isolated_or_constructorless_methods() => new[] { "IgnoredMethodMessage", "StaticExistingMessage", "GenericMethodMessage", "InvalidInstanceNotFoundMessage", "PrimitiveReturnMessage", "IsolatedStartsMessage", "IsolatedStartsAsyncMessage", "IsolatedNotFoundAsyncMessage", "MissingFallbackConstructorMessage", "MissingInstanceConstructorMessage", "NotFoundOnlyPrivateConstructorMessage" }.Any(message => ArtifactNames(ArtifactKind.Message).Contains(message, StringComparer.Ordinal)).ShouldBeFalse();
    [Fact] void should_reject_not_found_only_private_constructor_chain_without_saga_facts() => _result.Graph.Artifacts.SelectMany(_ => _.Variants).Any(_ => _.Definition.Name.Contains("NotFoundOnlyPrivateConstructor", StringComparison.Ordinal)).ShouldBeFalse();
    [Fact] void should_report_not_found_only_private_constructor_rejection() => Diagnostics(WolverineDiagnosticCodes.SagaRoleUnresolved).Single(_ => _.Message.Contains("NotFoundOnlyPrivateConstructorSaga.NotFound(NotFoundOnlyPrivateConstructorMessage)", StringComparison.Ordinal)).Message.ShouldContain("no accessible public parameterless constructor");
    [Fact] void should_admit_static_start_with_fallback_creation_when_a_public_constructor_exists() => ArtifactNames(ArtifactKind.Message).ShouldContain("StaticFallbackStartMessage");
    [Fact] void should_admit_existing_roles_without_requiring_saga_creation() => new[] { "ExistingOnlyMessage", "ExistingPrivateConstructorMessage" }.All(message => ArtifactNames(ArtifactKind.Message).Contains(message, StringComparer.Ordinal)).ShouldBeTrue();
    [Fact] void should_admit_exact_returned_saga_creation_without_a_public_constructor() => ArtifactNames(ArtifactKind.Message).ShouldContain("ReturnedCreationMessage");
    [Fact] void should_require_authoritative_canonical_saga_source_before_saga_or_payload_classification() =>
        (SagaNames.Contains("GeneratedBaseSaga", StringComparer.Ordinal) ||
         ArtifactNames(ArtifactKind.Message).Contains("GeneratedBaseMessage", StringComparer.Ordinal) ||
         ArtifactNames(_generatedOnlySagaResult, ArtifactKind.Saga).Count > 0 ||
         !ArtifactNames(_generatedOnlySagaResult, ArtifactKind.Message).Contains("GeneratedCanonicalMessage", StringComparer.Ordinal) ||
         !ArtifactNames(_generatedOnlySagaResult, ArtifactKind.Event).Contains("GeneratedCanonicalEvent", StringComparer.Ordinal)).ShouldBeFalse();
    [Fact] void should_not_admit_generated_role_methods() => ArtifactNames(ArtifactKind.Message).ShouldNotContain("GeneratedRoleMessage");
    [Fact] void should_not_use_generated_correlation_members() => TargetMember("GeneratedCorrelationSaga.Handle(GeneratedCorrelationMessage)").ShouldBeNull();
    [Fact] void should_not_allow_a_generated_ignore_attribute_to_suppress_an_authored_saga() => SagaNames.ShouldContain("GeneratedCorrelationSaga");
    [Fact] void should_not_emit_generated_source_diagnostics() => _result.Diagnostics.Any(_ => _.Source?.Path == "Orders/Generated.g.cs").ShouldBeFalse();

    IReadOnlyList<string> SagaNames => ArtifactNames(ArtifactKind.Saga);
    IReadOnlyList<ResolvedArtifact> SagaHandlers => [.. SagaHandles.Select(_ => Handler(_.Key.Source)).DistinctBy(_ => _.Key.Subject)];
    IReadOnlyList<ResolvedRelationship> SagaHandles => [.. _result.Graph.Relationships.Where(_ => _.Key.Kind == RelationshipKind.Handles && _.Key.Discriminator?.StartsWith("wolverine:saga:", StringComparison.Ordinal) == true)];
    IReadOnlyList<SubjectId> SagaSubjects => [.. _result.Graph.Artifacts.Where(_ => _.Key.Kind == ArtifactKind.Saga).Select(_ => _.Key.Subject)];
    IReadOnlyList<string> RoleSagaHandlerNames => HandlerMethodNames("RoleSaga");
    IReadOnlyList<string> ContextualStaticHandlerNames => HandlerMethodNames("ContextualStaticSaga");
    IReadOnlyList<string> ConflictingIdentityHandlers =>
    [
        "CorrelationSaga.Handle(ConflictingParameterIdentityMessage, string)",
        "CorrelationSaga.Handles(ConflictingParameterIdentityMessage, int)"
    ];
    IReadOnlyList<GenerationDiagnostic> ConflictingIdentityDiagnostics =>
    [
        .. Diagnostics(WolverineDiagnosticCodes.SagaCorrelationRuntime).Where(_ => ConflictingIdentityHandlers.Select(Handler).Select(handler => handler.Key.Subject).Contains(_.Subject))
    ];
    IReadOnlyList<ResolvedArtifact> NeutralDocumentHandlers =>
    [
        .. _result.Graph.Artifacts.Where(_ =>
            _.Key.Kind == ArtifactKind.Handler &&
            _.Variants.Any(variant => variant.Definition.Name.StartsWith("NeutralDocumentOperations.Apply(", StringComparison.Ordinal)))
    ];
    IReadOnlyList<string> NeutralDocumentHandlerNames => [.. NeutralDocumentHandlers.SelectMany(_ => _.Variants).Select(_ => _.Definition.Name)];
    IReadOnlyList<string> NeutralDocumentHandlerSubjects => [.. NeutralDocumentHandlers.Select(_ => _.Key.Subject.Value)];

    IReadOnlyList<string> HandlerMethodNames(string typeName) =>
    [
        .. SagaHandlers
            .Select(_ => _.Variants.Single().Definition.Name)
            .Where(_ => _.StartsWith($"{typeName}.", StringComparison.Ordinal))
            .Select(_ => _[$"{typeName}.".Length.._.IndexOf('(')])
            .Order(StringComparer.Ordinal)
    ];

    IReadOnlyList<ResolvedRelationship> DocumentRelationshipsFrom(string handlerName) =>
    [
        .. _result.Graph.Relationships.Where(_ =>
            _.Key.Source == Handler(handlerName).Key.Subject &&
            _.Key.Kind is RelationshipKind.Stores or RelationshipKind.Updates or RelationshipKind.Deletes)
    ];

    IReadOnlyList<string> ArtifactNames(ArtifactKind kind) => ArtifactNames(_result, kind);

    static IReadOnlyList<string> ArtifactNames(GeneratedScreenplayDefinition result, ArtifactKind kind) =>
    [
        .. result.Graph.Artifacts
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
            .Where(_ => _.Code == SagaLifecycleRealization || _.Code == WolverineDiagnosticCodes.SagaCorrelationRuntime || _.Code == WolverineDiagnosticCodes.SagaRoleUnresolved)
            .Select(_ => $"{_.Code}|{_.Subject.Value}|{_.Source?.Path}|{_.Source?.StartLine}|{_.Source?.StartColumn}|{_.Message}")
            .Order(StringComparer.Ordinal)
    ];

    static IReadOnlyList<string> ConflictingIdentityDiagnosticSignatures(GeneratedScreenplayDefinition result) =>
    [
        .. result.Diagnostics
            .Where(_ => _.Code == WolverineDiagnosticCodes.SagaCorrelationRuntime && _.Message.Contains("conflicting [SagaIdentityFrom]", StringComparison.Ordinal))
            .Select(_ => $"{_.Subject.Value}|{_.Source?.Path}|{_.Source?.StartLine}|{_.Source?.StartColumn}|{_.Message}")
            .Order(StringComparer.Ordinal)
    ];

    static string ScreenplayHash(GeneratedScreenplayDefinition result) =>
        Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(result.Source)));
}
