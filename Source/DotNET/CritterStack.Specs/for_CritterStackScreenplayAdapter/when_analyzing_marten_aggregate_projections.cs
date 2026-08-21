// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Cratis.CritterStack.Screenplay.for_CritterStackScreenplayAdapter;

public class when_analyzing_marten_aggregate_projections : given.a_critter_stack_application
{
    ResolvedApplicationGraph _graph = null!;

    void Because() => _graph = new GenerationResolver().Resolve([Contribution]);

    [Fact] void should_recognize_the_application() => Adapter.CanAnalyze(Context).ShouldBeTrue();
    [Fact] void should_discover_all_events() => Artifacts(ArtifactKind.Event).Select(_ => _.Variants.Single().Definition.Name).ShouldContainOnly("AccountOpened", "FundsDeposited", "FundsWithdrawn");
    [Fact] void should_discover_both_read_models() => Artifacts(ArtifactKind.ReadModel).Select(_ => _.Variants.Single().Definition.Name).ShouldContainOnly("Account", "AccountTransactions");
    [Fact] void should_discover_both_reducers() => Artifacts(ArtifactKind.Reducer).Select(_ => _.Variants.Single().Definition.Name).ShouldContainOnly("AccountSnapshot", "AccountTransactionsProjection");
    [Fact] void should_link_each_reducer_to_its_read_model() => Relationships(RelationshipKind.Builds).Count.ShouldEqual(2);
    [Fact] void should_link_the_consumed_events() => Relationships(RelationshipKind.Consumes).Count.ShouldEqual(6);
    [Fact] void should_not_report_loss_for_supported_aggregate_projections() => Contribution.Diagnostics.ShouldBeEmpty();
    [Fact] void should_resolve_without_conflicts() => _graph.Diagnostics.ShouldBeEmpty();

    IReadOnlyList<ResolvedArtifact> Artifacts(ArtifactKind kind) =>
        [.. _graph.Artifacts.Where(_ => _.Key.Kind == kind)];

    IReadOnlyList<ResolvedRelationship> Relationships(RelationshipKind kind) =>
        [.. _graph.Relationships.Where(_ => _.Key.Kind == kind)];
}
