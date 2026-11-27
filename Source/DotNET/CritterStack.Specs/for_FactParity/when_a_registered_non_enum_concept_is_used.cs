// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Cratis.CritterStack.Screenplay.for_FactParity;

/// <summary>
/// Preserves a registered value type without enum yield behavior.
/// </summary>
public class when_a_registered_non_enum_concept_is_used : given.a_marten_concept_comparison
{
    void Establish() => Source = Application("public readonly record struct Status(int Value);", registration: "options.RegisterValueType<Status>();");

    void Because() => Compare();

    [Fact] void should_preserve_the_marten_only_roster() => IsEqual(MartenOnly).ShouldBeTrue();
    [Fact] void should_preserve_the_marten_then_vogen_roster() => IsEqualWithVogen(MartenThenVogen).ShouldBeTrue();
    [Fact] void should_preserve_the_vogen_then_marten_roster() => IsEqualWithVogen(VogenThenMarten).ShouldBeTrue();
    [Fact] void should_report_the_missing_status_once_with_only_marten() => AssertSingleMissingStatusDiagnostic(MartenOnly, Expected);
    [Fact] void should_report_the_missing_status_once_with_marten_then_vogen() => AssertSingleMissingStatusDiagnostic(MartenThenVogen, ExpectedWithVogen);
    [Fact] void should_report_the_missing_status_once_with_vogen_then_marten() => AssertSingleMissingStatusDiagnostic(VogenThenMarten, ExpectedWithVogen);
    [Fact] void should_match_all_marten_only_diagnostics() => AssertDiagnosticsMatch(MartenOnly, Expected);
    [Fact] void should_match_all_marten_then_vogen_diagnostics() => AssertDiagnosticsMatch(MartenThenVogen, ExpectedWithVogen);
    [Fact] void should_match_all_vogen_then_marten_diagnostics() => AssertDiagnosticsMatch(VogenThenMarten, ExpectedWithVogen);
    [Fact] void should_preserve_the_output_in_all_rosters() => (OutputMatches(MartenOnly, Expected) && OutputMatches(MartenThenVogen, ExpectedWithVogen) && OutputMatches(VogenThenMarten, ExpectedWithVogen)).ShouldBeTrue();
}
