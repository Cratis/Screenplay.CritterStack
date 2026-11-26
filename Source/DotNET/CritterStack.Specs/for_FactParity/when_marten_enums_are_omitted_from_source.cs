// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Cratis.CritterStack.Screenplay.for_FactParity;

/// <summary>
/// Preserves the omission of flags enums when Marten emits concepts from source.
/// </summary>
public class when_marten_enums_are_omitted_from_source : given.a_marten_concept_comparison
{
    void Establish() => Source = Application("[System.Flags] public enum Status { A = 1, B = 2 }");

    void Because() => Compare();

    [Fact] void should_preserve_the_marten_only_roster() => IsEqual(MartenOnly).ShouldBeTrue();
    [Fact] void should_preserve_the_marten_then_vogen_roster() => IsEqualWithVogen(MartenThenVogen).ShouldBeTrue();
    [Fact] void should_preserve_the_vogen_then_marten_roster() => IsEqualWithVogen(VogenThenMarten).ShouldBeTrue();
    [Fact] void should_report_the_omission_once() => Warnings(MartenOnly, CritterStackDiagnosticCodes.FlagsEnumOmitted).ShouldEqual(Warnings(Expected, CritterStackDiagnosticCodes.FlagsEnumOmitted));
    [Fact] void should_not_invent_a_concept() => MartenOnly.Source.ShouldNotContain("concept Status");
}
