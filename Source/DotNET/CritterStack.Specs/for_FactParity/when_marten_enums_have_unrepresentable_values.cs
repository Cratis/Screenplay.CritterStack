// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Cratis.CritterStack.Screenplay.for_FactParity;

/// <summary>
/// Preserves omissions for empty and non-identifier enum values and aliased numbers.
/// </summary>
public class when_marten_enums_have_unrepresentable_values : given.a_marten_concept_comparison
{
    void Establish() => Source = Application("public enum Status { }");

    void Because() => Compare();

    [Fact] void should_preserve_the_marten_only_roster() => IsEqual(MartenOnly).ShouldBeTrue();
    [Fact] void should_preserve_the_marten_then_vogen_roster() => IsEqualWithVogen(MartenThenVogen).ShouldBeTrue();
    [Fact] void should_preserve_the_vogen_then_marten_roster() => IsEqualWithVogen(VogenThenMarten).ShouldBeTrue();
    [Fact] void should_report_the_unsupported_values() => Warnings(MartenOnly, CritterStackDiagnosticCodes.EnumValuesUnsupported).ShouldEqual(Warnings(Expected, CritterStackDiagnosticCodes.EnumValuesUnsupported));
}
