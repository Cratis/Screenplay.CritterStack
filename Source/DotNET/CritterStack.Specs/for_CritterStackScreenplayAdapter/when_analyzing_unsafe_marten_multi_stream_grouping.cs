// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Cratis.CritterStack.Screenplay.for_CritterStackScreenplayAdapter;

public class when_analyzing_unsafe_marten_multi_stream_grouping : given.a_marten_multi_stream_grouping_application
{
    [Fact] void should_report_computed_and_block_selectors() => Losses.Count(_ => _.Message.Contains("not a simple member-selector lambda", StringComparison.Ordinal)).ShouldEqual(4);
    [Fact] void should_report_computed_fan_out() => Losses.Any(_ => _.Message.Contains("not an exact declaration with a simple member-selector lambda", StringComparison.Ordinal)).ShouldBeTrue();
    [Fact] void should_report_both_arbitrary_grouper_forms() => Losses.Count(_ => _.Message.Contains("arbitrary custom grouping", StringComparison.Ordinal)).ShouldEqual(2);
    [Fact] void should_report_tenancy_property_grouping() => Losses.Any(_ => _.Message.Contains("configures tenancy-dependent grouping", StringComparison.Ordinal)).ShouldBeTrue();
    [Fact] void should_report_tenant_rollup_grouping() => Losses.Any(_ => _.Message.Contains("groups by tenant through RollUpByTenant", StringComparison.Ordinal)).ShouldBeTrue();
    [Fact] void should_report_tenant_identity_grouping() => Losses.Any(_ => _.Message.Contains("depends on tenant identity", StringComparison.Ordinal)).ShouldBeTrue();
    [Fact] void should_report_conditionally_authored_grouping() => Losses.Any(_ => _.Message.Contains("conditional or nested", StringComparison.Ordinal)).ShouldBeTrue();
    [Fact] void should_not_fabricate_computed_identity_mappings() => GroupingRelationships.Any(_ => TargetIs(_, "ComputedIdentity")).ShouldBeFalse();
    [Fact] void should_not_fabricate_computed_plural_identity_mappings() => GroupingRelationships.Any(_ => TargetIs(_, "CustomersShared") && SourceIs(_, "UnsafeSelectorsProjection")).ShouldBeFalse();
    [Fact] void should_not_fabricate_computed_fan_out_mappings() => FanOutRelationships.Any(_ => ParentIs(_, "ComputedFanOut")).ShouldBeFalse();
    [Fact] void should_not_fabricate_tenant_identity_mappings() => GroupingRelationships.Any(_ => TargetIs(_, "TenantEvent")).ShouldBeFalse();
    [Fact] void should_not_fabricate_conditional_identity_mappings() => GroupingRelationships.Any(_ => TargetIs(_, "ConditionalIdentity") && SourceIs(_, "ConditionalGroupingProjection")).ShouldBeFalse();
    [Fact] void should_ignore_an_unrelated_identity_method() => GroupingRelationships.Any(_ => SourceIs(_, "UnrelatedIdentityProjection")).ShouldBeFalse();

    IReadOnlyList<GenerationDiagnostic> Losses => [.. Contribution.Diagnostics.Where(_ => _.Code == MartenDiagnosticCodes.MultiStreamGroupingOmitted)];
    IReadOnlyList<RelationshipFact> GroupingRelationships => [.. Contribution.Facts.OfType<RelationshipFact>().Where(_ => _.Definition.Key.Kind == RelationshipKind.Consumes && _.Definition.Key.Discriminator?.StartsWith("marten:identit", StringComparison.Ordinal) == true)];
    IReadOnlyList<RelationshipFact> FanOutRelationships => [.. Contribution.Facts.OfType<RelationshipFact>().Where(_ => _.Definition.Key.Kind == RelationshipKind.Consumes && _.Definition.Key.Discriminator?.StartsWith("marten:fan-out-child:", StringComparison.Ordinal) == true)];

    static bool ParentIs(RelationshipFact relationship, string name) =>
        relationship.Definition.Key.Discriminator?.Contains($".{name}:", StringComparison.Ordinal) == true;

    static bool SourceIs(RelationshipFact relationship, string name) =>
        relationship.Definition.Key.Source.Value.Contains($".{name}", StringComparison.Ordinal);

    static bool TargetIs(RelationshipFact relationship, string name) =>
        relationship.Definition.Key.Target.Value.EndsWith($".{name}", StringComparison.Ordinal);
}
