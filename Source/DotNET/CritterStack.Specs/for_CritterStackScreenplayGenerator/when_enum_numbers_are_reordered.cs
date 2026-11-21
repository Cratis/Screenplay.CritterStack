// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Cratis.CritterStack.Screenplay.for_CritterStackScreenplayGenerator;

public class when_enum_numbers_are_reordered : given.an_explicit_enum_numbers_application
{
    const string Declaration = "public enum Priority { First = 2, Second = 0, Third = 1 }";

    void Because() => Result = Generate(CreateProject(declaration: Declaration));

    [Fact] void should_succeed() => Result.IsSuccess.ShouldBeTrue();
    [Fact] void should_keep_the_names_in_declaration_order() => Result.Source.ShouldEqual(PositionalSource);
    [Fact] void should_report_non_positional_numbers() => NumberingDiagnostic.Code.ShouldEqual(CritterStackDiagnosticCodes.EnumNumbersNotPreserved);
    [Fact] void should_locate_the_enum() => NumberingDiagnostic.Source.ShouldEqual(DeclarationRange(Declaration));
}
