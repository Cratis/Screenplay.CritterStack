// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Cratis.CritterStack.Screenplay.for_CritterStackScreenplayGenerator;

public class when_generating_explicit_marten_session_appends : given.a_community_kitchen_application
{
    DotNetProjectCompilation _project = null!;
    GeneratedScreenplayDefinition _result = null!;

    void Establish() => _project = CreateFeatureFolderProject();

    void Because() => _result = new CritterStackScreenplayGenerator().Generate(
        [_project],
        new CritterStackScreenplayOptions { Domain = "CommunityKitchen" });

    [Fact] void should_compile_the_fixture() => _project.Compilation.GetDiagnostics().Where(_ => _.Severity == Microsoft.CodeAnalysis.DiagnosticSeverity.Error).ShouldBeEmpty();
    [Fact] void should_succeed() => _result.IsSuccess.ShouldBeTrue();
    [Fact] void should_link_the_started_stream_declaratively() => Production(_result, "PlanBatch", "BatchPlanned")!.Key.Discriminator.ShouldEqual("declarative");
    [Fact] void should_record_the_started_stream_identity() => Production(_result, "PlanBatch", "BatchPlanned")!.Definitions.Single().SourceMember.ShouldEqual("id");
    [Fact] void should_link_the_appended_event_declaratively() => Production(_result, "PreparePortions", "PortionsPrepared")!.Key.Discriminator.ShouldEqual("declarative");
    [Fact] void should_record_the_appended_stream_identity() => Production(_result, "PreparePortions", "PortionsPrepared")!.Definitions.Single().SourceMember.ShouldEqual("batchId");
    [Fact] void should_mark_the_appended_stream_identity_as_the_command_identifier() =>
        ArtifactNamed(_result, ArtifactKind.Command, "PreparePortions").Variants[0].Definition.Properties.Single(_ => _.IsIdentifier).Name.ShouldEqual("batchId");
    [Fact] void should_print_the_started_stream_production() => _result.Source.ShouldContain("produces BatchPlanned");
    [Fact] void should_print_the_appended_production() => _result.Source.ShouldContain("produces PortionsPrepared");
    [Fact] void should_place_the_started_event_with_its_command() => PlacementOf(_result, ArtifactKind.Event, "BatchPlanned").ShouldEqual(PlacementOf(_result, ArtifactKind.Command, "PlanBatch"));
    [Fact] void should_place_the_appended_event_with_its_command() => PlacementOf(_result, ArtifactKind.Event, "PortionsPrepared").ShouldEqual(PlacementOf(_result, ArtifactKind.Command, "PreparePortions"));
    [Fact] void should_keep_a_computed_stream_identity_as_a_handler_reference() => Production(_result, "AdjustTarget", "TargetAdjusted")!.Key.Discriminator.ShouldEqual("imperative");
    [Fact] void should_report_the_computed_stream_identity() => _result.Diagnostics.ShouldContain(_ =>
        _.Code == WolverineDiagnosticCodes.ProductionLinkOmitted &&
        _.Message.Contains("Command 'AdjustTarget' is not linked to event 'TargetAdjusted'", StringComparison.Ordinal) &&
        _.Message.Contains("the stream identity is not read directly from a property of the command", StringComparison.Ordinal));
    [Fact] void should_keep_a_conditional_append_as_a_handler_reference() => Production(_result, "NoteTasting", "TastingNoted")!.Key.Discriminator.ShouldEqual("imperative");
    [Fact] void should_report_the_conditional_append() => _result.Diagnostics.ShouldContain(_ =>
        _.Code == WolverineDiagnosticCodes.ProductionLinkOmitted &&
        _.Message.Contains("Command 'NoteTasting' is not linked to event 'TastingNoted'", StringComparison.Ordinal) &&
        _.Message.Contains("the append only happens on some paths through the handler", StringComparison.Ordinal));
    [Fact] void should_not_report_a_loss_for_a_linked_command() => _result.Diagnostics.ShouldNotContain(_ =>
        _.Code == WolverineDiagnosticCodes.ProductionLinkOmitted &&
        (_.Message.Contains("'PlanBatch'", StringComparison.Ordinal) || _.Message.Contains("'PreparePortions'", StringComparison.Ordinal)));
}
