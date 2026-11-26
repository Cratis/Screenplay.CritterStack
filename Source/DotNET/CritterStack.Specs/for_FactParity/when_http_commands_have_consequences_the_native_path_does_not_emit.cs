// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Cratis.CritterStack.Screenplay.for_FactParity;

/// <summary>
/// A simple endpoint mixed with a bus endpoint and a state-input endpoint: the native contribution emits only the complete simple unit and
/// reports every other endpoint as unsupported. It does not pretend parity with the compatibility oracle, which emits the other commands.
/// </summary>
public class when_http_commands_have_consequences_the_native_path_does_not_emit : given.a_native_http_command_comparison
{
    const string Application =
        """
        namespace FactParity
        {
            public sealed record Simple(string Name);
            public sealed record WithBus(string Name);
            public sealed record Notified(string Name);
            public sealed record Touch(string Note);
            public sealed record Account(System.Guid Id, string Name);
            public static class Endpoints
            {
                [Wolverine.Http.WolverinePost("/simple")]
                public static void Post(Simple command) { }
                [Wolverine.Http.WolverinePost("/bus")]
                public static System.Threading.Tasks.ValueTask PostBus(WithBus command, Wolverine.IMessageBus bus) => bus.PublishAsync(new Notified(command.Name));
                [Wolverine.Http.WolverinePost("/state")]
                public static void PostState(Touch command, [Wolverine.Persistence.EventSourcing.ReadModel] Account account) { }
            }
        }
        """;

    void Because() => Compare(Application);

    [Fact] void should_emit_only_the_complete_simple_command() => Commands(Public).Select(_ => _.Name).ToArray().ShouldEqual(["Simple"]);
    [Fact]
    void should_report_each_other_endpoint_as_unsupported() =>
        Native.Diagnostics.Count(_ => _.Code == WolverineFacts.NativeHttpCommandOmittedCode && _.Outcome == GenerationDiagnosticOutcome.Unsupported).ShouldEqual(2);
    [Fact] void should_not_emit_facts_for_the_state_input_endpoint() => Native.Facts.Any(_ => _.Subject.Value.Contains("PostState")).ShouldBeFalse();
    [Fact] void should_not_claim_parity_with_the_compatibility_commands() => Commands(Expected).Count().ShouldBeGreaterThan(1);
}
