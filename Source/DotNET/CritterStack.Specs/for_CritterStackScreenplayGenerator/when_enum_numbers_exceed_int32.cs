// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Cratis.CritterStack.Screenplay.for_CritterStackScreenplayGenerator;

public class when_enum_numbers_exceed_int32 : given.an_explicit_enum_numbers_application
{
    const string Declaration = "public enum Priority : ulong { First = 0, Second = 1, Third = ulong.MaxValue }";

    void Because() => Result = Generate(CreateProject(declaration: Declaration));

    [Fact] void should_succeed_without_overflowing() => Result.IsSuccess.ShouldBeTrue();
    [Fact] void should_keep_the_names_only_output_unchanged() => Result.Source.ShouldEqual(PositionalSource);
    [Fact] void should_report_out_of_range_numbers() => NumberingDiagnostic.Code.ShouldEqual(CritterStackDiagnosticCodes.EnumNumbersNotPreserved);
    [Fact] void should_not_omit_the_enum() => Result.Diagnostics.Any(_ => _.Code == CritterStackDiagnosticCodes.EnumValuesUnsupported).ShouldBeFalse();
    [Fact] void should_locate_the_enum() => NumberingDiagnostic.Source.ShouldEqual(DeclarationRange(Declaration));
}
