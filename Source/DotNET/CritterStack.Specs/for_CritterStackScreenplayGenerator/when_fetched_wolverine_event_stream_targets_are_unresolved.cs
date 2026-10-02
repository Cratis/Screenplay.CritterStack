// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Cratis.CritterStack.Screenplay.for_CritterStackScreenplayGenerator;

public class when_fetched_wolverine_event_stream_targets_are_unresolved : given.a_wolverine_fetch_many_application
{
    GeneratedScreenplayDefinition _result = null!;

    void Because() => _result = new CritterStackScreenplayGenerator().Generate([Project], new CritterStackScreenplayOptions { Domain = "Batches" });

    [Fact] void should_keep_independently_proven_reads() => Reads("Unresolved").Count.ShouldEqual(1);
    [Fact] void should_reject_dynamic_indices_aliases_helpers_and_opaque_payloads() => HasEvent("Unproven", "Opaque").ShouldBeFalse();
    [Fact] void should_reject_a_collection_root_that_escapes_into_an_arbitrary_alias() => HasEvent("EscapedEvent").ShouldBeFalse();
    [Fact] void should_reject_a_reassigned_collection_root() => HasEvent("ReassignedEvent").ShouldBeFalse();
    [Fact] void should_keep_reads_from_a_reassigned_collection_root() => Reads("Reassigned").Count.ShouldEqual(1);
    [Fact] void should_reject_a_reassigned_indexed_local() => HasEvent("ReassignedIndexedEvent").ShouldBeFalse();
    [Fact] void should_not_expand_single_fetch_support() => HasEvent("SingleEvent").ShouldBeFalse();
    [Fact] void should_not_infer_loading_for_single_fetch() => Reads("Single").ShouldBeEmpty();
    [Fact] void should_not_accept_same_named_methods() => HasEvent("ImpostorEvent").ShouldBeFalse();
    [Fact] void should_not_infer_loading_from_impostors() => Reads("Impostor").ShouldBeEmpty();
    [Fact] void should_not_deduplicate_repeated_authored_identities() => Reads("Repeated").Count.ShouldEqual(2);
    [Fact] void should_not_claim_valid_handles_for_repeated_identities() => HasEvent("RepeatAppended").ShouldBeFalse();
    [Fact] void should_explain_repeated_identity_runtime_rejection() => _result.Diagnostics.Any(diagnostic => diagnostic.Message.Contains("repeated authored identity expressions are not proven distinct", StringComparison.Ordinal)).ShouldBeTrue();
    [Fact] void should_report_each_unsupported_exact_append_once() => Unresolved.Count.ShouldEqual(11);
    [Fact] void should_locate_each_unsupported_occurrence() => Unresolved.Select(diagnostic => (diagnostic.Source!.Path, diagnostic.Source.StartLine, diagnostic.Source.StartColumn)).Distinct().Count().ShouldEqual(11);
    [Fact] void should_exclude_nested_callback_and_generated_occurrences() => Unresolved.All(diagnostic => diagnostic.Source?.Path == "Batches/Handlers.cs").ShouldBeTrue();

    IReadOnlyList<GenerationDiagnostic> Unresolved => [.. _result.Diagnostics.Where(diagnostic => diagnostic.Code == WolverineDiagnosticCodes.EventWriteTargetUnresolved)];
    IReadOnlyList<ResolvedRelationship> Reads(string command) => [.. _result.Graph.Relationships.Where(relationship => relationship.Key.Kind == RelationshipKind.Reads && relationship.Key.Source.Value.EndsWith($".{command}", StringComparison.Ordinal))];
    bool HasEvent(params string[] names) => _result.Graph.Artifacts.Any(artifact => artifact.Key.Kind == ArtifactKind.Event && names.Contains(artifact.Variants[0].Definition.Name, StringComparer.Ordinal));
}
