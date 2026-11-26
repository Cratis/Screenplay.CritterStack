// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Cratis.CritterStack.Screenplay.for_FactParity;

/// <summary>
/// Preserves the omission of an enum whose numbers cannot be preserved.
/// </summary>
public class when_marten_enums_have_aliased_numbers : given.a_marten_concept_comparison
{
    void Establish() => Source = Application("public enum Status { A = 1, B = 1 }");

    void Because() => Compare();

    [Fact] void should_preserve_the_marten_only_roster() => IsEqual(MartenOnly).ShouldBeTrue();
    [Fact] void should_preserve_the_marten_then_vogen_roster() => IsEqualWithVogen(MartenThenVogen).ShouldBeTrue();
    [Fact] void should_preserve_the_vogen_then_marten_roster() => IsEqualWithVogen(VogenThenMarten).ShouldBeTrue();
    [Fact] void should_report_the_numbering_omission() => Warnings(MartenOnly, CritterStackDiagnosticCodes.EnumNumbersNotPreserved).ShouldEqual(Warnings(Expected, CritterStackDiagnosticCodes.EnumNumbersNotPreserved));
}
