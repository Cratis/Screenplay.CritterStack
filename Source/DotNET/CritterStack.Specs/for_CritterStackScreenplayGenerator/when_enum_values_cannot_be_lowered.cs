// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Cratis.CritterStack.Screenplay.for_CritterStackScreenplayGenerator;

public class when_enum_values_cannot_be_lowered : given.an_enum_application
{
    GeneratedScreenplayDefinition _empty = null!;
    GeneratedScreenplayDefinition _colliding = null!;

    void Because()
    {
        _empty = Generate(CreateProject(declaration: "public enum Priority { }"));
        _colliding = Generate(CreateProject(declaration: "public enum Priority { High, high }"));
    }

    [Fact] void should_report_the_empty_enum_through_the_shared_lowerer() => _empty.Diagnostics.Any(_ => _.Code == GenerationDiagnosticCodes.UnsupportedConceptRepresentation).ShouldBeTrue();
    [Fact] void should_report_values_colliding_after_screenplay_naming() => _colliding.Diagnostics.Any(_ => _.Code == GenerationDiagnosticCodes.UnsupportedConceptRepresentation).ShouldBeTrue();
    [Fact] void should_not_print_an_empty_concept() => _empty.Source.ShouldNotContain("concept Priority");
    [Fact] void should_not_print_colliding_values() => _colliding.Source.ShouldNotContain("concept Priority");
}
