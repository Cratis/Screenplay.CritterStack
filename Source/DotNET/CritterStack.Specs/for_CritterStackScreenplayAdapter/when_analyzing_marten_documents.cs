// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Cratis.CritterStack.Screenplay.for_CritterStackScreenplayAdapter;

public class when_analyzing_marten_documents : given.a_marten_document_application
{
    ResolvedApplicationGraph _graph = null!;

    void Because() => _graph = new GenerationResolver().Resolve([Contribution]);

    [Fact] void should_discover_the_document_once() => _graph.Artifacts.Count(_ => _.Key.Kind == ArtifactKind.Document).ShouldEqual(1);
    [Fact] void should_keep_the_document_shape() => Document.Properties.Select(_ => _.Name).ShouldContainOnly("id", "name");
    [Fact] void should_record_store_operations() => Relationships(RelationshipKind.Stores).Count.ShouldEqual(1);
    [Fact] void should_record_delete_operations() => Relationships(RelationshipKind.Deletes).Count.ShouldEqual(1);
    [Fact] void should_record_query_operations() => Relationships(RelationshipKind.Reads).Count.ShouldEqual(1);
    [Fact] void should_not_invent_an_event_built_read_model() => _graph.Artifacts.Any(_ => _.Key.Kind == ArtifactKind.ReadModel).ShouldBeFalse();
    [Fact] void should_report_the_screenplay_language_gap_once() => Contribution.Diagnostics.Select(_ => _.Code).ShouldContainOnly(MartenDiagnosticCodes.DocumentModelOmitted);

    ArtifactDefinition Document => _graph.Artifacts.Single(_ => _.Key.Kind == ArtifactKind.Document).Variants.Single().Definition;

    IReadOnlyList<ResolvedRelationship> Relationships(RelationshipKind kind) =>
        [.. _graph.Relationships.Where(_ => _.Key.Kind == kind)];
}
