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

    [Fact] void should_succeed_for_the_empty_enum() => _empty.IsSuccess.ShouldBeTrue();
    [Fact] void should_succeed_for_colliding_values() => _colliding.IsSuccess.ShouldBeTrue();
    [Fact] void should_report_the_empty_enum() => _empty.Diagnostics.Single(_ => _.Code == CritterStackDiagnosticCodes.EnumValuesUnsupported).Source!.Path.ShouldEqual("Ordering/Priority.cs");
    [Fact] void should_report_values_colliding_after_screenplay_naming() => _colliding.Diagnostics.Single(_ => _.Code == CritterStackDiagnosticCodes.EnumValuesUnsupported).Source!.Path.ShouldEqual("Ordering/Priority.cs");
    [Fact] void should_not_leave_the_empty_enum_to_the_shared_lowerer() => _empty.Diagnostics.Any(_ => _.Code == GenerationDiagnosticCodes.UnsupportedConceptRepresentation).ShouldBeFalse();
    [Fact] void should_not_leave_colliding_values_to_the_shared_lowerer() => _colliding.Diagnostics.Any(_ => _.Code == GenerationDiagnosticCodes.UnsupportedConceptRepresentation).ShouldBeFalse();
    [Fact] void should_not_print_an_empty_concept() => _empty.Source.ShouldNotContain("concept Priority");
    [Fact] void should_not_print_colliding_values() => _colliding.Source.ShouldNotContain("concept Priority");
    [Fact] void should_keep_the_command_using_the_empty_enum() => _empty.Source.ShouldContain("command PlaceOrder");
    [Fact] void should_keep_the_order_read_by_the_command_using_the_empty_enum() => _empty.Source.ShouldContain("reads Order");
    [Fact] void should_keep_the_event_using_the_empty_enum() => _empty.Source.ShouldContain("event OrderPlaced");
    [Fact] void should_keep_the_command_using_colliding_values() => _colliding.Source.ShouldContain("command PlaceOrder");
    [Fact] void should_keep_the_order_read_by_the_command_using_colliding_values() => _colliding.Source.ShouldContain("reads Order");
    [Fact] void should_keep_the_event_using_colliding_values() => _colliding.Source.ShouldContain("event OrderPlaced");
    [Fact] void should_not_omit_artifacts_for_the_empty_enum() => _empty.Diagnostics.Any(_ => _.Code == "GEN0016").ShouldBeFalse();
    [Fact] void should_not_omit_artifacts_for_colliding_values() => _colliding.Diagnostics.Any(_ => _.Code == "GEN0016").ShouldBeFalse();
}
