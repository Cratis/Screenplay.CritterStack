// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Cratis.CritterStack.Screenplay.for_CritterStackScreenplayAdapter;

public class when_analyzing_marten_explicit_event_projection : given.a_marten_explicit_event_projection_application
{
    ResolvedApplicationGraph _graph = null!;

    void Because() => _graph = new GenerationResolver().Resolve([Contribution]);

    [Fact] void should_emit_the_explicit_projection() => Artifacts(ArtifactKind.Projection).Select(NameOf).ShouldContainOnly("ImportProjection");
    [Fact] void should_bind_operations_to_the_switch_event_types() => Targets(RelationshipKind.Consumes, ArtifactKind.Event).ShouldContainOnly("Imported", "Removed");
    [Fact] void should_emit_each_exact_document_target() => Artifacts(ArtifactKind.Document).Select(NameOf).ShouldContainOnly("ImportView", "ImportStatus", "ImportAudit");
    [Fact] void should_preserve_store_update_and_delete_operations() => Targets(RelationshipKind.Stores, ArtifactKind.Document).ShouldContainOnly("ImportView");
    [Fact] void should_preserve_update_operations() => Targets(RelationshipKind.Updates, ArtifactKind.Document).ShouldContainOnly("ImportStatus");
    [Fact] void should_preserve_delete_and_delete_where_operations() => Targets(RelationshipKind.Deletes, ArtifactKind.Document).ShouldContainOnly("ImportView", "ImportAudit");
    [Fact] void should_bind_store_operations_to_the_imported_event() => EventBoundTargets(RelationshipKind.Stores, "Imported").ShouldContainOnly("ImportView");
    [Fact] void should_bind_update_operations_to_the_imported_event() => EventBoundTargets(RelationshipKind.Updates, "Imported").ShouldContainOnly("ImportStatus");
    [Fact] void should_bind_delete_operations_to_the_removed_event() => EventBoundTargets(RelationshipKind.Deletes, "Removed").ShouldContainOnly("ImportView", "ImportAudit");
    [Fact] void should_not_use_teardown_as_operation_evidence() => Artifacts(ArtifactKind.Document).Select(NameOf).ShouldNotContain("TeardownOnly");
    [Fact] void should_not_infer_event_flow_from_an_arbitrary_switch_value() => Artifacts(ArtifactKind.Document).Select(NameOf).ShouldNotContain("HiddenDocument");
    [Fact] void should_not_fabricate_read_models_for_documents() => Artifacts(ArtifactKind.ReadModel).ShouldBeEmpty();
    [Fact] void should_report_the_unrepresented_body_and_value_flow() => Contribution.Diagnostics.Count(_ => _.Code == MartenDiagnosticCodes.EventProjectionOmitted).ShouldEqual(1);
    [Fact] void should_report_each_document_state_gap() => Contribution.Diagnostics.Count(_ => _.Code == MartenDiagnosticCodes.DocumentModelOmitted).ShouldEqual(3);

    IReadOnlyList<ResolvedArtifact> Artifacts(ArtifactKind kind) => [.. _graph.Artifacts.Where(_ => _.Key.Kind == kind)];

    IReadOnlyList<string> Targets(RelationshipKind kind, ArtifactKind targetKind)
    {
        var projection = Artifacts(ArtifactKind.Projection).Single();
        var targets = _graph.Relationships
            .Where(_ => _.Key.Kind == kind && _.Key.Source == projection.Key.Subject)
            .Select(_ => _.Key.Target)
            .ToHashSet();

        return
        [
            .. Artifacts(targetKind)
                .Where(_ => targets.Contains(_.Key.Subject))
                .Select(NameOf)
        ];
    }

    IReadOnlyList<string> EventBoundTargets(RelationshipKind kind, string eventName)
    {
        var eventSubject = Artifacts(ArtifactKind.Event).Single(_ => NameOf(_) == eventName).Key.Subject.Value;
        var targets = _graph.Relationships
            .Where(_ => _.Key.Kind == kind && _.Key.Discriminator == eventSubject)
            .Select(_ => _.Key.Target)
            .ToHashSet();
        return [.. Artifacts(ArtifactKind.Document).Where(_ => targets.Contains(_.Key.Subject)).Select(NameOf)];
    }

    static string NameOf(ResolvedArtifact artifact) => artifact.Variants.Single().Definition.Name;
}
