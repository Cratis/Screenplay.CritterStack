// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Cratis.CritterStack.Screenplay.for_CritterStackScreenplayGenerator;

public class when_generating_wolverine_aggregate_handler_yields : given.a_community_kitchen_application
{
    GeneratedScreenplayDefinition _result = null!;

    void Because() => _result = new CritterStackScreenplayGenerator().Generate(
        [CreateFeatureFolderProject()],
        new CritterStackScreenplayOptions { Domain = "CommunityKitchen" });

    [Fact] void should_succeed() => _result.IsSuccess.ShouldBeTrue();
    [Fact] void should_link_an_unconditional_yield_declaratively() => Production(_result, "CloseBatch", "BatchClosed")!.Key.Discriminator.ShouldEqual("declarative");
    [Fact] void should_record_the_aggregate_stream_identity() => Production(_result, "CloseBatch", "BatchClosed")!.Definitions.Single().SourceMember.ShouldEqual("id");
    [Fact] void should_print_the_unconditional_production() => _result.Source.ShouldContain("produces BatchClosed");
    [Fact] void should_keep_a_conditional_yield_as_a_handler_reference() => Production(_result, "ServeBatch", "BatchServed")!.Key.Discriminator.ShouldEqual("imperative");
    [Fact] void should_report_the_conditional_yield() => _result.Diagnostics.ShouldContain(_ =>
        _.Code == WolverineDiagnosticCodes.ProductionLinkOmitted &&
        _.Message.Contains("Command 'ServeBatch' is not linked to event 'BatchServed'", StringComparison.Ordinal) &&
        _.Message.Contains("the handler yields it only on some paths", StringComparison.Ordinal));
    [Fact] void should_resolve_the_aggregate_handler_to_its_handler_file() =>
        ArtifactNamed(_result, ArtifactKind.Command, "ServeBatch").Variants.Single().Files.ShouldContainOnly("Application/Kitchen/Serving/ServeBatch.cs");
    [Fact] void should_not_resolve_the_aggregate_handler_to_the_http_endpoint() => _result.Source.ShouldNotContain("file Application/Kitchen/Serving/ServeBatchEndpoint.cs");
    [Fact] void should_resolve_the_session_handler_to_its_handler_file() =>
        ArtifactNamed(_result, ArtifactKind.Command, "PlanBatch").Variants.Single().Files.ShouldContainOnly("Application/Kitchen/Planning/PlanBatch.cs");
    [Fact] void should_place_the_conditionally_yielded_event_in_a_state_change_slice() => PlacementOf(_result, ArtifactKind.Event, "BatchServed").ShouldEqual(PlacementOf(_result, ArtifactKind.Command, "ServeBatch"));
    [Fact] void should_not_place_the_produced_event_in_a_state_view_slice() => PlacementOf(_result, ArtifactKind.Event, "BatchServed").ShouldNotContain("StateView");
    [Fact] void should_report_that_the_forwarding_route_is_not_represented() => _result.Diagnostics.ShouldContain(_ =>
        _.Code == WolverineDiagnosticCodes.HttpMetadataOmitted &&
        _.Message.Contains("forwards 'ServeBatch' to its Wolverine handler", StringComparison.Ordinal));
}
