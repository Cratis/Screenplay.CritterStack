// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Cratis.CritterStack.Screenplay.for_CritterStackScreenplayAdapter;

public class when_analyzing_wolverine_marten_ops : given.a_wolverine_marten_ops_application
{
    ResolvedApplicationGraph _graph = null!;

    void Because() => _graph = new GenerationResolver().Resolve([Contribution]);

    [Fact] void should_compile_the_fixture() => Project.Compilation.GetDiagnostics(TestContext.Current.CancellationToken).Where(_ => _.Severity == DiagnosticSeverity.Error).ShouldBeEmpty();
    [Fact] void should_store_the_document_for_store_and_insert() => Operations(RelationshipKind.Stores).Where(_ => !_.Definition.IsCollection).Select(SourceName).ShouldContainOnly("StoreRequested", "InsertRequested");
    [Fact] void should_store_many_documents_in_each_tuple_slot() => Operations(RelationshipKind.Stores).Where(_ => _.Definition.IsCollection).Select(_ => _.Definition.Key.Discriminator).ShouldContainOnly("marten-op:0", "marten-op:1");
    [Fact] void should_update_the_document_in_a_task_tuple() => Operations(RelationshipKind.Updates).Select(SourceName).ShouldContainOnly("UpdateRequested");
    [Fact] void should_delete_documents_by_entity_and_identity() => Operations(RelationshipKind.Deletes).Select(SourceName).ShouldContainOnly("UpdateRequested", "DeleteRequested");
    [Fact] void should_start_streams_for_both_tenant_forms() => Operations(RelationshipKind.StartsStream).Select(SourceName).ShouldContainOnly("StartRequested", "TenantStartRequested");
    [Fact] void should_recover_the_appended_events() => Relationships(RelationshipKind.Produces).Select(SourceName).ShouldContainOnly("StartRequested", "TenantStartRequested");
    [Fact] void should_not_cascade_operation_wrappers() => Relationships(RelationshipKind.Cascades).ShouldBeEmpty();
    [Fact] void should_not_invent_operations_for_unknown_or_unreturned_values() => Operations().Select(SourceName).ShouldNotContain(_ => _ == "UnknownRequested" || _ == "UnreturnedRequested");
    [Fact] void should_keep_exact_operation_evidence() => Operations().All(_ => _.Evidence.Strength == EvidenceStrength.Exact).ShouldBeTrue();
    [Fact] void should_not_create_operation_wrapper_events() => _graph.Artifacts.Where(_ => _.Key.Kind == ArtifactKind.Event).Select(_ => _.Variants.Single().Definition.Name).ShouldContainOnly("Started");
    [Fact] void should_classify_document_operations_as_commands() => _graph.Artifacts.Where(_ => _.Key.Kind == ArtifactKind.Command).Select(_ => _.Variants.Single().Definition.Name).ShouldContain("StoreRequested");

    IReadOnlyList<RelationshipFact> Operations(RelationshipKind? kind = null) =>
        [.. Contribution.Facts.OfType<RelationshipFact>().Where(_ => _.Definition.Key.Discriminator?.StartsWith("marten-op:", StringComparison.Ordinal) == true && (kind is null || _.Definition.Key.Kind == kind))];

    IReadOnlyList<RelationshipFact> Relationships(RelationshipKind kind) =>
        [.. Contribution.Facts.OfType<RelationshipFact>().Where(_ => _.Definition.Key.Kind == kind)];

    string SourceName(RelationshipFact relationship) =>
        _graph.Artifacts.Single(_ => _.Key.Subject == relationship.Definition.Key.Source).Variants.Single().Definition.Name;
}
