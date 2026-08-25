// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Cratis.CritterStack.Screenplay.for_CritterStackScreenplayAdapter;

public class when_analyzing_compound_handler_stages : given.a_compound_stage_handler_application
{
    ResolvedApplicationGraph _graph = null!;

    void Because() => _graph = new GenerationResolver().Resolve([Contribution]);

    [Fact] void should_compile_the_fixture() => Project.Compilation.GetDiagnostics().Where(_ => _.Severity == Microsoft.CodeAnalysis.DiagnosticSeverity.Error).ShouldBeEmpty();
    [Fact] void should_preserve_the_request_as_a_command() => ArtifactNames(ArtifactKind.Command).ShouldContainOnly("InspectionRequested");
    [Fact] void should_select_the_message_after_the_bound_parameter() => ArtifactNames(ArtifactKind.Command).ShouldNotContain("InspectionRecord");
    [Fact] void should_retain_the_queryable_read() => Contribution.Facts.OfType<RelationshipFact>().Count(_ => _.Definition.Key.Kind == RelationshipKind.Reads && RelationshipTarget(_) == "InspectionRecord").ShouldEqual(1);
    [Fact] void should_lower_the_before_stage_cascade() => RelationshipTarget(Cascade).ShouldEqual("InspectionRefused");
    [Fact] void should_retain_the_stage_discriminator() => Cascade.Definition.Key.Discriminator.ShouldEqual("stage:Before");
    [Fact] void should_not_treat_loaded_data_as_a_message() => ArtifactNames(ArtifactKind.Message).ShouldNotContain("InspectionLookup");
    [Fact] void should_not_treat_continuation_control_as_a_message() => ArtifactNames(ArtifactKind.Message).ShouldNotContain("HandlerContinuation");
    [Fact] void should_report_each_compound_stage() => CompoundStageDiagnostics.Count.ShouldEqual(2);
    [Fact] void should_report_the_load_stage() => CompoundStageDiagnostics.Count(_ => _.Message.Contains("'LoadAsync'", StringComparison.Ordinal)).ShouldEqual(1);
    [Fact] void should_report_the_before_stage() => CompoundStageDiagnostics.Count(_ => _.Message.Contains("'Before'", StringComparison.Ordinal)).ShouldEqual(1);
    [Fact] void should_report_short_circuit_control() => CompoundStageDiagnostics.Single(_ => _.Message.Contains("'Before'", StringComparison.Ordinal)).Message.ShouldContain("can short-circuit");
    [Fact] void should_attach_stage_diagnostics_to_the_entry_point() => CompoundStageDiagnostics.Select(_ => _.Subject).Distinct().Count().ShouldEqual(1);
    [Fact] void should_classify_stage_loss_as_unsupported_information() => CompoundStageDiagnostics.All(_ => _.Severity == GenerationDiagnosticSeverity.Information && _.Outcome == GenerationDiagnosticOutcome.Unsupported).ShouldBeTrue();
    [Fact] void should_not_report_other_loss() => Contribution.Diagnostics.Count.ShouldEqual(2);

    IReadOnlyList<GenerationDiagnostic> CompoundStageDiagnostics =>
        [.. Contribution.Diagnostics.Where(_ => _.Code == "WOLVERINE0020")];

    RelationshipFact Cascade => Contribution.Facts.OfType<RelationshipFact>().Single(_ => _.Definition.Key.Kind == RelationshipKind.Cascades);

    IReadOnlyList<string> ArtifactNames(ArtifactKind kind) =>
        [.. _graph.Artifacts.Where(_ => _.Key.Kind == kind).Select(_ => _.Variants.Single().Definition.Name)];

    string RelationshipTarget(RelationshipFact relationship) =>
        _graph.Artifacts.Single(_ => _.Key.Subject == relationship.Definition.Key.Target).Variants.Single().Definition.Name;
}
