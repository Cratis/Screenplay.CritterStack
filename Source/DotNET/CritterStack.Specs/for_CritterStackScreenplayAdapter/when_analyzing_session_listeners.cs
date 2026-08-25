// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Cratis.CritterStack.Screenplay.for_CritterStackScreenplayAdapter;

public class when_analyzing_session_listeners : given.a_session_listener_application
{
    ResolvedApplicationGraph _graph = null!;

    void Because() => _graph = new GenerationResolver().Resolve([Contribution]);

    [Fact] void should_compile_the_baseline_fixture() => BaselineProject.Compilation.GetDiagnostics().Where(_ => _.Severity == Microsoft.CodeAnalysis.DiagnosticSeverity.Error).ShouldBeEmpty();
    [Fact] void should_compile_the_listener_fixture() => Project.Compilation.GetDiagnostics().Where(_ => _.Severity == Microsoft.CodeAnalysis.DiagnosticSeverity.Error).ShouldBeEmpty();
    [Fact] void should_report_the_listener_once() => ListenerDiagnostics.Count.ShouldEqual(1);
    [Fact] void should_name_the_listener() => ListenerDiagnostics.Single().Message.ShouldContain("CommitListener");
    [Fact] void should_classify_the_loss_as_unsupported_information() => ListenerDiagnostics.All(_ => _.Severity == GenerationDiagnosticSeverity.Information && _.Outcome == GenerationDiagnosticOutcome.Unsupported).ShouldBeTrue();
    [Fact] void should_locate_the_listener_registration() => ListenerDiagnostics.Single().Source?.Path.ShouldEqual("SessionListeners/Listener.cs");
    [Fact] void should_not_change_discovered_facts() => Contribution.Facts.Select(_ => _.Id.Value).ShouldContainOnly(BaselineContribution.Facts.Select(_ => _.Id.Value));
    [Fact] void should_not_report_other_loss() => Contribution.Diagnostics.Count.ShouldEqual(1);
    [Fact] void should_not_invent_listener_artifacts() => _graph.Artifacts.ShouldBeEmpty();

    IReadOnlyList<GenerationDiagnostic> ListenerDiagnostics =>
        [.. Contribution.Diagnostics.Where(_ => _.Code == "MARTEN0016")];
}
