// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Cratis.CritterStack.Screenplay.for_CritterStackScreenplayAdapter;

public class when_analyzing_marten_documents : given.a_marten_document_application
{
    ResolvedApplicationGraph _graph = null!;

    void Because() => _graph = new GenerationResolver().Resolve([Contribution]);

    [Fact] void should_discover_explicitly_used_and_configured_documents() => _graph.Artifacts.Count(_ => _.Key.Kind == ArtifactKind.Document).ShouldEqual(3);
    [Fact] void should_keep_inherited_members_in_the_document_shape() => Document.Properties.Select(_ => _.Name).ShouldContainOnly("id", "studentNumber", "name");
    [Fact] void should_mark_the_configured_inherited_identity_member() => Document.Properties.Single(_ => _.Name == "studentNumber").IsIdentifier.ShouldBeTrue();
    [Fact] void should_not_mark_the_conventional_member_after_an_explicit_override() => Document.Properties.Single(_ => _.Name == "id").IsIdentifier.ShouldBeFalse();
    [Fact] void should_not_guess_an_identifier_for_an_unresolved_configuration() => UnresolvedDocument.Properties.Any(_ => _.IsIdentifier).ShouldBeFalse();
    [Fact] void should_not_mark_a_shadowed_base_identity_member_as_the_derived_property() => ShadowedDocument.Properties.Any(_ => _.IsIdentifier).ShouldBeFalse();
    [Fact] void should_emit_only_the_visible_shadowing_property() => ShadowedDocument.Properties.Count(_ => _.Name == "studentNumber").ShouldEqual(1);
    [Fact] void should_retain_configured_document_evidence() => DocumentEvidence.Single().Strength.ShouldEqual(EvidenceStrength.Configured);
    [Fact] void should_record_store_operations() => Relationships(RelationshipKind.Stores).Count.ShouldEqual(1);
    [Fact] void should_record_delete_operations() => Relationships(RelationshipKind.Deletes).Count.ShouldEqual(1);
    [Fact] void should_record_query_operations() => Relationships(RelationshipKind.Reads).Count.ShouldEqual(1);
    [Fact] void should_not_invent_an_event_built_read_model() => _graph.Artifacts.Any(_ => _.Key.Kind == ArtifactKind.ReadModel).ShouldBeFalse();
    [Fact] void should_report_each_ordinary_document_language_gap() => Contribution.Diagnostics.Count(_ => _.Code == MartenDiagnosticCodes.DocumentModelOmitted).ShouldEqual(3);
    [Fact] void should_diagnose_unresolved_and_ambiguous_identity_configurations_without_guessing() => Contribution.Diagnostics.Count(_ => _.Code == MartenDiagnosticCodes.DocumentIdentityUnresolved).ShouldEqual(2);
    [Fact] void should_not_discover_a_document_from_a_generated_syntax_tree() => _graph.Artifacts.Any(_ => _.Key.Kind == ArtifactKind.Document && _.Variants.Single().Definition.Name == "GeneratedStudent").ShouldBeFalse();
    [Fact] void should_not_contribute_a_fact_for_a_generated_document() => Contribution.Facts.OfType<ArtifactFact>().Any(_ => _.Definition.Name == "GeneratedStudent").ShouldBeFalse();
    [Fact] void should_not_report_a_document_model_omission_for_a_generated_document() => Contribution.Diagnostics.Any(_ => _.Code == MartenDiagnosticCodes.DocumentModelOmitted && _.Message.Contains("GeneratedStudent", StringComparison.Ordinal)).ShouldBeFalse();

    IReadOnlyList<Evidence> DocumentEvidence => Artifact("Student").Variants.Single().Evidence;
    ArtifactDefinition Document => Artifact("Student").Variants.Single().Definition;
    ArtifactDefinition UnresolvedDocument => Artifact("UnresolvedStudent").Variants.Single().Definition;
    ArtifactDefinition ShadowedDocument => Artifact("ShadowedStudent").Variants.Single().Definition;

    ResolvedArtifact Artifact(string name) => _graph.Artifacts.Single(_ =>
        _.Key.Kind == ArtifactKind.Document && _.Variants.Single().Definition.Name == name);

    IReadOnlyList<ResolvedRelationship> Relationships(RelationshipKind kind) =>
        [.. _graph.Relationships.Where(_ => _.Key.Kind == kind)];
}
