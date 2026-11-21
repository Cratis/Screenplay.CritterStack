// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Cratis.CritterStack.Screenplay.for_CritterStackScreenplayAdapter;

public class when_undeclared_timeout_handlers_have_persistence_effects : given.a_wolverine_slice_pattern_application
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
                public static void Handle(Expired command, JasperFx.Events.IEventStream<State> stream) => stream.AppendOne(new Lapsed());
            }
        }
        """);

    void Because() => _result = new CritterStackScreenplayAdapter().Analyze(new([Project]), new());

    [Fact] void should_compile_the_input() => Project.Compilation.GetDiagnostics(TestContext.Current.CancellationToken).Where(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error).ShouldBeEmpty();
    [Fact] void should_place_the_command_as_automation() => Placement("Expired").Placement.SliceKind.ShouldEqual(GenerationSliceKind.Automation);
    [Fact] void should_place_the_produced_event_as_automation() => Placement("Lapsed", ArtifactKind.Event).Placement.SliceKind.ShouldEqual(GenerationSliceKind.Automation);
    [Fact] void should_explain_the_placement_by_the_timeout_trigger() => Placement("Expired").Evidence.Explanation.ShouldContain("Proven TimeoutMessage scheduler trigger classifies the slice as Automation");
    [Fact] void should_not_report_a_slice_pattern_conflict() => _result.Diagnostics.Any(diagnostic => diagnostic.Code == WolverineDiagnosticCodes.SlicePatternConflict).ShouldBeFalse();
    [Fact] void should_not_report_an_omitted_slice_pattern() => _result.Diagnostics.Any(diagnostic => diagnostic.Code == WolverineDiagnosticCodes.SlicePatternOmitted).ShouldBeFalse();

    ArtifactPlacementFact Placement(string name, ArtifactKind kind = ArtifactKind.Command)
    {
        var key = _result.Facts.OfType<ArtifactFact>().First(fact => fact.Definition.Key.Kind == kind && fact.Definition.Name == name).Definition.Key;
        return _result.Facts.OfType<ArtifactPlacementFact>().Single(fact => fact.Artifact == key);
    }
}
