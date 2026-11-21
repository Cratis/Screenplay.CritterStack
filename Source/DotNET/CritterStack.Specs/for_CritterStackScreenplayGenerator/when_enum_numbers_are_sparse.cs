// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Cratis.CritterStack.Screenplay.for_CritterStackScreenplayGenerator;

public class when_enum_numbers_are_sparse : given.an_explicit_enum_numbers_application
{
    const string Declaration = "public enum Priority { First = 0, Second = 10, Third = 20 }";

    void Because() => Result = Generate(CreateProject(declaration: Declaration));

    [Fact] void should_succeed() => Result.IsSuccess.ShouldBeTrue();
    [Fact] void should_keep_the_names_only_output_unchanged() => Result.Source.ShouldEqual(PositionalSource);
    [Fact] void should_report_one_numbering_warning() => Result.Diagnostics.Count(_ => _.Code == CritterStackDiagnosticCodes.EnumNumbersNotPreserved).ShouldEqual(1);
    [Fact] void should_report_an_unsupported_warning() => NumberingDiagnostic.Severity.ShouldEqual(GenerationDiagnosticSeverity.Warning);
    [Fact] void should_report_the_loss_of_numeric_fidelity() => NumberingDiagnostic.Outcome.ShouldEqual(GenerationDiagnosticOutcome.Unsupported);
    [Fact] void should_locate_the_full_enum_declaration() => NumberingDiagnostic.Source.ShouldEqual(DeclarationRange(Declaration));
    [Fact] void should_explain_what_is_not_preserved() => NumberingDiagnostic.Message.ShouldEqual("Enum 'Priority': explicit member numbers are not preserved in the generated concept");
}
