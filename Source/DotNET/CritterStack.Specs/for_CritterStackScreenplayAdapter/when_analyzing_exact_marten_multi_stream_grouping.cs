// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Cratis.CritterStack.Screenplay.for_CritterStackScreenplayAdapter;

public class when_analyzing_exact_marten_multi_stream_grouping : given.a_marten_multi_stream_grouping_application
{
    [Fact] void should_preserve_the_target_read_model() => Graph.Artifacts.Any(_ => _.Key.Kind == ArtifactKind.ReadModel && _.Variants.Single().Definition.Name == "CustomerOrders").ShouldBeTrue();
    [Fact] void should_preserve_the_reducer_build_relationship() => Relationships.Any(_ => _.Definition.Key.Kind == RelationshipKind.Builds && SourceIs(_, "CustomerOrdersProjection") && TargetIs(_, "CustomerOrders")).ShouldBeTrue();
    [Fact] void should_preserve_the_single_identity_source_member() => Identity("CustomerAssigned").Definition.TargetMember.ShouldEqual("customerId");
    [Fact] void should_mark_the_single_identity_as_one_to_one() => Identity("CustomerAssigned").Definition.IsCollection.ShouldBeFalse();
    [Fact] void should_preserve_the_one_to_many_identity_source_member() => Identity("CustomersShared").Definition.TargetMember.ShouldEqual("customerIds");
    [Fact] void should_mark_the_plural_identities_as_one_to_many() => Identity("CustomersShared").Definition.IsCollection.ShouldBeTrue();
    [Fact] void should_preserve_exact_identity_evidence() => Identity("CustomerAssigned").Evidence.Strength.ShouldEqual(EvidenceStrength.Exact);
    [Fact] void should_preserve_the_direct_fan_out_child() => FanOut("OrderImported", "LineImported").Definition.SourceMember.ShouldEqual("lines");
    [Fact] void should_preserve_the_event_wrapper_fan_out_child() => FanOut("RouteImported", "StopImported").Definition.SourceMember.ShouldEqual("stops");
    [Fact] void should_preserve_the_explicit_fan_out_mode() => FanOut("RouteImported", "StopImported").Definition.Key.Discriminator.ShouldContain("before-grouping");
    [Fact] void should_mark_fan_out_as_one_to_many() => FanOut("OrderImported", "LineImported").Definition.IsCollection.ShouldBeTrue();
    [Fact] void should_preserve_fan_out_parent_consumption() => Relationships.Any(_ => _.Definition.Key.Kind == RelationshipKind.Consumes && SourceIs(_, "CustomerOrdersProjection") && TargetIs(_, "OrderImported") && _.Definition.Key.Discriminator?.StartsWith("marten:fan-out-source:", StringComparison.Ordinal) == true).ShouldBeTrue();
    [Fact] void should_preserve_the_authored_event_consumes() => Relationships.Count(_ => _.Definition.Key.Kind == RelationshipKind.Consumes && SourceIs(_, "CustomerOrdersProjection")).ShouldBeGreaterThan(3);
    [Fact] void should_retain_the_screenplay_language_loss() => Contribution.Diagnostics.Any(_ => _.Code == MartenDiagnosticCodes.MultiStreamGroupingOmitted && _.Message.Contains("retained as neutral evidence", StringComparison.Ordinal)).ShouldBeTrue();
    [Fact] void should_not_create_conflicting_relationships() => Graph.Relationships.Any(_ => _.IsConflicted).ShouldBeFalse();

    IReadOnlyList<RelationshipFact> Relationships => [.. Contribution.Facts.OfType<RelationshipFact>()];

    RelationshipFact Identity(string eventName) => Relationships.Single(_ =>
        _.Definition.Key.Kind == RelationshipKind.Consumes &&
        SourceIs(_, "CustomerOrdersProjection") &&
        TargetIs(_, eventName) &&
        _.Definition.Key.Discriminator?.StartsWith("marten:identit", StringComparison.Ordinal) == true);

    RelationshipFact FanOut(string parentName, string childName) => Relationships.Single(_ =>
        _.Definition.Key.Kind == RelationshipKind.Consumes &&
        SourceIs(_, "CustomerOrdersProjection") &&
        TargetIs(_, childName) &&
        _.Definition.Key.Discriminator?.StartsWith("marten:fan-out-child:", StringComparison.Ordinal) == true &&
        _.Definition.Key.Discriminator.Contains($".{parentName}:", StringComparison.Ordinal));

    static bool SourceIs(RelationshipFact relationship, string name) =>
        relationship.Definition.Key.Source.Value.Contains($".{name}", StringComparison.Ordinal);

    static bool TargetIs(RelationshipFact relationship, string name) =>
        relationship.Definition.Key.Target.Value.EndsWith($".{name}", StringComparison.Ordinal);
}
