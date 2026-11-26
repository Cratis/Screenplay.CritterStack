// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Cratis.CritterStack.Screenplay.for_FactParity;

/// <summary>Real Vogen and the native effect-free HTTP command fragment compose to the compatibility result in either registration order.</summary>
public class when_native_http_commands_compose_with_vogen : given.a_native_http_command_with_vogen_comparison
{
    void Because() => ComposeInBothOrders();

    [Fact] void should_admit_both_producers_when_vogen_is_first() => BothProducersAdmitted(VogenThenWolverine).ShouldBeTrue();
    [Fact] void should_admit_both_producers_when_wolverine_is_first() => BothProducersAdmitted(WolverineThenVogen).ShouldBeTrue();
    [Fact] void should_match_the_oracle_when_vogen_is_first() => MatchesTheOracle(VogenThenWolverine).ShouldBeTrue();
    [Fact] void should_match_the_oracle_when_wolverine_is_first() => MatchesTheOracle(WolverineThenVogen).ShouldBeTrue();
    [Fact] void should_match_the_oracle_diagnostics_in_both_orders() => Orders.All(_ => Diagnostics(_).SequenceEqual(Diagnostics(Oracle))).ShouldBeTrue();
    [Fact] void should_produce_identical_output_in_both_orders() => VogenThenWolverine.Source.ShouldEqual(WolverineThenVogen.Source);
    [Fact] void should_contribute_a_represented_concept() => VogenContribution.Facts.OfType<ConceptRepresentationFact>().ShouldNotBeEmpty();
    [Fact] void should_contribute_native_command_surface() => WolverineContribution.Facts.OfType<ArtifactPlacementFact>().ShouldNotBeEmpty();
    [Fact] void should_emit_the_concept_and_the_command() => Orders.All(_ => _.Source.Contains("concept CustomerCode") && Commands(_).Count() == 1).ShouldBeTrue();
    [Fact]
    void should_bind_both_members_to_the_independent_concept() =>
        Orders.All(_ => CommandProperties(_).Count(property => property.Type.TargetArtifactKind == ArtifactKind.Concept) == 2).ShouldBeTrue();
    [Fact] void should_keep_the_optional_member() => Orders.All(_ => CommandProperties(_).Any(property => property.Type.IsOptional)).ShouldBeTrue();
    [Fact] void should_have_no_relationships() => Orders.All(_ => _.Graph.Relationships.Count == 0 && Relationships(_) == "[]").ShouldBeTrue();
    [Fact] void should_have_no_placement_or_relationship_diagnostics() => Orders.Append(Oracle).All(HasNoPlacementOrRelationshipDiagnostics).ShouldBeTrue();
}
