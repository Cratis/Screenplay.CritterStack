// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Cratis.CritterStack.Screenplay.for_CritterStackScreenplayAdapter;

public class when_analyzing_unresolved_marten_tenancy_configuration : given.a_marten_tenancy_configuration_application
{
    [Fact] void should_report_each_unresolved_value_once() => UnresolvedDiagnostics.Count.ShouldEqual(3);
    [Fact] void should_not_admit_the_stale_separate_style() => TenancyDiagnostics.Any(_ => _.Message.Contains("Separate", StringComparison.Ordinal)).ShouldBeFalse();
    [Fact] void should_not_guess_the_computed_conjoined_value() => UnresolvedDiagnostics.Count(_ => _.Source?.Path == "Orders/Tenancy.cs").ShouldEqual(2);
    [Fact] void should_not_guess_the_invalid_numeric_value() => UnresolvedDiagnostics.Any(_ => _.Message.Contains("99", StringComparison.Ordinal)).ShouldBeFalse();
    [Fact] void should_anchor_each_unresolved_occurrence() => UnresolvedDiagnostics.All(_ => _.Source!.StartLine > 0 && _.Source.StartColumn > 0).ShouldBeTrue();
    [Fact] void should_keep_unresolved_event_tenancy_project_scoped() => UnresolvedDiagnostics.All(_ => _.Subject.Value.StartsWith("dotnet:project/", StringComparison.Ordinal)).ShouldBeTrue();

    IReadOnlyList<GenerationDiagnostic> TenancyDiagnostics => [.. Contribution.Diagnostics.Where(_ => _.Code == MartenDiagnosticCodes.TenancyConfigurationOmitted)];
    IReadOnlyList<GenerationDiagnostic> UnresolvedDiagnostics => [.. TenancyDiagnostics.Where(_ => _.Message.Contains("otherwise unresolved", StringComparison.Ordinal))];
}
