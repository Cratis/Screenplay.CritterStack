// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Cratis.CritterStack.Screenplay.for_CritterStackScreenplayGenerator;

public class when_an_endpoint_forwards_and_returns_other_consequences : given.a_community_kitchen_application
{
    GeneratedScreenplayDefinition _result = null!;

    void Because() => _result = new CritterStackScreenplayGenerator().Generate(
        [CreateProject(
        [
            ("""
            namespace Kitchen.Planning;

            public record PlanBatch(System.Guid Id);
            public record KitchenNotification(System.Guid Id);
            public record KitchenAnnouncement(System.Guid Id);
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
                public static async System.Threading.Tasks.Task<(string, KitchenNotification, Wolverine.OutgoingMessages)> Post(
                    PlanBatch command, Wolverine.IMessageBus bus)
                {
                    await bus.InvokeAsync(command);
                    return ("accepted", new KitchenNotification(command.Id), [new KitchenAnnouncement(command.Id)]);
                }
            }
            """, "Kitchen/Planning/PlanBatchEndpoint.cs")
        ])],
        new CritterStackScreenplayOptions { Domain = "CommunityKitchen" });

    [Fact] void should_preserve_the_returned_message() => _result.Graph.Relationships.ShouldContain(_ =>
        _.Key.Kind == RelationshipKind.Cascades &&
        _.Key.Target.Value.EndsWith("/Kitchen.Planning.KitchenNotification", StringComparison.Ordinal));
    [Fact] void should_preserve_the_outgoing_message() => _result.Graph.Relationships.ShouldContain(_ =>
        _.Key.Kind == RelationshipKind.Cascades &&
        _.Key.Target.Value.EndsWith("/Kitchen.Planning.KitchenAnnouncement", StringComparison.Ordinal));
    [Fact] void should_not_classify_the_endpoint_as_a_forwarder() => _result.Diagnostics.ShouldNotContain(_ =>
        _.Message.Contains("forwards 'PlanBatch' to its Wolverine handler", StringComparison.Ordinal));
}
