// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Cratis.CritterStack.Screenplay.for_CritterStackScreenplayAdapter;

public class when_deriving_shared_source_placement : given.a_shared_source_placement_application
{
    AdapterContribution _contribution = null!;
    ResolvedApplicationGraph _graph = null!;
    AdapterContribution _relocatedContribution = null!;
    IReadOnlyList<SubjectId> _sourceStructureSubjectsAfter = null!;
    IReadOnlyList<SubjectId> _sourceStructureSubjectsBefore = null!;

    void Because()
    {
        var context = new DotNetAnalysisContext([Project]);
        _sourceStructureSubjectsBefore = [.. DotNetSourceStructures.Create(context).Structures.Select(_ => _.Subject)];
        _contribution = Adapter.Analyze(context, AdapterOptions);
        _sourceStructureSubjectsAfter = [.. DotNetSourceStructures.Create(context).Structures.Select(_ => _.Subject)];
        _graph = new GenerationResolver().Resolve([_contribution]);
        _relocatedContribution = Adapter.Analyze(new([CreateProject(physicalRoot: "/relocated")]), AdapterOptions);
    }

    [Fact] void should_derive_without_source_placement_diagnostics() => _contribution.Diagnostics.Where(_ => _.Code.StartsWith("DOTNETSP", StringComparison.Ordinal)).ShouldBeEmpty();
    [Fact] void should_colocate_the_marten_read_model_and_wolverine_query() => Placement(ArtifactKind.Query, "GetOrder").ShouldEqual(Placement(ArtifactKind.ReadModel, "OrderSummary"));
    [Fact] void should_place_the_cross_framework_state_view_from_shared_source() => Placement(ArtifactKind.ReadModel, "OrderSummary").ShouldEqual(("Orders", "Summary", GenerationSliceKind.StateView));
    [Fact] void should_place_the_explicit_marten_reducer_from_its_projection_source() => Placement(ArtifactKind.Reducer, "OrderSummaryProjection").ShouldEqual(("Orders", "Projections", GenerationSliceKind.StateView));
    [Fact] void should_place_the_state_change_from_shared_source() => Placement(ArtifactKind.Command, "SubmitOrder").ShouldEqual(("Orders", "Submit", GenerationSliceKind.StateChange));
    [Fact] void should_place_the_synthetic_endpoint_command_from_shared_source() => Placement(ArtifactKind.Command, "Cancel").ShouldEqual(("Orders", "Submit", GenerationSliceKind.StateChange));
    [Fact] void should_place_the_state_change_event_from_its_own_source() => Placement(ArtifactKind.Event, "OrderSubmitted").ShouldEqual(("Orders", "Submit", GenerationSliceKind.StateChange));
    [Fact] void should_place_the_automation_from_its_authored_source() => Placement(ArtifactKind.Reaction, "Notification").ShouldEqual(("Orders", "Notify", GenerationSliceKind.Automation));
    [Fact] void should_keep_custom_projections_deliberately_unplaced() => _graph.Placements.Any(_ => _.Artifact == Artifact(ArtifactKind.Projection, "AuditProjection").Key).ShouldBeFalse();
    [Fact] void should_leave_the_fixed_source_structure_snapshot_unchanged() => _sourceStructureSubjectsAfter.SequenceEqual(_sourceStructureSubjectsBefore).ShouldBeTrue();
    [Fact] void should_not_rewrite_the_synthetic_reducer_subject() => PlacementFact(ArtifactKind.Reducer, "OrderSummarySnapshot").Artifact.ShouldEqual(Artifact(ArtifactKind.Reducer, "OrderSummarySnapshot").Key);
    [Fact] void should_not_rewrite_the_query_method_subject() => PlacementFact(ArtifactKind.Query, "GetOrder").Artifact.ShouldEqual(Artifact(ArtifactKind.Query, "GetOrder").Key);
    [Fact] void should_not_rewrite_the_reaction_method_subject() => PlacementFact(ArtifactKind.Reaction, "Notification").Artifact.ShouldEqual(Artifact(ArtifactKind.Reaction, "Notification").Key);
    [Fact] void should_keep_the_typed_command_self_owned() => PlacementEvidence(ArtifactKind.Command, "SubmitOrder").ShouldContain($"effectiveOwner={SubjectFor("Application.Orders.Submit.SubmitOrder").Value}");
    [Fact] void should_use_the_exact_containing_type_as_the_synthetic_endpoint_command_owner() => PlacementEvidence(ArtifactKind.Command, "Cancel").ShouldContain($"effectiveOwner={SubjectFor("Application.Orders.Submit.OrderCommandEndpoints").Value}");
    [Fact] void should_use_the_exact_model_as_the_synthetic_reducer_owner() => PlacementEvidence(ArtifactKind.Reducer, "OrderSummarySnapshot").ShouldContain($"effectiveOwner={SubjectFor("Application.Orders.Summary.OrderSummary").Value}");
    [Fact] void should_use_the_exact_projection_as_the_explicit_reducer_owner() => PlacementEvidence(ArtifactKind.Reducer, "OrderSummaryProjection").ShouldContain($"effectiveOwner={SubjectFor("Application.Orders.Projections.OrderSummaryProjection").Value}");
    [Fact] void should_use_the_exact_containing_type_as_the_query_owner() => PlacementEvidence(ArtifactKind.Query, "GetOrder").ShouldContain($"effectiveOwner={SubjectFor("Application.Orders.Summary.OrderEndpoints").Value}");
    [Fact] void should_use_the_exact_containing_type_as_the_reaction_owner() => PlacementEvidence(ArtifactKind.Reaction, "Notification").ShouldContain($"effectiveOwner={SubjectFor("Application.Orders.Notify.NotificationHandler").Value}");
    [Fact] void should_record_the_complete_strict_policy() => PlacementEvidence(ArtifactKind.Command, "SubmitOrder").ShouldContain("strictPolicy(version=1, featureRoot=Source, namespaceSegmentsToSkip=1, module=<absent>)");
    [Fact] void should_record_the_explicit_compatibility_policy_even_when_strict_wins() => PlacementEvidence(ArtifactKind.Command, "SubmitOrder").ShouldContain("compatibilityPolicy(version=1, placement=Application/Order/SubmitOrder:StateChange)");
    [Fact] void should_prefer_strict_placement_over_compatibility() => PlacementEvidence(ArtifactKind.Command, "SubmitOrder").ShouldContain("usedCompatibility=false; compatibilityReason=<none>");
    [Fact] void should_keep_the_evidence_explanation_stable_after_physical_relocation() => PlacementEvidence(_relocatedContribution, ArtifactKind.Query, "GetOrder").ShouldEqual(PlacementEvidence(ArtifactKind.Query, "GetOrder"));
    [Fact] void should_not_include_physical_paths_in_the_evidence_explanation() => new[] { PlacementEvidence(ArtifactKind.Query, "GetOrder"), PlacementEvidence(_relocatedContribution, ArtifactKind.Query, "GetOrder") }.Any(_ => _.Contains("/workspace", StringComparison.Ordinal) || _.Contains("/relocated", StringComparison.Ordinal)).ShouldBeFalse();

