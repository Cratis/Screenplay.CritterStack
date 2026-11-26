// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Cratis.CritterStack.Screenplay.for_FactParity;

/// <summary>
/// Preserves enum omission when a same-named unbound type is printed by plain name.
/// </summary>
public class when_a_native_marten_enum_name_captures_an_unbound_type : given.a_marten_concept_comparison
{
    void Establish() => Source = Application("public enum Status { Draft, Placed }", extra: "namespace Other { public sealed record Status(int V); }", more: ", Other.Status Other");

    void Because() => Compare();

    [Fact] void should_preserve_the_marten_only_roster() => IsEqual(MartenOnly).ShouldBeTrue();
    [Fact] void should_preserve_the_marten_then_vogen_roster() => IsEqualWithVogen(MartenThenVogen).ShouldBeTrue();
    [Fact] void should_preserve_the_vogen_then_marten_roster() => IsEqualWithVogen(VogenThenMarten).ShouldBeTrue();
    [Fact] void should_report_the_capture_once() => Warnings(MartenOnly, CritterStackDiagnosticCodes.EnumConceptNameConflict).ShouldEqual(Warnings(Expected, CritterStackDiagnosticCodes.EnumConceptNameConflict));
}
