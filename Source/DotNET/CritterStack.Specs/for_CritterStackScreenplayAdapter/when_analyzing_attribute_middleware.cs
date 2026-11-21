// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Cratis.CritterStack.Screenplay.for_CritterStackScreenplayAdapter;

public class when_analyzing_attribute_middleware : given.a_wolverine_attribute_middleware_application
{
    const string Source =
        """
        using Wolverine.Attributes;
        namespace AttributeMiddleware;
        public record Process(string Id);
        public record Prepared(string Id);
        [Middleware(typeof(SharedMiddleware))]
        public static class ProcessHandler
        {
            [WolverineBefore]
            public static System.Threading.Tasks.Task<Prepared> Prepare(Process message) => System.Threading.Tasks.Task.FromResult(new Prepared(message.Id));
            public static void Handle(Process message) { }
            [WolverineAfter]
            public static void Finish(Process message) { }
            [WolverineFinally]
            public static void Clean() { }
            [WolverineBefore]
            public static void Before(Process message) { }
            [WolverineAfterCommit]
            public static void Saved(Process message) { }
            [Lookalike.Attributes.WolverineBefore]
            public static void Unrelated(Process message) { }
        }
        public static class SharedMiddleware
        {
            [WolverineBefore]
            public static void Initialize() { }
        }
        """;

    DotNetProjectCompilation _project = null!;
    AdapterContribution _contribution = null!;

    void Establish() => _project = CreateProject(Source);
    void Because() => _contribution = Analyze(_project);

    [Fact] void should_compile_the_fixture() => _project.Compilation.GetDiagnostics(TestContext.Current.CancellationToken).Where(_ => _.Severity == DiagnosticSeverity.Error).ShouldBeEmpty();
    [Fact] void should_recognize_all_six_stages_once() => Stages.Count.ShouldEqual(6);
    [Fact] void should_recognize_arbitrarily_named_before_methods() => Stage("Prepare").Message.ShouldContain("(before)");
    [Fact] void should_recognize_arbitrarily_named_after_methods() => Stage("Finish").Message.ShouldContain("(after)");
    [Fact] void should_recognize_finally_without_a_request_parameter() => Stage("Clean").Message.ShouldContain("(after)");
    [Fact] void should_preserve_after_commit() => Stage("Saved").Message.ShouldContain("(after-commit)");
    [Fact] void should_locate_attribute_stages_at_the_attribute() => Stage("Prepare").Source.StartLine.ShouldEqual(8);
    [Fact] void should_locate_shared_stages_at_the_application_attribute() => Stage("Initialize").Source.StartLine.ShouldEqual(5);
    [Fact] void should_not_match_lookalike_attributes() => Stages.Any(_ => _.Message.Contains("'Unrelated'", StringComparison.Ordinal)).ShouldBeFalse();
    [Fact] void should_report_located_unsupported_information() => Stages.ShouldEachConformTo(_ => _.Severity == GenerationDiagnosticSeverity.Information && _.Outcome == GenerationDiagnosticOutcome.Unsupported && _.Source.Path == "MiddlewareApplication/Handlers.cs");
    [Fact] void should_keep_proven_stage_return_consequences() => _contribution.Facts.OfType<RelationshipFact>().Count(_ => _.Definition.Key.Kind == RelationshipKind.Cascades).ShouldEqual(1);

    IReadOnlyList<GenerationDiagnostic> Stages => [.. _contribution.Diagnostics.Where(_ => _.Code == WolverineDiagnosticCodes.CompoundStageOmitted)];
    GenerationDiagnostic Stage(string method) => Stages.Single(_ => _.Message.Contains($"'{method}'", StringComparison.Ordinal));
}
