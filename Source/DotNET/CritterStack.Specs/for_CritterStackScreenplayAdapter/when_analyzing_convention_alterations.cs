// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Cratis.CritterStack.Screenplay.for_CritterStackScreenplayAdapter;

public class when_analyzing_convention_alterations : given.a_convention_alteration_application
{
    ResolvedApplicationGraph _graph = null!;

    void Because() => _graph = new GenerationResolver().Resolve([Contribution]);

    [Fact] void should_compile_the_baseline_fixture() => BaselineProject.Compilation.GetDiagnostics().Where(_ => _.Severity == Microsoft.CodeAnalysis.DiagnosticSeverity.Error).ShouldBeEmpty();
    [Fact] void should_compile_the_altered_fixture() => Project.Compilation.GetDiagnostics().Where(_ => _.Severity == Microsoft.CodeAnalysis.DiagnosticSeverity.Error).ShouldBeEmpty();
    [Fact] void should_report_each_wolverine_alteration() => WolverineDiagnostics.Count.ShouldEqual(4);
    [Fact] void should_report_the_handler_policy_once() => WolverineDiagnostics.Count(_ => _.Message.Contains("CustomHandlerPolicy", StringComparison.Ordinal)).ShouldEqual(1);
    [Fact] void should_report_the_combined_policy_extension_once() => WolverineDiagnostics.Count(_ => _.Message.Contains("CustomPolicyExtension", StringComparison.Ordinal)).ShouldEqual(1);
    [Fact] void should_report_the_chain_modifying_attribute_once() => WolverineDiagnostics.Count(_ => _.Message.Contains("CustomChainAttribute", StringComparison.Ordinal)).ShouldEqual(1);
    [Fact] void should_report_message_discovery_customization_once() => WolverineDiagnostics.Count(_ => _.Message.Contains("message discovery customization", StringComparison.Ordinal)).ShouldEqual(1);
    [Fact] void should_report_each_marten_alteration() => MartenDiagnostics.Count.ShouldEqual(3);
    [Fact] void should_report_the_combined_marten_configuration_once() => MartenDiagnostics.Count(_ => _.Message.Contains("CustomMartenConfiguration", StringComparison.Ordinal)).ShouldEqual(1);
    [Fact] void should_report_the_document_policy_once() => MartenDiagnostics.Count(_ => _.Message.Contains("CustomDocumentPolicy", StringComparison.Ordinal)).ShouldEqual(1);
    [Fact] void should_report_the_projection_document_policy_once() => MartenDiagnostics.Count(_ => _.Message.Contains("CustomProjectionDocumentPolicy", StringComparison.Ordinal)).ShouldEqual(1);
    [Fact] void should_classify_each_alteration_as_unsupported() => Contribution.Diagnostics.All(_ => _.Outcome == GenerationDiagnosticOutcome.Unsupported).ShouldBeTrue();
    [Fact] void should_not_change_discovered_facts() => Contribution.Facts.Select(_ => _.Id.Value).ShouldContainOnly(BaselineContribution.Facts.Select(_ => _.Id.Value));
    [Fact] void should_preserve_message_discovery() => _graph.Artifacts.Any(_ => _.Key.Kind == ArtifactKind.Message && _.Variants.Single().Definition.Name == "PolicyTrigger").ShouldBeTrue();
    [Fact] void should_preserve_reaction_discovery() => _graph.Artifacts.Any(_ => _.Key.Kind == ArtifactKind.Reaction && _.Variants.Single().Definition.Name == "PolicyTrigger").ShouldBeTrue();
    [Fact] void should_not_report_unrelated_diagnostics() => Contribution.Diagnostics.Except(WolverineDiagnostics).Except(MartenDiagnostics).ShouldBeEmpty();

    IReadOnlyList<GenerationDiagnostic> WolverineDiagnostics =>
        [.. Contribution.Diagnostics.Where(_ => _.Code == "WOLVERINE0019")];

    IReadOnlyList<GenerationDiagnostic> MartenDiagnostics =>
        [.. Contribution.Diagnostics.Where(_ => _.Code == "MARTEN0014")];
}
