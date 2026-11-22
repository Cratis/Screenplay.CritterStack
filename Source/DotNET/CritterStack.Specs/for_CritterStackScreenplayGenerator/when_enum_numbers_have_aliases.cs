// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Cratis.CritterStack.Screenplay.for_CritterStackScreenplayGenerator;

public class when_enum_numbers_have_aliases : given.an_explicit_enum_numbers_application
{
    const string Declaration = "public enum Priority { First = 0, Second = 0, Third = 1 }";

    void Because() => Result = Generate(CreateProject(declaration: Declaration));

    [Fact] void should_succeed() => Result.IsSuccess.ShouldBeTrue();
    [Fact] void should_not_declare_a_concept() => Result.Source.ShouldNotContain("concept Priority");
    [Fact] void should_keep_the_plain_type_name_in_uses() => Result.Source.ShouldContain("priority Priority");
    [Fact] void should_report_the_unrepresentable_numbering() => NumberingDiagnostic.Code.ShouldEqual(CritterStackDiagnosticCodes.EnumNumbersNotPreserved);
    [Fact] void should_report_the_enum_as_unsupported() => NumberingDiagnostic.Outcome.ShouldEqual(GenerationDiagnosticOutcome.Unsupported);
    [Fact] void should_explain_why() => NumberingDiagnostic.Message.ShouldEqual("Enum 'Priority' has numbering that cannot be preserved: members share a number (aliases); it is not declared as a concept and its uses keep the plain type name without a concept reference");
    [Fact] void should_locate_the_enum() => NumberingDiagnostic.Source.ShouldEqual(DeclarationRange(Declaration));
}
