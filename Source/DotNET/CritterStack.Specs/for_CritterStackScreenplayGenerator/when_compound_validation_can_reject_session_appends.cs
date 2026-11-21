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
    [Fact] void should_keep_the_append_as_a_handler_reference() => Production(_result, "PreparePortions", "PortionsPrepared").Key.Discriminator.ShouldEqual("imperative");
    [Fact] void should_keep_the_started_stream_as_a_handler_reference() => Production(_result, "PlanBatch", "BatchPlanned").Key.Discriminator.ShouldEqual("imperative");
    [Fact] void should_not_claim_an_unconditional_append() => _result.Source.ShouldNotContain("produces PortionsPrepared");
    [Fact] void should_not_claim_an_unconditional_stream_start() => _result.Source.ShouldNotContain("produces BatchPlanned");
    [Fact] void should_report_one_blocker_for_the_append() => Blockers("PreparePortions").Count.ShouldEqual(1);
    [Fact] void should_report_the_append_blocker_as_a_warning() => Blockers("PreparePortions").Single().Severity.ShouldEqual(GenerationDiagnosticSeverity.Warning);
    [Fact] void should_report_the_append_blocker_as_unsupported() => Blockers("PreparePortions").Single().Outcome.ShouldEqual(GenerationDiagnosticOutcome.Unsupported);
    [Fact] void should_report_the_append_validation_blocker() => Blockers("PreparePortions").Single().Message.ShouldContain("compound validation decides whether it is produced");
    [Fact] void should_anchor_the_append_blocker_in_its_handler() => Blockers("PreparePortions").Single().Source.Path.ShouldEqual("Application/Kitchen/Preparation/PreparePortions.cs");
    [Fact] void should_report_one_blocker_for_the_stream_start() => Blockers("PlanBatch").Count.ShouldEqual(1);
    [Fact] void should_report_the_stream_start_blocker_as_a_warning() => Blockers("PlanBatch").Single().Severity.ShouldEqual(GenerationDiagnosticSeverity.Warning);
    [Fact] void should_report_the_stream_start_blocker_as_unsupported() => Blockers("PlanBatch").Single().Outcome.ShouldEqual(GenerationDiagnosticOutcome.Unsupported);
    [Fact] void should_report_the_stream_start_validation_blocker() => Blockers("PlanBatch").Single().Message.ShouldContain("compound validation decides whether it is produced");
    [Fact] void should_anchor_the_stream_start_blocker_in_its_handler() => Blockers("PlanBatch").Single().Source.Path.ShouldEqual("Application/Kitchen/Planning/PlanBatch.cs");

    IReadOnlyList<GenerationDiagnostic> Blockers(string command) =>
    [
        .. _result.Diagnostics.Where(_ =>
            _.Code == WolverineDiagnosticCodes.ProductionLinkOmitted &&
            _.Subject == ArtifactNamed(_result, ArtifactKind.Command, command).Key.Subject)
    ];
}
