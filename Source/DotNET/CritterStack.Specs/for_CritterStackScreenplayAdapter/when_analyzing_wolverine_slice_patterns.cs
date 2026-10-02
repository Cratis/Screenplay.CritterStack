// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Cratis.CritterStack.Screenplay.for_CritterStackScreenplayAdapter;

public class when_analyzing_wolverine_slice_patterns : given.a_wolverine_slice_pattern_application
{
    AdapterContribution _result = null!;

    void Because() => _result = new CritterStackScreenplayAdapter().Analyze(new([Project]), new());

    [Fact] void should_have_compiler_checked_input() => Project.Compilation.GetDiagnostics().Where(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error).ShouldBeEmpty();
    [Fact] void should_map_command_explicitly_and_prefer_method_over_type() => Placement("Change").Placement.SliceKind.ShouldEqual(GenerationSliceKind.StateChange);
    [Fact] void should_map_view_without_changing_the_command_kind() => Placement("View").Placement.SliceKind.ShouldEqual(GenerationSliceKind.StateView);
    [Fact] void should_map_automation_over_persistence_inference() => Placement("Automate").Placement.SliceKind.ShouldEqual(GenerationSliceKind.Automation);
    [Fact] void should_map_translation_explicitly() => Placement("Translate").Placement.SliceKind.ShouldEqual(GenerationSliceKind.Translate);
    [Fact] void should_classify_produced_events_with_the_command() => Placement("Automated", ArtifactKind.Event).Placement.SliceKind.ShouldEqual(GenerationSliceKind.Automation);
    [Fact] void should_retain_append_behavior() => _result.Facts.OfType<RelationshipFact>().Any(fact => fact.Definition.Key.Kind == RelationshipKind.Appends && fact.Subject == Placement("Automate").Subject).ShouldBeTrue();
    [Fact] void should_keep_http_write_trigger_classification() => Placement("HttpChange").Placement.SliceKind.ShouldEqual(GenerationSliceKind.StateChange);
    [Fact] void should_keep_http_read_trigger_classification() => Placement("Get", ArtifactKind.Query).Placement.SliceKind.ShouldEqual(GenerationSliceKind.StateView);
    [Fact] void should_keep_timeout_scheduler_classification() => Placement("Timeout").Placement.SliceKind.ShouldEqual(GenerationSliceKind.Automation);
    [Fact] void should_report_only_proven_trigger_conflicts() => Diagnostics(WolverineDiagnosticCodes.SlicePatternConflict).Count.ShouldEqual(3);
    [Fact] void should_locate_trigger_conflicts_at_the_attribute() => Diagnostics(WolverineDiagnosticCodes.SlicePatternConflict).All(diagnostic => diagnostic.Outcome == GenerationDiagnosticOutcome.Conflict && diagnostic.Source?.Path == "Patterns/Handlers.cs" && diagnostic.Source.StartLine > 0).ShouldBeTrue();
    [Fact] void should_preserve_nearest_inherited_type_metadata() => Placement("Inherited").Placement.SliceKind.ShouldEqual(GenerationSliceKind.Translate);
    [Fact] void should_prefer_overridden_method_metadata_to_type_metadata() => Placement("MethodInherited").Placement.SliceKind.ShouldEqual(GenerationSliceKind.Automation);
    [Fact] void should_not_use_an_impostor_attribute() => Placement("Impostor").Placement.SliceKind.ShouldEqual(GenerationSliceKind.StateChange);
    [Fact] void should_not_use_generated_attributes() => Placement("GeneratedMetadata").Placement.SliceKind.ShouldEqual(GenerationSliceKind.StateChange);
    [Fact] void should_retain_inference_for_undefined_values() => Placement("Unknown").Placement.SliceKind.ShouldEqual(GenerationSliceKind.StateChange);
    [Fact] void should_report_undefined_values() => Diagnostics(WolverineDiagnosticCodes.SlicePatternUnresolved).Single().Outcome.ShouldEqual(GenerationDiagnosticOutcome.Unsupported);
    [Fact] void should_report_an_admitted_but_unplaceable_declaration() => Diagnostics(WolverineDiagnosticCodes.SlicePatternOmitted).Single().Message.ShouldContain("UnplacedHandler");
    [Fact] void should_exclude_ignored_handlers() => _result.Diagnostics.Any(diagnostic => diagnostic.Message.Contains("IgnoredHandler", StringComparison.Ordinal)).ShouldBeFalse();
    [Fact] void should_preserve_classification_provenance() => Placement("Automate").Evidence.Explanation.ShouldContain("sliceClassification(strength=Exact, source=Patterns/Handlers.cs:");
    [Fact] void should_not_turn_a_declared_command_cascade_into_a_persisted_event() => _result.Facts.OfType<ArtifactFact>().Any(fact => fact.Definition.Key.Kind == ArtifactKind.Event && fact.Definition.Name == "Cascade").ShouldBeFalse();
    [Fact] void should_keep_reactions_as_reactions() => Placement("DeclaredCommand", ArtifactKind.Reaction).Placement.SliceKind.ShouldEqual(GenerationSliceKind.StateChange);

    ArtifactPlacementFact Placement(string name, ArtifactKind kind = ArtifactKind.Command)
    {
        var key = _result.Facts.OfType<ArtifactFact>().First(fact => fact.Definition.Key.Kind == kind && fact.Definition.Name == name).Definition.Key;
        return _result.Facts.OfType<ArtifactPlacementFact>().Single(fact => fact.Artifact == key);
    }

    IReadOnlyList<GenerationDiagnostic> Diagnostics(string code) => [.. _result.Diagnostics.Where(diagnostic => diagnostic.Code == code)];
}
