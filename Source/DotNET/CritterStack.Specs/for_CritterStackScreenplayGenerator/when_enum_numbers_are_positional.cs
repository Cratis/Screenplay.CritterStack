// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Cratis.CritterStack.Screenplay.for_CritterStackScreenplayGenerator;

public class when_enum_numbers_are_positional : given.an_explicit_enum_numbers_application
{
    GeneratedScreenplayDefinition _explicit = null!;

    void Because()
    {
        Result = Generate(CreateProject(declaration: PositionalDeclaration));
        _explicit = Generate(CreateProject(declaration: "public enum Priority : ulong { First = 0, Second = 1, Third = 2 }"));
    }

    [Fact] void should_succeed_for_implicit_numbers() => Result.IsSuccess.ShouldBeTrue();
    [Fact] void should_succeed_for_explicit_numbers() => _explicit.IsSuccess.ShouldBeTrue();
    [Fact] void should_not_report_implicit_positional_numbers() => Result.Diagnostics.Any(_ => _.Code == CritterStackDiagnosticCodes.EnumNumbersNotPreserved).ShouldBeFalse();
    [Fact] void should_not_report_explicit_positional_numbers() => _explicit.Diagnostics.Any(_ => _.Code == CritterStackDiagnosticCodes.EnumNumbersNotPreserved).ShouldBeFalse();
    [Fact] void should_keep_implicit_output_unchanged() => Result.Source.ShouldEqual(PositionalSource);
    [Fact] void should_keep_explicit_output_unchanged() => _explicit.Source.ShouldEqual(PositionalSource);
}
