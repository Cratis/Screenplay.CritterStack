// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Cratis.CritterStack.Screenplay.for_CritterStackScreenplayAdapter;

public class when_analyzing_unresolved_marten_tenancy_configuration : given.a_marten_tenancy_configuration_application
{
    [Fact] void should_report_each_unresolved_value_once() => UnresolvedDiagnostics.Count.ShouldEqual(6);
    [Fact] void should_classify_each_unresolved_value_as_unknown() => UnresolvedDiagnostics.All(_ => _.Outcome == GenerationDiagnosticOutcome.Unknown).ShouldBeTrue();
    [Fact] void should_not_admit_the_stale_separate_style() => TenancyDiagnostics.Any(_ => _.Message.Contains("Separate", StringComparison.Ordinal)).ShouldBeFalse();
    [Fact] void should_not_guess_computed_default_or_compound_values() => UnresolvedDiagnostics.Count(_ => _.Source?.Path == "Orders/Tenancy.cs").ShouldEqual(5);
    [Fact] void should_report_an_unresolved_generic_document_target() => UnresolvedDiagnostics.Any(_ => _.Message.Contains("otherwise unresolved generic document target", StringComparison.Ordinal)).ShouldBeTrue();
    [Fact] void should_not_guess_the_invalid_numeric_value() => UnresolvedDiagnostics.Any(_ => _.Message.Contains("99", StringComparison.Ordinal)).ShouldBeFalse();
    [Fact] void should_anchor_each_unresolved_occurrence() => UnresolvedDiagnostics.All(_ => _.Source.StartLine > 0 && _.Source.StartColumn > 0).ShouldBeTrue();
    [Fact] void should_keep_unresolved_event_tenancy_project_scoped() => UnresolvedDiagnostics.All(_ => _.Subject.Value.StartsWith("dotnet:project/", StringComparison.Ordinal)).ShouldBeTrue();

    [Fact] void should_preserve_full_diagnostic_source_ranges() => string.Join('\n', given.diagnostic_ranges.Of(Contribution)).ShouldEqual(string.Join('\n', ExpectedDiagnostics));

    const string Retained = " This authored declaration is retained as diagnostic evidence only; it does not originate, duplicate, or modify Screenplay artifacts or relationships, and runtime execution or precedence is not asserted";
    const string UnresolvedStyle = "Marten has an authored event tenancy-style declaration with a computed, invalid, stale, or otherwise unresolved value; no tenancy style or effective state was guessed." + Retained;
    const string UnresolvedTarget = "Marten has an authored document tenancy declaration 'MultiTenanted' with an otherwise unresolved generic document target; no document type or effective tenancy was guessed." + Retained;

    static string Style(string style) => $"Marten has an authored event tenancy-style declaration '{style}'; database topology, runtime tenant resolution, effective state, and projection consequences were not inferred.{Retained}";
    static string Document(string style, string document) => $"Marten has an authored document tenancy declaration '{style}' for '{document}'; partition callback behavior, precedence, effective state, tenant identities, and database topology were not inferred.{Retained}";
    static string Attribute(string attribute, string document) => $"Marten has an authored [{attribute}] document tenancy declaration for '{document}'; attribute evidence alone does not establish a Marten document, and precedence or effective state was not inferred.{Retained}";
    static string Policy(string policy) => $"Marten has an authored project-wide document tenancy policy declaration '{policy}'; the policy was not expanded to document types, and partition callback behavior, precedence, effective state, tenant identities, and database topology were not inferred.{Retained}";
    static string Persisted(string document) => $"Marten document '{document}' is persisted, queried, or explicitly configured, but the current Screenplay language has no ordinary document-state declaration";

