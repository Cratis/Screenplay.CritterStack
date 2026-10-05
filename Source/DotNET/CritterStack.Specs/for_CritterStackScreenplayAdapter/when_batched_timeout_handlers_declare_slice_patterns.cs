// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Cratis.CritterStack.Screenplay.for_CritterStackScreenplayAdapter;

public class when_batched_timeout_handlers_declare_slice_patterns : given.a_wolverine_slice_pattern_application
{
    AdapterContribution _result = null!;

    void Establish() => Project = CreateApplication(
        """
        namespace Patterns
        {
            public record Expired : Wolverine.TimeoutMessage;
            public record Lapsed;
            public class State;
            public static class ExpiredHandler
            {
                [Wolverine.Persistence.EventSourcing.SlicePattern(JasperFx.Events.EventModeling.SlicePattern.Command)]
                public static void Handle(Expired[] messages, JasperFx.Events.IEventStream<State> stream) => stream.AppendOne(new Lapsed());
            }
        }
        """);

    void Because() => _result = new CritterStackScreenplayAdapter().Analyze(new([Project]), new());

    [Fact] void should_compile_the_input() => Project.Compilation.GetDiagnostics().Where(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error).ShouldBeEmpty();
    [Fact] void should_report_the_declaration_conflicting_with_the_timeout_trigger() => _result.Diagnostics.Single(diagnostic => diagnostic.Code == WolverineDiagnosticCodes.SlicePatternConflict).Message.ShouldContain("TimeoutMessage");
    [Fact] void should_not_let_the_declaration_classify_the_batched_timeout_handler() => _result.Facts.OfType<ArtifactPlacementFact>().Any(fact => fact.Placement.SliceKind == GenerationSliceKind.StateChange && fact.Evidence.Explanation?.Contains("sliceClassification", StringComparison.Ordinal) == true).ShouldBeFalse();
}
