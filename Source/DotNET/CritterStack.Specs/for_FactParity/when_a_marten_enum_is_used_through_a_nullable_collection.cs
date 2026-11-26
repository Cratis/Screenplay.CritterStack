// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Cratis.CritterStack.Screenplay.for_FactParity;

/// <summary>
/// Preserves the oracle's concept rule: an enum used only through a collection of optionals is not declared as a concept.
/// </summary>
public class when_a_marten_enum_is_used_through_a_nullable_collection : given.a_marten_concept_comparison
{
    void Establish() => Source = Application("public enum Status { Draft, Placed }", "System.Collections.Generic.List<Status?>");

    void Because() => Compare();

    [Fact] void should_not_declare_the_enum_concept_in_the_oracle() => Expected.Source.ShouldNotContain("concept Status");
    [Fact] void should_not_declare_the_enum_concept_natively() => MartenOnly.Source.ShouldNotContain("concept Status");
}
