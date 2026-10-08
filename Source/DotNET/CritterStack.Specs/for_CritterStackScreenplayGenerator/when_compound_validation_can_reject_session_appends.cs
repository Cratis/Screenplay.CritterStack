// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Cratis.CritterStack.Screenplay.for_CritterStackScreenplayGenerator;

public class when_compound_validation_can_reject_session_appends : given.a_community_kitchen_application
{
    GeneratedScreenplayDefinition _result = null!;

    void Because() => _result = new CritterStackScreenplayGenerator().Generate(
        [CreateProject(
        [
            ("""
            namespace Kitchen.Preparation;

            public record PreparePortions(System.Guid Id, int Portions);
            public record PortionsPrepared(int Portions);
            public static class PreparePortionsEndpoint
            {
                public static string[] Validate(PreparePortions command) => command.Portions > 0 ? [] : ["Portions must be positive"];

                [Wolverine.Http.WolverinePost("/batches/prepare")]
                public static void Post(PreparePortions command, Marten.IDocumentSession session) =>
                    session.Events.Append(command.Id, new PortionsPrepared(command.Portions));
            }
            """, "Kitchen/Preparation/PreparePortions.cs"),
            ("""
            namespace Kitchen.Planning;

            public record PlanBatch(System.Guid Id, int Portions);
            public record BatchPlanned(int Portions);
            public class SoupBatch;
            public static class PlanBatchEndpoint
            {
                public static System.Threading.Tasks.Task<string[]> ValidateAsync(PlanBatch command) =>
                    System.Threading.Tasks.Task.FromResult(command.Portions > 0 ? System.Array.Empty<string>() : new[] { "Portions must be positive" });

                [Wolverine.Http.WolverinePost("/batches")]
                public static void Post(PlanBatch command, Marten.IDocumentSession session) =>
                    session.Events.StartStream<SoupBatch>(command.Id, new BatchPlanned(command.Portions));
            }
            """, "Kitchen/Planning/PlanBatch.cs")
        ])],
        new CritterStackScreenplayOptions { Domain = "CommunityKitchen" });

    [Fact] void should_succeed() => _result.IsSuccess.ShouldBeTrue();
    [Fact] void should_keep_the_append_as_a_handler_reference() => Production(_result, "PreparePortions", "PortionsPrepared")!.Key.Discriminator.ShouldEqual("imperative");
    [Fact] void should_keep_the_started_stream_as_a_handler_reference() => Production(_result, "PlanBatch", "BatchPlanned")!.Key.Discriminator.ShouldEqual("imperative");
    [Fact] void should_not_claim_an_unconditional_append() => _result.Source.ShouldNotContain("produces PortionsPrepared");
    [Fact] void should_not_claim_an_unconditional_stream_start() => _result.Source.ShouldNotContain("produces BatchPlanned");
    [Fact] void should_report_the_append_validation_blocker() => _result.Diagnostics.ShouldContain(_ =>
        _.Code == WolverineDiagnosticCodes.ProductionLinkOmitted &&
        _.Severity == GenerationDiagnosticSeverity.Warning &&
        _.Subject == ArtifactNamed(_result, ArtifactKind.Command, "PreparePortions").Key.Subject &&
        _.Message.Contains("compound validation decides whether it is produced", StringComparison.Ordinal) &&
        _.Source!.Path == "Application/Kitchen/Preparation/PreparePortions.cs");
    [Fact] void should_report_the_stream_start_validation_blocker() => _result.Diagnostics.ShouldContain(_ =>
        _.Code == WolverineDiagnosticCodes.ProductionLinkOmitted &&
        _.Subject == ArtifactNamed(_result, ArtifactKind.Command, "PlanBatch").Key.Subject &&
        _.Message.Contains("compound validation decides whether it is produced", StringComparison.Ordinal) &&
        _.Source!.Path == "Application/Kitchen/Planning/PlanBatch.cs");
}
