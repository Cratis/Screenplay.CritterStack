// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Cratis.CritterStack.Screenplay.Canonical;

namespace Cratis.CritterStack.Screenplay.for_Expectations;

public class when_checking_artifact_expectations : given.a_canonical_kitchen_result
{
    const string AdapterId = "cratis.critter-stack";
    const string PlanPath = "Application/Kitchen/Planning/PlanBatch.cs";
    const string CardPath = "Application/Kitchen/Cards/BatchCard.cs";

    void Because() => Evaluate(
        (ExpectationKind.Artifact, "Command:PlanBatch"),
        (ExpectationKind.Artifact, "Event:PlanBatch"),
        (ExpectationKind.Artifact, "Command:Plan"),
        (ExpectationKind.Artifact, "PlanBatch"),
        (ExpectationKind.ArtifactCount, "Command|6"),
        (ExpectationKind.ArtifactCount, "Event|6"),
        (ExpectationKind.ArtifactCount, "Command|5"),
        (ExpectationKind.ArtifactCount, "Bogus|1"),
        (ExpectationKind.ArtifactCount, "Command|six"),
        (ExpectationKind.ArtifactCount, "Command"),
        (ExpectationKind.ArtifactCount, "Command|6|x"),
        (ExpectationKind.Evidence, $"Event:BatchPlanned|{AdapterId}|{PlanPath}"),
        (ExpectationKind.Evidence, $"Event:BatchPlanned|{AdapterId}|{CardPath}"),
        (ExpectationKind.Evidence, $"Event:BatchPlanned|other.adapter|{PlanPath}"),
        (ExpectationKind.Evidence, $"Event:BatchPlanned|{AdapterId}|Application/Kitchen"),
        (ExpectationKind.Evidence, $"Event:Missing|{AdapterId}|{PlanPath}"),
        (ExpectationKind.Evidence, $"Event:BatchPlanned|{AdapterId}"),
        (ExpectationKind.EvidenceCount, "Event:BatchPlanned|2"),
        (ExpectationKind.EvidenceCount, "Event:BatchPlanned|1"),
        (ExpectationKind.EvidenceCount, "Event:BatchPlanned|two"),
        (ExpectationKind.EvidenceCount, "Event:Missing|1"),
        (ExpectationKind.EvidenceCount, "Event:BatchPlanned"),
        (ExpectationKind.Identifier, "Command:PlanBatch:id"),
        (ExpectationKind.Identifier, "Document:BatchCard:id"),
        (ExpectationKind.Identifier, "Command:PlanBatch:recipe"),
        (ExpectationKind.Identifier, "Command:PlanBatch:missing"),
        (ExpectationKind.Identifier, "ReadModel:BatchCard:id"),
        (ExpectationKind.Identifier, "Command:PlanBatch"),
        (ExpectationKind.NotIdentifier, "Command:PlanBatch:recipe"),
        (ExpectationKind.NotIdentifier, "Command:PlanBatch:id"),
        (ExpectationKind.NotIdentifier, "Command:PlanBatch"));

