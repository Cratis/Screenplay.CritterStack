// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Cratis.CritterStack.Screenplay.for_CritterStackScreenplayAdapter;

public class when_analyzing_exact_marten_tenancy_configuration : given.a_marten_tenancy_configuration_application
{
    static readonly HashSet<string> _attributedOnlyTypes = ["AttributedMultiDocument", "AttributedSingleDocument"];

    [Fact] void should_preserve_every_exact_and_fail_closed_tenancy_occurrence() => TenancyDiagnostics.Count.ShouldEqual(20);
    [Fact] void should_retain_current_single_event_tenancy() => CurrentEventTenancy.Any(_ => _.Message.Contains("declaration 'Single'", StringComparison.Ordinal)).ShouldBeTrue();
    [Fact] void should_retain_current_conjoined_event_tenancy_constants() => CurrentEventTenancy.Count(_ => _.Message.Contains("declaration 'Conjoined'", StringComparison.Ordinal)).ShouldEqual(2);
    [Fact] void should_retain_legacy_single_event_tenancy_metadata() => LegacyEventTenancy.Any(_ => _.Message.Contains("declaration 'Single'", StringComparison.Ordinal)).ShouldBeTrue();
    [Fact] void should_retain_legacy_conjoined_event_tenancy_metadata() => LegacyEventTenancy.Any(_ => _.Message.Contains("declaration 'Conjoined'", StringComparison.Ordinal)).ShouldBeTrue();
    [Fact] void should_preserve_repeated_document_declarations() => TenancyDiagnostics.Count(_ => _.Message.Contains("'MultiTenanted' for 'MultiDocument'", StringComparison.Ordinal)).ShouldEqual(2);
    [Fact] void should_preserve_conflicting_document_declarations() => TenancyDiagnostics.Count(_ => _.Message.Contains("for 'ConflictingDocument'", StringComparison.Ordinal)).ShouldEqual(2);
    [Fact] void should_retain_single_tenanted_document_declarations() => TenancyDiagnostics.Any(_ => _.Message.Contains("'SingleTenanted' for 'SingleDocument'", StringComparison.Ordinal)).ShouldBeTrue();
    [Fact] void should_retain_partitioned_document_tenancy_without_interpreting_the_callback() => TenancyDiagnostics.Any(_ => _.Message.Contains("'MultiTenantedWithPartitioning' for 'PartitionedDocument'", StringComparison.Ordinal) && _.Message.Contains("partition callback behavior", StringComparison.Ordinal)).ShouldBeTrue();
    [Fact] void should_retain_exact_multi_tenanted_attributes() => TenancyDiagnostics.Any(_ => _.Message.Contains("[MultiTenanted]", StringComparison.Ordinal) && _.Message.Contains("AttributedMultiDocument", StringComparison.Ordinal)).ShouldBeTrue();
    [Fact] void should_retain_exact_single_tenanted_attributes() => TenancyDiagnostics.Any(_ => _.Message.Contains("[SingleTenanted]", StringComparison.Ordinal) && _.Message.Contains("AttributedSingleDocument", StringComparison.Ordinal)).ShouldBeTrue();
    [Fact] void should_keep_attribute_evidence_document_scoped() => AttributedDiagnostics.All(_ => _.Subject.Value.StartsWith("dotnet://Orders/", StringComparison.Ordinal)).ShouldBeTrue();
    [Fact] void should_retain_the_global_multi_tenancy_policy_occurrences() => PolicyDiagnostics.Count(_ => _.Message.Contains("'AllDocumentsAreMultiTenanted'", StringComparison.Ordinal)).ShouldEqual(2);
    [Fact] void should_retain_both_partitioning_policy_overloads() => PolicyDiagnostics.Count(_ => _.Message.Contains("'AllDocumentsAreMultiTenantedWithPartitioning'", StringComparison.Ordinal)).ShouldEqual(2);
    [Fact] void should_keep_policy_evidence_project_scoped() => PolicyDiagnostics.All(_ => _.Subject.Value == "dotnet:project/Orders").ShouldBeTrue();
    [Fact] void should_anchor_every_declaration_at_its_authored_occurrence() => TenancyDiagnostics.All(_ => _.Source!.StartLine > 0 && _.Source.StartColumn > 0).ShouldBeTrue();
    [Fact] void should_preserve_each_authored_occurrence_without_collapsing_locations() => TenancyDiagnostics.Select(_ => (_.Source!.Path, _.Source.StartLine, _.Source.StartColumn)).Distinct().Count().ShouldEqual(TenancyDiagnostics.Count);
    [Fact] void should_only_originate_documents_from_independent_schema_for_registrations() => DocumentNames.Count.ShouldEqual(4);
    [Fact] void should_originate_each_independently_registered_document() => DocumentNames.ToHashSet().SetEquals(["ConflictingDocument", "MultiDocument", "PartitionedDocument", "SingleDocument"]).ShouldBeTrue();
    [Fact] void should_not_originate_attribute_only_documents() => DocumentNames.Intersect(_attributedOnlyTypes).ShouldBeEmpty();
    [Fact] void should_not_originate_events_from_tenancy_configuration() => Graph.Artifacts.Where(_ => _.Key.Kind == ArtifactKind.Event).ShouldBeEmpty();
    [Fact] void should_not_duplicate_tenant_specific_artifacts() => Graph.Artifacts.Select(_ => _.Key).Distinct().Count().ShouldEqual(Graph.Artifacts.Count);
    [Fact] void should_not_emit_tenancy_relationships() => Contribution.Facts.OfType<RelationshipFact>().ShouldBeEmpty();

    IReadOnlyList<GenerationDiagnostic> TenancyDiagnostics => [.. Contribution.Diagnostics.Where(_ => _.Code == MartenDiagnosticCodes.TenancyConfigurationOmitted)];
    IReadOnlyList<GenerationDiagnostic> CurrentEventTenancy => [.. TenancyDiagnostics.Where(_ => _.Source?.Path == "Orders/Tenancy.cs" && _.Message.Contains("event tenancy-style", StringComparison.Ordinal))];
    IReadOnlyList<GenerationDiagnostic> LegacyEventTenancy => [.. TenancyDiagnostics.Where(_ => _.Source?.Path == "LegacyOrders/Tenancy.cs")];
    IReadOnlyList<GenerationDiagnostic> AttributedDiagnostics => [.. TenancyDiagnostics.Where(_ => _attributedOnlyTypes.Any(type => _.Message.Contains(type, StringComparison.Ordinal)))];
    IReadOnlyList<GenerationDiagnostic> PolicyDiagnostics => [.. TenancyDiagnostics.Where(_ => _.Message.Contains("policy declaration", StringComparison.Ordinal))];
    IReadOnlyList<string> DocumentNames =>
    [
        .. Graph.Artifacts
            .Where(_ => _.Key.Kind == ArtifactKind.Document)
            .SelectMany(_ => _.Variants)
            .Select(_ => _.Definition.Name)
    ];
}
