// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Cratis.CritterStack.Screenplay.for_CritterStackScreenplayGenerator;

public class when_generating_a_flat_feature_namespace : given.a_community_kitchen_application
{
    GeneratedScreenplayDefinition _result = null!;

    void Because() => _result = new CritterStackScreenplayGenerator().Generate(
        [CreateFlatProject()],
        new CritterStackScreenplayOptions { Domain = "CommunityKitchen" });

    [Fact] void should_succeed() => _result.IsSuccess.ShouldBeTrue();
    [Fact] void should_not_report_conflicting_slice_kinds() => _result.Diagnostics.ShouldNotContain(_ => _.Code == GenerationDiagnosticCodes.ConflictingSliceKind);
    [Fact] void should_generate_the_commands() => _result.Source.ShouldContain("command PlanBatch");
    [Fact] void should_generate_the_read_model() => _result.Source.ShouldContain("readmodel BatchCard");
    [Fact] void should_split_a_command_into_its_own_state_change_slice() => PlacementOf(_result, ArtifactKind.Command, "PlanBatch").ShouldEqual("Application/PlanBatch:StateChange");
    [Fact] void should_keep_a_produced_event_with_its_command() => PlacementOf(_result, ArtifactKind.Event, "BatchPlanned").ShouldEqual("Application/PlanBatch:StateChange");
    [Fact] void should_split_the_read_model_into_its_own_state_view_slice() => PlacementOf(_result, ArtifactKind.ReadModel, "BatchCard").ShouldEqual("Application/BatchCard:StateView");
    [Fact] void should_keep_the_linked_production() => Production(_result, "PlanBatch", "BatchPlanned")!.Key.Discriminator.ShouldEqual("declarative");
    [Fact] void should_explain_the_split_and_the_placement_options() => _result.Diagnostics.ShouldContain(_ =>
        _.Code == CritterStackDiagnosticCodes.MixedSliceKindsSplit &&
        _.Severity == GenerationDiagnosticSeverity.Warning &&
        _.Message.Contains("'Application.Kitchen'", StringComparison.Ordinal) &&
        _.Message.Contains("--skip-segments", StringComparison.Ordinal));
}