    [Fact] void should_meet_an_artifact_with_that_kind_and_name() => Met(ExpectationKind.Artifact, "Command:PlanBatch").ShouldBeTrue();
    [Fact] void should_not_meet_an_artifact_with_another_kind() => Met(ExpectationKind.Artifact, "Event:PlanBatch").ShouldBeFalse();
    [Fact] void should_not_meet_an_artifact_that_is_only_a_name_prefix() => Met(ExpectationKind.Artifact, "Command:Plan").ShouldBeFalse();
    [Fact] void should_not_meet_an_artifact_without_a_kind() => Met(ExpectationKind.Artifact, "PlanBatch").ShouldBeFalse();
    [Fact] void should_meet_the_artifact_count_of_a_kind() => Met(ExpectationKind.ArtifactCount, "Command|6").ShouldBeTrue();
    [Fact] void should_meet_the_artifact_count_of_another_kind() => Met(ExpectationKind.ArtifactCount, "Event|6").ShouldBeTrue();
    [Fact] void should_not_meet_a_different_artifact_count() => Met(ExpectationKind.ArtifactCount, "Command|5").ShouldBeFalse();
    [Fact] void should_not_meet_an_artifact_count_of_an_unknown_kind() => Met(ExpectationKind.ArtifactCount, "Bogus|1").ShouldBeFalse();
    [Fact] void should_not_meet_an_artifact_count_that_is_not_a_number() => Met(ExpectationKind.ArtifactCount, "Command|six").ShouldBeFalse();
    [Fact] void should_not_meet_an_artifact_count_without_a_count() => Met(ExpectationKind.ArtifactCount, "Command").ShouldBeFalse();
    [Fact] void should_not_meet_an_artifact_count_with_an_extra_part() => Met(ExpectationKind.ArtifactCount, "Command|6|x").ShouldBeFalse();
    [Fact] void should_meet_evidence_from_the_adapter_at_the_first_path() => Met(ExpectationKind.Evidence, $"Event:BatchPlanned|{AdapterId}|{PlanPath}").ShouldBeTrue();
    [Fact] void should_meet_evidence_from_the_adapter_at_the_second_path() => Met(ExpectationKind.Evidence, $"Event:BatchPlanned|{AdapterId}|{CardPath}").ShouldBeTrue();
    [Fact] void should_not_meet_evidence_from_another_adapter() => Met(ExpectationKind.Evidence, $"Event:BatchPlanned|other.adapter|{PlanPath}").ShouldBeFalse();
    [Fact] void should_not_meet_evidence_at_a_path_that_is_only_a_prefix() => Met(ExpectationKind.Evidence, $"Event:BatchPlanned|{AdapterId}|Application/Kitchen").ShouldBeFalse();
    [Fact] void should_not_meet_evidence_for_an_artifact_that_is_missing() => Met(ExpectationKind.Evidence, $"Event:Missing|{AdapterId}|{PlanPath}").ShouldBeFalse();
    [Fact] void should_not_meet_evidence_without_a_path() => Met(ExpectationKind.Evidence, $"Event:BatchPlanned|{AdapterId}").ShouldBeFalse();
    [Fact] void should_meet_the_evidence_count_across_variants() => Met(ExpectationKind.EvidenceCount, "Event:BatchPlanned|2").ShouldBeTrue();
    [Fact] void should_not_meet_a_different_evidence_count() => Met(ExpectationKind.EvidenceCount, "Event:BatchPlanned|1").ShouldBeFalse();
    [Fact] void should_not_meet_an_evidence_count_that_is_not_a_number() => Met(ExpectationKind.EvidenceCount, "Event:BatchPlanned|two").ShouldBeFalse();
    [Fact] void should_not_meet_an_evidence_count_for_an_artifact_that_is_missing() => Met(ExpectationKind.EvidenceCount, "Event:Missing|1").ShouldBeFalse();
    [Fact] void should_not_meet_an_evidence_count_without_a_count() => Met(ExpectationKind.EvidenceCount, "Event:BatchPlanned").ShouldBeFalse();
    [Fact] void should_meet_an_identifier_property_of_a_command() => Met(ExpectationKind.Identifier, "Command:PlanBatch:id").ShouldBeTrue();
    [Fact] void should_meet_an_identifier_property_of_a_document() => Met(ExpectationKind.Identifier, "Document:BatchCard:id").ShouldBeTrue();
    [Fact] void should_not_meet_an_identifier_that_is_an_ordinary_property() => Met(ExpectationKind.Identifier, "Command:PlanBatch:recipe").ShouldBeFalse();
    [Fact] void should_not_meet_an_identifier_that_is_not_declared() => Met(ExpectationKind.Identifier, "Command:PlanBatch:missing").ShouldBeFalse();
    [Fact] void should_not_meet_an_identifier_property_on_a_variant_that_does_not_mark_it() => Met(ExpectationKind.Identifier, "ReadModel:BatchCard:id").ShouldBeFalse();
    [Fact] void should_not_meet_an_identifier_without_a_property() => Met(ExpectationKind.Identifier, "Command:PlanBatch").ShouldBeFalse();
    [Fact] void should_meet_a_not_identifier_for_an_ordinary_property() => Met(ExpectationKind.NotIdentifier, "Command:PlanBatch:recipe").ShouldBeTrue();
    [Fact] void should_not_meet_a_not_identifier_for_an_identifier_property() => Met(ExpectationKind.NotIdentifier, "Command:PlanBatch:id").ShouldBeFalse();
    [Fact] void should_meet_a_not_identifier_that_is_malformed() => Met(ExpectationKind.NotIdentifier, "Command:PlanBatch").ShouldBeTrue();
}
