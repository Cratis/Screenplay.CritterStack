// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Cratis.CritterStack.Screenplay.for_FactParity;

/// <summary>
/// Preserves enum yielding to an independent Vogen concept and to an unbound type of the same name.
/// </summary>
public class when_native_marten_concepts_compete_with_independent_concepts : given.a_marten_concept_comparison
{
    void Establish()
    {
        Generated = """
            namespace Other
            {
                [System.CodeDom.Compiler.GeneratedCode("Vogen", "8.0.7")]
                public partial struct Status
                {
                    public System.Guid Value { get; }
                }
            }
            """;
        Source = Application(
            "public enum Status { Draft, Placed }",
            extra: "namespace Other { [Vogen.ValueObject<System.Guid>] public partial struct Status; }",
            more: ", Other.Status Other");
    }

    void Because() => Compare();

    [Fact] void should_admit_vogen_in_the_public_marten_then_vogen_roster() => PublicMartenThenVogen.Diagnostics.ShouldNotContain(_ => _.Code == "DOTNETADAPTER005");
    [Fact] void should_admit_vogen_in_the_public_vogen_then_marten_roster() => PublicVogenThenMarten.Diagnostics.ShouldNotContain(_ => _.Code == "DOTNETADAPTER005");
    [Fact] void should_declare_the_independent_concept_in_the_public_marten_then_vogen_roster() => PublicMartenThenVogen.Source.ShouldContain("concept Status : Uuid");
    [Fact] void should_declare_the_independent_concept_in_the_public_vogen_then_marten_roster() => PublicVogenThenMarten.Source.ShouldContain("concept Status : Uuid");
    [Fact] void should_preserve_the_marten_then_vogen_roster() => IsEqualWithVogen(MartenThenVogen).ShouldBeTrue();
    [Fact] void should_preserve_the_vogen_then_marten_roster() => IsEqualWithVogen(VogenThenMarten).ShouldBeTrue();
    [Fact] void should_report_the_enum_yield_once() => Warnings(MartenThenVogen, CritterStackDiagnosticCodes.EnumConceptNameConflict).ShouldEqual(Warnings(Expected, CritterStackDiagnosticCodes.EnumConceptNameConflict));
}
