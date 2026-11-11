// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Cratis.CritterStack.Screenplay.Canonical;

namespace Cratis.CritterStack.Screenplay.for_Expectations;

public class when_checking_relationship_expectations : given.a_canonical_kitchen_result
{
    [Fact] void should_meet_a_relationship_kind_that_exists() => Met(ExpectationKind.Relationship, "Produces").ShouldBeTrue();
    [Fact] void should_not_meet_a_relationship_kind_that_does_not_exist() => Met(ExpectationKind.Relationship, "Bogus").ShouldBeFalse();
    [Fact] void should_not_meet_a_relationship_kind_that_is_only_a_prefix() => Met(ExpectationKind.Relationship, "Produce").ShouldBeFalse();
    [Fact] void should_meet_a_relationship_between_the_source_and_the_target() => Met(ExpectationKind.Relationship, "Produces Command:PlanBatch -> Event:BatchPlanned").ShouldBeTrue();
    [Fact] void should_not_meet_a_relationship_to_another_target() => Met(ExpectationKind.Relationship, "Produces Command:PlanBatch -> Event:BatchServed").ShouldBeFalse();
    [Fact] void should_not_meet_a_relationship_in_the_opposite_direction() => Met(ExpectationKind.Relationship, "Produces Event:BatchPlanned -> Command:PlanBatch").ShouldBeFalse();
    [Fact] void should_not_meet_a_relationship_of_another_kind() => Met(ExpectationKind.Relationship, "Consumes Command:PlanBatch -> Event:BatchPlanned").ShouldBeFalse();
    [Fact] void should_not_meet_a_relationship_of_an_unknown_kind() => Met(ExpectationKind.Relationship, "Bogus Command:PlanBatch -> Event:BatchPlanned").ShouldBeFalse();
    [Fact] void should_not_meet_a_relationship_without_an_arrow() => Met(ExpectationKind.Relationship, "Produces Command:PlanBatch").ShouldBeFalse();
    [Fact] void should_not_meet_a_relationship_whose_target_lacks_a_kind() => Met(ExpectationKind.Relationship, "Produces Command:PlanBatch -> BatchPlanned").ShouldBeFalse();
    [Fact] void should_meet_the_relationship_count_between_the_source_and_the_target() => Met(ExpectationKind.RelationshipCount, "Produces|Command:PlanBatch|Event:BatchPlanned|1").ShouldBeTrue();
    [Fact] void should_meet_a_relationship_count_of_zero_for_an_unrelated_pair() => Met(ExpectationKind.RelationshipCount, "Produces|Command:PlanBatch|Event:BatchServed|0").ShouldBeTrue();
    [Fact] void should_meet_the_relationship_count_of_another_kind() => Met(ExpectationKind.RelationshipCount, "Reads|Command:ServeBatch|ReadModel:SoupBatch|1").ShouldBeTrue();
    [Fact] void should_not_meet_a_different_relationship_count() => Met(ExpectationKind.RelationshipCount, "Produces|Command:PlanBatch|Event:BatchPlanned|2").ShouldBeFalse();
    [Fact] void should_not_meet_a_relationship_count_of_zero_for_a_related_pair() => Met(ExpectationKind.RelationshipCount, "Produces|Command:PlanBatch|Event:BatchPlanned|0").ShouldBeFalse();
    [Fact] void should_not_meet_a_relationship_count_of_an_unknown_kind() => Met(ExpectationKind.RelationshipCount, "Bogus|Command:PlanBatch|Event:BatchPlanned|1").ShouldBeFalse();
    [Fact] void should_not_meet_a_relationship_count_that_is_not_a_number() => Met(ExpectationKind.RelationshipCount, "Produces|Command:PlanBatch|Event:BatchPlanned|one").ShouldBeFalse();
    [Fact] void should_not_meet_a_relationship_count_without_a_count() => Met(ExpectationKind.RelationshipCount, "Produces|Command:PlanBatch|Event:BatchPlanned").ShouldBeFalse();
}