    static string[] ExpectedDiagnostics =>
    [
        $"MARTEN0013|Warning|Unsupported|dotnet:project/LegacyOrders|LegacyOrders/Tenancy.cs:8:9-8:45|{Style("Single")}",
        $"MARTEN0013|Warning|Unsupported|dotnet:project/LegacyOrders|LegacyOrders/Tenancy.cs:9:9-9:76|{Style("Conjoined")}",
        $"MARTEN0013|Warning|Unknown|dotnet:project/LegacyOrders|LegacyOrders/Tenancy.cs:10:9-10:75|{UnresolvedStyle}",
        $"MARTEN0013|Warning|Unsupported|dotnet://Orders/Orders/Orders.AttributedMultiDocument|Orders/Tenancy.cs:8:2-8:29|{Attribute("MultiTenanted", "AttributedMultiDocument")}",
        $"MARTEN0013|Warning|Unsupported|dotnet://Orders/Orders/Orders.AttributedSingleDocument|Orders/Tenancy.cs:11:2-11:30|{Attribute("SingleTenanted", "AttributedSingleDocument")}",
        $"MARTEN0013|Warning|Unsupported|dotnet:project/Orders|Orders/Tenancy.cs:19:9-19:80|{Style("Single")}",
        $"MARTEN0013|Warning|Unsupported|dotnet:project/Orders|Orders/Tenancy.cs:20:9-20:48|{Style("Conjoined")}",
        $"MARTEN0013|Warning|Unsupported|dotnet:project/Orders|Orders/Tenancy.cs:21:9-21:83|{Style("Conjoined")}",
        $"MARTEN0013|Warning|Unknown|dotnet:project/Orders|Orders/Tenancy.cs:22:9-22:77|{UnresolvedStyle}",
        $"MARTEN0013|Warning|Unknown|dotnet:project/Orders|Orders/Tenancy.cs:23:9-23:46|{UnresolvedStyle}",
        $"MARTEN0013|Warning|Unknown|dotnet:project/Orders|Orders/Tenancy.cs:24:9-24:84|{UnresolvedStyle}",
        $"MARTEN0013|Warning|Unknown|dotnet:project/Orders|Orders/Tenancy.cs:26:9-26:47|{UnresolvedStyle}",
        $"MARTEN0013|Warning|Unsupported|dotnet://Orders/Orders/Orders.MultiDocument|Orders/Tenancy.cs:28:9-28:60|{Document("MultiTenanted", "MultiDocument")}",
        $"MARTEN0013|Warning|Unsupported|dotnet://Orders/Orders/Orders.MultiDocument|Orders/Tenancy.cs:29:9-29:60|{Document("MultiTenanted", "MultiDocument")}",
        $"MARTEN0013|Warning|Unsupported|dotnet://Orders/Orders/Orders.SingleDocument|Orders/Tenancy.cs:30:9-30:62|{Document("SingleTenanted", "SingleDocument")}",
        $"MARTEN0013|Warning|Unsupported|dotnet://Orders/Orders/Orders.ConflictingDocument|Orders/Tenancy.cs:31:9-31:66|{Document("MultiTenanted", "ConflictingDocument")}",
        $"MARTEN0013|Warning|Unsupported|dotnet://Orders/Orders/Orders.ConflictingDocument|Orders/Tenancy.cs:32:9-32:67|{Document("SingleTenanted", "ConflictingDocument")}",
        $"MARTEN0013|Warning|Unsupported|dotnet://Orders/Orders/Orders.PartitionedDocument|Orders/Tenancy.cs:33:9-34:51|{Document("MultiTenantedWithPartitioning", "PartitionedDocument")}",
        $"MARTEN0013|Warning|Unsupported|dotnet:project/Orders|Orders/Tenancy.cs:36:9-36:56|{Policy("AllDocumentsAreMultiTenanted")}",
        $"MARTEN0013|Warning|Unsupported|dotnet:project/Orders|Orders/Tenancy.cs:37:9-37:56|{Policy("AllDocumentsAreMultiTenanted")}",
        $"MARTEN0013|Warning|Unsupported|dotnet:project/Orders|Orders/Tenancy.cs:38:9-39:49|{Policy("AllDocumentsAreMultiTenantedWithPartitioning")}",
        $"MARTEN0013|Warning|Unsupported|dotnet:project/Orders|Orders/Tenancy.cs:40:9-42:70|{Policy("AllDocumentsAreMultiTenantedWithPartitioning")}",
        $"MARTEN0013|Warning|Unknown|dotnet:project/Orders|Orders/Tenancy.cs:46:9-46:48|{UnresolvedTarget}",
        $"MARTEN0003|Information|Unsupported|dotnet://Orders/Orders/Orders.ConflictingDocument|Orders/Tenancy.cs:31:9-31:50|{Persisted("ConflictingDocument")}",
        $"MARTEN0003|Information|Unsupported|dotnet://Orders/Orders/Orders.MultiDocument|Orders/Tenancy.cs:28:9-28:44|{Persisted("MultiDocument")}",
        $"MARTEN0003|Information|Unsupported|dotnet://Orders/Orders/Orders.PartitionedDocument|Orders/Tenancy.cs:33:9-33:50|{Persisted("PartitionedDocument")}",
        $"MARTEN0003|Information|Unsupported|dotnet://Orders/Orders/Orders.SingleDocument|Orders/Tenancy.cs:30:9-30:45|{Persisted("SingleDocument")}"
    ];

    IReadOnlyList<GenerationDiagnostic> TenancyDiagnostics => [.. Contribution.Diagnostics.Where(_ => _.Code == MartenDiagnosticCodes.TenancyConfigurationOmitted)];
    IReadOnlyList<GenerationDiagnostic> UnresolvedDiagnostics => [.. TenancyDiagnostics.Where(_ => _.Message.Contains("otherwise unresolved", StringComparison.Ordinal))];
}
