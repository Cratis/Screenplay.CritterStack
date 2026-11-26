// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Cratis.CritterStack.Screenplay.for_FactParity;

/// <summary>The admission predicate rejects missing, empty, non-admitted and rejection-carrying evidence instead of passing vacuously.</summary>
public class when_http_command_admission_evidence_is_missing_or_rejected : given.a_native_http_command_comparison
{
    const string Application =
        """
        namespace FactParity
        {
            public sealed record Simple(string Name);
            public static class Endpoints
            {
                [Wolverine.Http.WolverinePost("/simple")]
                public static void Post(Simple command) { }
            }
        }
        """;

    void Because() => Compare(Application);

    [Fact] void should_accept_the_real_admitted_run() => IsAdmitted.ShouldBeTrue();

    [Fact]
    void should_reject_an_empty_roster() =>
        Admitted(Public with { AdapterRun = Public.AdapterRun with { Adapters = [] } }, ExpectedIdentity).ShouldBeFalse();

    [Fact]
    void should_reject_a_roster_without_the_expected_adapter() =>
        Admitted(Public, new AdapterIdentity { Id = "someone.else", Version = "1.0.0" }).ShouldBeFalse();

    [Fact]
    void should_reject_a_record_that_was_not_executed() =>
        Admitted(Public with { AdapterRun = Public.AdapterRun with { Adapters = [.. Public.AdapterRun.Adapters.Select(_ => _ with { Executed = false })] } }, ExpectedIdentity).ShouldBeFalse();

    [Fact]
    void should_reject_a_record_that_was_not_admitted() =>
        Admitted(Public with { AdapterRun = Public.AdapterRun with { Adapters = [.. Public.AdapterRun.Adapters.Select(_ => _ with { Disposition = AdapterRunDisposition.RosterRejected })] } }, ExpectedIdentity).ShouldBeFalse();

    [Fact]
    void should_reject_a_record_without_a_completed_execution() =>
        Admitted(Public with { AdapterRun = Public.AdapterRun with { Adapters = [.. Public.AdapterRun.Adapters.Select(_ => _ with { Execution = new AdapterExecutionNotRun() })] } }, ExpectedIdentity).ShouldBeFalse();

    [Fact]
    void should_reject_a_runner_rejection_diagnostic_beside_admitted_looking_records() =>
        Admitted(
            Public with
            {
                Diagnostics =
                [
                    .. Public.Diagnostics,
                    new GenerationDiagnostic
                    {
                        Code = "DOTNETADAPTER007",
                        Severity = GenerationDiagnosticSeverity.Error,
                        Outcome = GenerationDiagnosticOutcome.Unknown,
                        Message = "rejected"
                    }
                ]
            },
            ExpectedIdentity).ShouldBeFalse();
}
