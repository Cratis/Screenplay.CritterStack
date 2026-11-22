// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Cratis.CritterStack.Screenplay.for_CritterStackScreenplayGenerator;

public class when_enum_numbers_are_sparse : given.an_explicit_enum_numbers_application
{
    const string Declaration = "public enum Priority { First = 0, Second = 10, Third = 20 }";

    void Because() => Result = Generate(CreateProject(declaration: Declaration));

    [Fact] void should_succeed() => Result.IsSuccess.ShouldBeTrue();
    [Fact] void should_compile_without_undeclared_field_types() => new Cratis.Screenplay.ScreenplayCompiler().Compile(Result.Source).Diagnostics.Any(_ => _.Code == "PLAY0165").ShouldBeFalse();
    [Fact] void should_emit_the_numbers_in_declaration_order() => Result.Source.ShouldEqual(WithNumbers(0, 10, 20));
    [Fact] void should_not_report_a_numbering_warning() => Result.Diagnostics.Any(_ => _.Code == CritterStackDiagnosticCodes.EnumNumbersNotPreserved).ShouldBeFalse();
}
