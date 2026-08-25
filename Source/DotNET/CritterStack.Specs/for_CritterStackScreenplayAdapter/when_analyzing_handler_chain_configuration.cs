// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Cratis.CritterStack.Screenplay.for_CritterStackScreenplayAdapter;

public class when_analyzing_handler_chain_configuration : given.a_handler_chain_configuration_application
{
    void Because() => new GenerationResolver().Resolve([Contribution]);

    [Fact] void should_compile_the_baseline_fixture() => BaselineProject.Compilation.GetDiagnostics().Where(_ => _.Severity == Microsoft.CodeAnalysis.DiagnosticSeverity.Error).ShouldBeEmpty();
    [Fact] void should_compile_the_configured_fixture() => Project.Compilation.GetDiagnostics().Where(_ => _.Severity == Microsoft.CodeAnalysis.DiagnosticSeverity.Error).ShouldBeEmpty();
    [Fact] void should_report_the_chain_configuration_once() => ChainDiagnostics.Count.ShouldEqual(1);
    [Fact] void should_report_retry_and_discard_delivery_loss() => ChainDiagnostics.Single().Message.ShouldContain("retry or discard delivery semantics");
    [Fact] void should_classify_the_loss_as_unsupported_information() => ChainDiagnostics.All(_ => _.Severity == GenerationDiagnosticSeverity.Information && _.Outcome == GenerationDiagnosticOutcome.Unsupported).ShouldBeTrue();
    [Fact] void should_not_change_discovered_facts() => Contribution.Facts.Select(_ => _.Id.Value).ShouldContainOnly(BaselineContribution.Facts.Select(_ => _.Id.Value));
    [Fact] void should_not_report_other_loss() => Contribution.Diagnostics.Count.ShouldEqual(1);

    IReadOnlyList<GenerationDiagnostic> ChainDiagnostics =>
        [.. Contribution.Diagnostics.Where(_ => _.Code == "WOLVERINE0021")];
}
