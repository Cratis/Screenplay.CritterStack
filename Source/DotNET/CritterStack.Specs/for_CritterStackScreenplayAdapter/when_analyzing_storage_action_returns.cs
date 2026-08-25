// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Cratis.CritterStack.Screenplay.for_CritterStackScreenplayAdapter;

public class when_analyzing_storage_action_returns : given.a_storage_action_handler_application
{
    ResolvedApplicationGraph _graph = null!;

    void Because() => _graph = new GenerationResolver().Resolve([Contribution]);

    [Fact] void should_compile_the_fixture() => Project.Compilation.GetDiagnostics().Where(_ => _.Severity == Microsoft.CodeAnalysis.DiagnosticSeverity.Error).ShouldBeEmpty();
    [Fact] void should_store_each_returned_storage_action_document() => RelationshipTargets(RelationshipKind.Stores).ShouldContainOnly("ManifestDocument", "CountState", "ManifestDocument", "ManifestDocument");
    [Fact] void should_refine_each_delete_storage_factory() => RelationshipTargets(RelationshipKind.Deletes).ShouldContainOnly("ManifestDocument", "ManifestDocument");
    [Fact] void should_refine_the_update_storage_factory() => RelationshipTargets(RelationshipKind.Updates).ShouldContainOnly("ManifestDocument");
    [Fact] void should_preserve_the_outgoing_message_cascade() => RelationshipTargets(RelationshipKind.Cascades).ShouldContainOnly("CountsRecalculated");
    [Fact] void should_classify_storage_action_handlers_as_commands() => ArtifactNames(ArtifactKind.Command).ShouldContainOnly("ManifestPushed", "CountsChanged", "ManifestRemoved", "ManifestUpdated", "CustomStorageRequested", "StreamRequested", "MixedStorageActionsRequested");
    [Fact] void should_not_classify_storage_action_handlers_as_reactions() => ArtifactNames(ArtifactKind.Reaction).ShouldBeEmpty();
    [Fact] void should_not_classify_storage_action_documents_as_events() => ArtifactNames(ArtifactKind.Event).Any(_ => string.Equals(_, "ManifestDocument", StringComparison.Ordinal) || string.Equals(_, "CountState", StringComparison.Ordinal)).ShouldBeFalse();
    [Fact] void should_retain_exact_storage_action_evidence() => StorageActionRelationships.All(_ => _.Evidence.Strength == EvidenceStrength.Exact).ShouldBeTrue();
    [Fact] void should_retain_the_storage_action_slot() => StorageActionRelationships.Select(_ => _.Definition.Key.Discriminator).ShouldContainOnly("storage-action:0", "storage-action:1", "storage-action:0", "storage-action:0", "storage-action:0", "storage-action:0", "storage-action:1");
    [Fact] void should_refine_same_entity_tuple_slots_independently() => MixedStorageActionRelationships.Select(_ => _.Definition.Key.Kind).ShouldContainOnly(RelationshipKind.Stores, RelationshipKind.Deletes);
    [Fact] void should_omit_the_same_entity_nothing_slot() => MixedStorageActionRelationships.Select(_ => _.Definition.Key.Discriminator).ShouldNotContain("storage-action:2");
    [Fact] void should_not_store_a_start_stream_action_as_a_document() => StorageActionRelationships.Any(_ => SourceName(_) == "StreamRequested").ShouldBeFalse();
    [Fact] void should_classify_an_interface_implementation_as_a_storage_action() => StorageActionRelationships.Any(_ => SourceName(_) == "CustomStorageRequested").ShouldBeTrue();
    [Fact] void should_not_report_loss() => Contribution.Diagnostics.ShouldBeEmpty();

    IReadOnlyList<RelationshipFact> StorageActionRelationships =>
        [.. Contribution.Facts.OfType<RelationshipFact>().Where(_ => _.Definition.Key.Kind is RelationshipKind.Stores or RelationshipKind.Updates or RelationshipKind.Deletes)];

    IReadOnlyList<RelationshipFact> MixedStorageActionRelationships
    {
        get
        {
            var command = _graph.Artifacts.Single(_ => _.Key.Kind == ArtifactKind.Command && _.Variants.Single().Definition.Name == "MixedStorageActionsRequested");
            return [.. StorageActionRelationships.Where(_ => _.Definition.Key.Source == command.Key.Subject)];
        }
    }

    IReadOnlyList<string> RelationshipTargets(RelationshipKind kind) =>
        [.. _graph.Relationships
            .Where(_ => _.Key.Kind == kind)
            .Select(relationship => _graph.Artifacts.Single(artifact => artifact.Key.Subject == relationship.Key.Target).Variants.Single().Definition.Name)];

    IReadOnlyList<string> ArtifactNames(ArtifactKind kind) =>
        [.. _graph.Artifacts.Where(_ => _.Key.Kind == kind).Select(_ => _.Variants.Single().Definition.Name)];

    string SourceName(RelationshipFact relationship) =>
        _graph.Artifacts.Single(_ => _.Key.Subject == relationship.Definition.Key.Source).Variants.Single().Definition.Name;
}
