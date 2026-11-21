// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Cratis.CritterStack.Screenplay.for_CritterStackScreenplayGenerator;

public class when_printing_failure_stage_printed_model : given.an_on_exception_handler_application
{
    GeneratedScreenplayDefinition _result = null!;

    void Because() => _result = new CritterStackScreenplayGenerator().Generate(
        [Project], new CritterStackScreenplayOptions { Domain = "FailureStages" });

    [Fact] void should_generate_a_compiler_verified_candidate() => _result.IsSuccess.ShouldBeTrue();
    [Fact] void should_keep_the_success_consequence() => _result.Source.ShouldContain("OrderShipped");
    [Fact] void should_not_print_outgoing_failure_messages_as_success_consequences() => _result.Source.ShouldNotContain("ShipmentFailed");
    [Fact] void should_not_print_returned_failure_messages_as_success_consequences() => _result.Source.ShouldNotContain("ShipmentTimedOut");
    [Fact] void should_not_print_async_failure_messages_as_success_consequences() => _result.Source.ShouldNotContain("ShipmentRejected");
    [Fact] void should_omit_all_failure_cascades_from_the_graph() => Contribution.Facts.OfType<RelationshipFact>().Where(_ => _.Definition.Key.Kind == RelationshipKind.Cascades).Select(_ => _.Definition.Key.Discriminator).ShouldContainOnly("immediate");
    [Fact] void should_retain_one_located_loss_for_each_failure_hook() => _result.Diagnostics.Count(_ => _.Code == WolverineDiagnosticCodes.FailureStageConditionOmitted && _.Source.Path == "OnExceptionHandlers/Handlers.cs").ShouldEqual(3);
}
