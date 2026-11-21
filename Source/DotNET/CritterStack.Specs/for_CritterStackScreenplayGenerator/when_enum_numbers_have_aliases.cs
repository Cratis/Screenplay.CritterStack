// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Cratis.CritterStack.Screenplay.for_CritterStackScreenplayGenerator;

public class when_enum_numbers_have_aliases : given.an_explicit_enum_numbers_application
{
    const string Declaration = "public enum Priority { First = 0, Second = 0, Third = 1 }";

    void Because() => Result = Generate(CreateProject(declaration: Declaration));

    [Fact] void should_succeed() => Result.IsSuccess.ShouldBeTrue();
    [Fact] void should_retain_all_alias_names() => Result.Source.ShouldEqual(PositionalSource);
    [Fact] void should_report_duplicate_numbers() => NumberingDiagnostic.Code.ShouldEqual(CritterStackDiagnosticCodes.EnumNumbersNotPreserved);
    [Fact] void should_not_omit_aliases() => Result.Diagnostics.Any(_ => _.Code == CritterStackDiagnosticCodes.EnumValuesUnsupported).ShouldBeFalse();
    [Fact] void should_locate_the_enum() => NumberingDiagnostic.Source.ShouldEqual(DeclarationRange(Declaration));
}
