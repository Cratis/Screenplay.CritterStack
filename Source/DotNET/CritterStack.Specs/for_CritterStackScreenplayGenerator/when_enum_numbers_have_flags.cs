// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Cratis.CritterStack.Screenplay.for_CritterStackScreenplayGenerator;

public class when_enum_numbers_have_flags : given.an_explicit_enum_numbers_application
{
    const string Declaration = "[System.Flags] public enum Priority { First = 1, Second = 2, Third = 4 }";

    void Because() => Result = Generate(CreateProject(declaration: Declaration));

    [Fact] void should_succeed() => Result.IsSuccess.ShouldBeTrue();
    [Fact] void should_keep_omitting_the_flags_concept() => Result.Source.ShouldNotContain("concept Priority");
    [Fact] void should_keep_the_existing_flags_warning() => Result.Diagnostics.Count(_ => _.Code == CritterStackDiagnosticCodes.FlagsEnumOmitted).ShouldEqual(1);
    [Fact] void should_not_add_a_numbering_warning_for_an_omitted_concept() => Result.Diagnostics.Any(_ => _.Code == CritterStackDiagnosticCodes.EnumNumbersNotPreserved).ShouldBeFalse();
    [Fact] void should_keep_locating_the_flags_enum() => Result.Diagnostics.Single(_ => _.Code == CritterStackDiagnosticCodes.FlagsEnumOmitted).Source.ShouldEqual(DeclarationRange(Declaration));
}
