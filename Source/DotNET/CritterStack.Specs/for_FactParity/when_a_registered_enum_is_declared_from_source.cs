// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Cratis.CritterStack.Screenplay.for_FactParity;

/// <summary>
/// Preserves a registered enum as one configured concept with an exact representation.
/// </summary>
public class when_a_registered_enum_is_declared_from_source : given.a_marten_concept_comparison
{
    void Establish() => Source = Application("public enum Status { Draft, Placed }", registration: "options.RegisterValueType<Status>();");

    void Because() => Compare();

    [Fact] void should_preserve_the_marten_only_roster() => IsEqual(MartenOnly).ShouldBeTrue();
    [Fact] void should_preserve_the_marten_then_vogen_roster() => IsEqualWithVogen(MartenThenVogen).ShouldBeTrue();
    [Fact] void should_preserve_the_vogen_then_marten_roster() => IsEqualWithVogen(VogenThenMarten).ShouldBeTrue();
    [Fact] void should_declare_one_concept() => MartenOnly.Source.Split("concept Status").Length.ShouldEqual(2);
}
