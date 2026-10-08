// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Cratis.CritterStack.Screenplay.for_CritterStackScreenplayGenerator;

public class when_generating_fetched_wolverine_event_streams : given.a_wolverine_fetch_many_application
{
    GeneratedScreenplayDefinition _result = null!;

    void Because() => _result = new CritterStackScreenplayGenerator().Generate([Project], new CritterStackScreenplayOptions { Domain = "Batches" });

    [Fact] void should_have_compiler_checked_input() => Project.Compilation.GetDiagnostics().Where(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error).ShouldBeEmpty();
    [Fact] void should_succeed() => _result.IsSuccess.ShouldBeTrue();
    [Fact] void should_recognize_inherited_guid_fetch_with_named_cancellation_and_configure_await() => Relationships("Move", RelationshipKind.Reads).Select(relationship => relationship.Definitions.Single().SourceMember).ShouldContainOnly(["fromId", "toId"]);
    [Fact] void should_target_the_debit_slot() => Append("Move", "Debit").Definitions.Single().SourceMember.ShouldEqual("fromId");
    [Fact] void should_target_a_direct_indexed_local() => Append("Move", "Credit").Definitions.Single().SourceMember.ShouldEqual("toId");
    [Fact] void should_retain_both_targets_for_one_event_type() => Relationships("Move", RelationshipKind.Appends).Count(relationship => relationship.Key.Discriminator!.Contains("Moved", StringComparison.Ordinal)).ShouldEqual(2);
    [Fact] void should_produce_the_shared_event_only_once() => Relationships("Move", RelationshipKind.Produces).Count(relationship => relationship.Key.Target.Value.EndsWith(".Moved", StringComparison.Ordinal)).ShouldEqual(1);
    [Fact] void should_keep_ordinary_returns_as_cascades() => Relationships("Move", RelationshipKind.Cascades).Single().Key.Target.Value.ShouldContain("FollowUp");
    [Fact] void should_keep_ordinary_returns_out_of_events() => EventNames.ShouldNotContain("FollowUp");
    [Fact] void should_quantify_runtime_reads() => Relationships("Cancel", RelationshipKind.Reads).Single().Definitions.Single().IsCollection.ShouldBeTrue();
    [Fact] void should_quantify_runtime_appends() => Append("Cancel", "Cancelled").Definitions.Single().IsCollection.ShouldBeTrue();
    [Fact] void should_retain_the_runtime_ids_member() => Append("Cancel", "Cancelled").Definitions.Single().SourceMember.ShouldEqual("ids");
    [Fact] void should_admit_the_string_overload() => Relationships("Close", RelationshipKind.Reads).Select(relationship => relationship.Definitions.Single().SourceMember).ShouldContainOnly(["fromId", "toId"]);
    [Fact] void should_expand_a_fixed_foreach_into_distinct_slots() => Relationships("Close", RelationshipKind.Appends).Select(relationship => relationship.Definitions.Single().SourceMember).ShouldContainOnly(["fromId", "toId"]);
    [Fact] void should_not_quantify_fixed_slots() => Relationships("Close", RelationshipKind.Appends).Any(relationship => relationship.Definitions.Single().IsCollection).ShouldBeFalse();
    [Fact] void should_not_misattribute_another_parameter_property_to_the_command() => Relationships("ForeignIdentity", RelationshipKind.Reads).Single().Definitions.Single().SourceMember.ShouldBeNull();
    [Fact] void should_preserve_a_proven_foreign_identity_append_without_a_command_member() => Append("ForeignIdentity", "ForeignEvent").Definitions.Single().SourceMember.ShouldBeNull();
    [Fact] void should_keep_fetches_without_writes() => Relationships("Inspect", RelationshipKind.Reads).Count.ShouldEqual(1);
    [Fact] void should_not_invent_writes_for_read_only_fetches() => Relationships("Inspect", RelationshipKind.Appends).ShouldBeEmpty();
    [Fact] void should_not_invent_streams_for_empty_inputs() => Relationships("Empty", RelationshipKind.Reads).ShouldBeEmpty();
    [Fact] void should_keep_several_fetch_sites_distinct() => Relationships("Several", RelationshipKind.Reads).Select(relationship => relationship.Key.Discriminator).Distinct().Count().ShouldEqual(2);
    [Fact] void should_keep_the_same_event_at_several_fetch_sites() => Relationships("Several", RelationshipKind.Appends).Count.ShouldEqual(2);
    [Fact] void should_distinguish_overloaded_handlers() => Relationships("Overload", RelationshipKind.Reads).Select(relationship => relationship.Key.Discriminator).Distinct().Count().ShouldEqual(2);
    [Fact] void should_not_mark_any_batch_identity_globally() => _result.Graph.Artifacts.Where(artifact => artifact.Key.Kind == ArtifactKind.Command && _result.Graph.Relationships.Any(relationship => relationship.Key.Kind == RelationshipKind.Reads && relationship.Key.Source == artifact.Key.Subject)).SelectMany(artifact => artifact.Variants).SelectMany(variant => variant.Definition.Properties).Any(property => property.IsIdentifier).ShouldBeFalse();
    [Fact] void should_not_invent_stream_or_list_artifacts() => _result.Graph.Artifacts.SelectMany(artifact => artifact.Variants).Any(variant => variant.Definition.Name.StartsWith("IEventStream", StringComparison.Ordinal) || variant.Definition.Name.StartsWith("IReadOnlyList", StringComparison.Ordinal)).ShouldBeFalse();
    [Fact] void should_not_manufacture_version_metadata() => _result.Diagnostics.Any(diagnostic => diagnostic.Code == WolverineDiagnosticCodes.StreamVersionOmitted).ShouldBeFalse();
    [Fact] void should_locate_loading_loss_at_authored_fetch_sites() => _result.Diagnostics.Where(diagnostic => diagnostic.Code == WolverineDiagnosticCodes.MultipleStreamMetadataOmitted).All(diagnostic => diagnostic.Source?.Path == "Batches/Handlers.cs" && diagnostic.Source.StartLine > 0).ShouldBeTrue();
    [Fact] void should_explain_quantified_identity_loss() => _result.Diagnostics.Any(diagnostic => diagnostic.Code == WolverineDiagnosticCodes.MultipleStreamMetadataOmitted && diagnostic.Message.Contains("unknown runtime cardinality", StringComparison.Ordinal)).ShouldBeTrue();
    [Fact] void should_exclude_generated_fetches() => EventNames.ShouldNotContain("GeneratedEvent");

    IReadOnlyList<string> EventNames => [.. _result.Graph.Artifacts.Where(artifact => artifact.Key.Kind == ArtifactKind.Event).Select(artifact => artifact.Variants[0].Definition.Name)];

    IReadOnlyList<ResolvedRelationship> Relationships(string command, RelationshipKind kind) =>
        [.. _result.Graph.Relationships.Where(relationship => relationship.Key.Kind == kind && relationship.Key.Source.Value.EndsWith($".{command}", StringComparison.Ordinal))];

    ResolvedRelationship Append(string command, string eventName) => Relationships(command, RelationshipKind.Appends).Single(relationship => relationship.Key.Discriminator!.Contains($".{eventName}", StringComparison.Ordinal));
}
