// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Cratis.CritterStack.Screenplay.for_FactParity;

/// <summary>
/// Preserves enum concepts used through nullable member types.
/// </summary>
public class when_marten_enum_uses_are_nullable : given.a_marten_concept_comparison
{
    void Establish() => Source = Application("public enum Status { Draft, Placed }", "Status?");

    void Because() => Compare();

    [Fact] void should_preserve_the_marten_only_roster() => IsEqual(MartenOnly).ShouldBeTrue();
    [Fact] void should_preserve_the_marten_then_vogen_roster() => IsEqualWithVogen(MartenThenVogen).ShouldBeTrue();
    [Fact] void should_preserve_the_vogen_then_marten_roster() => IsEqualWithVogen(VogenThenMarten).ShouldBeTrue();
    [Fact] void should_declare_the_enum_concept() => MartenOnly.Source.ShouldContain("concept Status : Enum");
}
