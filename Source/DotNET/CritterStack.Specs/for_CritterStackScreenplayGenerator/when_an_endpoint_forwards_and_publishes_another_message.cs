// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Cratis.CritterStack.Screenplay.for_CritterStackScreenplayGenerator;

public class when_an_endpoint_forwards_and_publishes_another_message : given.a_community_kitchen_application
{
    GeneratedScreenplayDefinition _result = null!;

    void Because() => _result = new CritterStackScreenplayGenerator().Generate(
        [CreateProject(
        [
            ("""
            namespace Kitchen.Planning;

            public record PlanBatch(System.Guid Id);
            public record KitchenNotification(System.Guid Id);
            public static class PlanBatchHandler
            {
                public static void Handle(PlanBatch command) { }
            }
            """, "Kitchen/Planning/PlanBatch.cs"),
            ("""
            namespace Kitchen.Planning;

            public static class PlanBatchEndpoint
            {
                [Wolverine.Http.WolverinePost("/batches")]
                public static async System.Threading.Tasks.Task Post(PlanBatch command, Wolverine.IMessageBus bus)
                {
                    await bus.InvokeAsync(command);
                    await bus.PublishAsync(new KitchenNotification(command.Id));
                }
            }
            """, "Kitchen/Planning/PlanBatchEndpoint.cs")
        ])],
        new CritterStackScreenplayOptions { Domain = "CommunityKitchen" });

    [Fact] void should_preserve_the_additional_message_relationship() => _result.Graph.Relationships.ShouldContain(_ =>
        _.Key.Kind == RelationshipKind.Publishes &&
        _.Key.Source == ArtifactNamed(_result, ArtifactKind.Command, "PlanBatch").Key.Subject &&
        _.Key.Target.Value.EndsWith("/Kitchen.Planning.KitchenNotification", StringComparison.Ordinal));
    [Fact] void should_report_the_additional_delivery_loss() => _result.Diagnostics.ShouldContain(_ =>
        _.Code == WolverineDiagnosticCodes.DirectMessageDeliveryOmitted &&
        _.Message.Contains("publish delivery of 'KitchenNotification'", StringComparison.Ordinal) &&
        _.Source!.Path == "Application/Kitchen/Planning/PlanBatchEndpoint.cs");
    [Fact] void should_not_classify_the_endpoint_as_a_forwarder() => _result.Diagnostics.ShouldNotContain(_ =>
        _.Message.Contains("forwards 'PlanBatch' to its Wolverine handler", StringComparison.Ordinal));
}
