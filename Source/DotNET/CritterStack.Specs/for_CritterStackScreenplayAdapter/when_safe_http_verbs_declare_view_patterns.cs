// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Cratis.CritterStack.Screenplay.for_CritterStackScreenplayAdapter;

public class when_safe_http_verbs_declare_view_patterns : given.a_wolverine_slice_pattern_application
{
    AdapterContribution _result = null!;

    void Establish() => Project = CreateApplication(
        """
        using Wolverine.Persistence.EventSourcing;
        using JasperFx.Events.EventModeling;
        namespace Patterns;
        public record Details(string Name);
        public static class SafeEndpoints
        {
            [Wolverine.Http.WolverineHead("/details"), SlicePattern(SlicePattern.View)]
            public static Details Head() => new("Example");
            [Wolverine.Http.WolverineOptions("/details"), SlicePattern(SlicePattern.View)]
            public static Details Options() => new("Example");
        }
        """);

    void Because() => _result = new CritterStackScreenplayAdapter().Analyze(new([Project]), new());

    [Fact] void should_compile_the_input() => Project.Compilation.GetDiagnostics().Where(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error).ShouldBeEmpty();
    [Fact] void should_keep_both_endpoints_on_the_effect_analysis_path() => _result.Facts.OfType<ArtifactFact>().Where(fact => fact.Definition.Key.Kind == ArtifactKind.Command).Select(fact => fact.Definition.Name).ShouldContainOnly(["Head", "Options"]);
    [Fact] void should_not_infer_queries_from_the_verb() => _result.Facts.OfType<ArtifactFact>().Where(fact => fact.Definition.Key.Kind == ArtifactKind.Query).ShouldBeEmpty();
    [Fact] void should_apply_the_declared_view_classification() => _result.Facts.OfType<ArtifactPlacementFact>().All(fact => fact.Placement.SliceKind == GenerationSliceKind.StateView).ShouldBeTrue();
    [Fact] void should_not_report_a_false_trigger_conflict() => _result.Diagnostics.Where(diagnostic => diagnostic.Code == WolverineDiagnosticCodes.SlicePatternConflict).ShouldBeEmpty();
}
