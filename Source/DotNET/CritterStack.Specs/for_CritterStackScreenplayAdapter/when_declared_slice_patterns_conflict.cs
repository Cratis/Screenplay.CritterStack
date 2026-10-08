// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Cratis.CritterStack.Screenplay.for_CritterStackScreenplayAdapter;

public class when_declared_slice_patterns_conflict : given.a_wolverine_slice_pattern_application
{
    const string Application =
        """
        namespace Patterns.Transfer;
        public record Move;
        public record Moved;
        public class State;
        public static class FirstHandler
        {
            [Wolverine.Persistence.EventSourcing.SlicePattern(JasperFx.Events.EventModeling.SlicePattern.Automation)]
            public static void Handle(Move command, JasperFx.Events.IEventStream<State> stream) => stream.AppendOne(new Moved());
        }
        public static class SecondHandler
        {
            [Wolverine.Persistence.EventSourcing.SlicePattern(JasperFx.Events.EventModeling.SlicePattern.Translation)]
            public static void Handle(Move command, JasperFx.Events.IEventStream<State> stream) => stream.AppendOne(new Moved());
        }
        """;

    AdapterContribution _strict = null!;
    AdapterContribution _reversed = null!;
    GeneratedScreenplayDefinition _compatibility = null!;
    AdapterContribution _unresolved = null!;

    void Because()
    {
        var adapter = new CritterStackScreenplayAdapter();
        var options = new DotNetAdapterOptions { FeatureRoot = "Source", NamespaceSegmentsToSkip = 1 };
        _strict = adapter.Analyze(new([CreateApplication(Application, strict: true)]), options);
        _reversed = adapter.Analyze(new([CreateApplication(Application, strict: true, reverse: true)]), options);
        _compatibility = new CritterStackScreenplayGenerator().Generate([CreateApplication(Application)], new CritterStackScreenplayOptions { Domain = "Patterns" });
        _unresolved = adapter.Analyze(new([CreateApplication(Application.Replace("JasperFx.Events.EventModeling.SlicePattern.Automation", "MissingConstant", StringComparison.Ordinal))]), new());
    }

    [Fact] void should_fail_closed_for_shared_artifact_kind_conflicts() => _strict.Diagnostics.Any(diagnostic => diagnostic.Code == "DOTNETSP0013").ShouldBeTrue();
    [Fact] void should_not_emit_competing_strict_placements() => _strict.Facts.OfType<ArtifactPlacementFact>().ShouldBeEmpty();
    [Fact] void should_preserve_the_conflict_in_reverse_input_order() => _reversed.Diagnostics.Select(diagnostic => diagnostic.Code + diagnostic.Message).ShouldContainOnly(_strict.Diagnostics.Select(diagnostic => diagnostic.Code + diagnostic.Message));
    [Fact] void should_not_let_compatibility_choose_a_winner() => _compatibility.Diagnostics.Any(diagnostic => diagnostic.Code == "GEN0008").ShouldBeTrue();
    [Fact] void should_report_unresolved_values_without_throwing() => _unresolved.Diagnostics.Single(diagnostic => diagnostic.Code == WolverineDiagnosticCodes.SlicePatternUnresolved).Outcome.ShouldEqual(GenerationDiagnosticOutcome.Unknown);
}
