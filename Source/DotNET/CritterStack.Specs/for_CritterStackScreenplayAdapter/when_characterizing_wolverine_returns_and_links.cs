// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Cratis.CritterStack.Screenplay.for_CritterStackScreenplayAdapter;

/// <summary>
/// Pins every fact and diagnostic for one handler per return consequence kind, session append and yield shape, and
/// declared SlicePattern, against a golden file captured from the pre-refactor code. A production link exists only when
/// the loss reason is null; any loss reason turns it into a handler reference with the reason in a diagnostic.
/// </summary>
public class when_characterizing_wolverine_returns_and_links : given.a_wolverine_returns_and_links_variants_application
{
    (string Expected, string Actual) _transcript;

    void Because()
    {
        Analyze();
        _transcript = given.contribution_transcript.Compare(Contribution, "wolverine-returns-and-links");
    }

    [Fact] void should_have_compiler_checked_input() => Project.Compilation.GetDiagnostics(TestContext.Current.CancellationToken).Where(_ => _.Severity == DiagnosticSeverity.Error).ShouldBeEmpty();
    [Fact] void should_preserve_every_fact_and_diagnostic() => _transcript.Actual.ShouldEqual(_transcript.Expected);
    [Fact] void should_keep_the_golden_non_vacuous() => _transcript.Expected.Split('\n').Length.ShouldBeGreaterThan(200);

    [Fact] void should_let_an_unreadable_identity_win_over_a_conditional_append() => Message("ConditionalComputed").ShouldContain("the stream identity is not read directly from a property of the command");
    [Fact] void should_report_a_conditional_append_as_unsupported() => Outcome("ConditionalAppend").ShouldEqual(GenerationDiagnosticOutcome.Unsupported);
    [Fact] void should_report_an_unreadable_identity_as_unknown() => Outcome("ComputedAppend").ShouldEqual(GenerationDiagnosticOutcome.Unknown);
    [Fact] void should_report_a_conditional_yield_as_unsupported_even_without_an_identity() => Outcome("YieldConditionalNoIdentity").ShouldEqual(GenerationDiagnosticOutcome.Unsupported);
    [Fact] void should_prefer_the_conditional_yield_reason_over_the_missing_identity() => Message("YieldConditionalNoIdentity").ShouldContain("the handler yields it only on some paths, and the condition is not represented");
    [Fact] void should_report_a_missing_yield_identity_when_unconditional() => Message("YieldNoIdentity").ShouldContain("the aggregate stream identity is not a command property");
    [Fact] void should_not_report_a_loss_for_a_plain_append() => Omitted("PlainAppend").ShouldBeEmpty();
    [Fact] void should_not_report_a_loss_for_a_plain_yield() => Omitted("YieldPlain").ShouldBeEmpty();

    string Message(string command) => Omitted(command).Single().Message;

    GenerationDiagnosticOutcome? Outcome(string command) => Omitted(command).Single().Outcome;

    IReadOnlyList<GenerationDiagnostic> Omitted(string command) =>
        [.. Contribution.Diagnostics.Where(_ => _.Code == WolverineDiagnosticCodes.ProductionLinkOmitted && _.Message.Contains($"Command '{command}' ", StringComparison.Ordinal))];
}