    (string Module, string Slice, GenerationSliceKind Kind) Placement(ArtifactKind kind, string name)
    {
        var placement = _graph.Placements
            .Single(_ => _.Artifact == Artifact(kind, name).Key)
            .EffectiveVariants
            .Single()
            .Placement;

        return (placement.Module, placement.Slice, placement.SliceKind);
    }

    ArtifactPlacementFact PlacementFact(ArtifactKind kind, string name) => _contribution.Facts
        .OfType<ArtifactPlacementFact>()
        .Single(_ => _.Artifact == Artifact(kind, name).Key);

    string PlacementEvidence(ArtifactKind kind, string name) => PlacementEvidence(_contribution, kind, name);

    string PlacementEvidence(AdapterContribution contribution, ArtifactKind kind, string name) => contribution.Facts
        .OfType<ArtifactPlacementFact>()
        .Single(_ => _.Artifact == Artifact(kind, name).Key)
        .Evidence
        .Explanation ?? string.Empty;

    SubjectId SubjectFor(string metadataName) => Project.SubjectForType(Project.Compilation.GetTypeByMetadataName(metadataName)!);

    ResolvedArtifact Artifact(ArtifactKind kind, string name) => _graph.Artifacts.Single(_ =>
        _.Key.Kind == kind &&
        _.Variants.Any(variant => variant.Definition.Name == name));
}
