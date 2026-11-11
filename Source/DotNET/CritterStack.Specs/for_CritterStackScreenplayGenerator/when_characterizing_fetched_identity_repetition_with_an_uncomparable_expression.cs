// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Cratis.CritterStack.Screenplay.for_CritterStackScreenplayGenerator;

public class when_characterizing_fetched_identity_repetition_with_an_uncomparable_expression : given.a_wolverine_fetch_many_application
{
    (string Expected, string Actual) _transcript;

    void Establish() => UseApplication(
        """
        using System;
        using System.Threading.Tasks;
        namespace Mixed;
        public class Account;
        public record RepeatThenCompute(Guid Id);
        public record RepeatThenComputeAppended;
        public record ComputeThenRepeat(Guid Id);
        public record ComputeThenRepeatAppended;
        public static class RepeatThenComputeHandler
        {
            public static async Task Handle(RepeatThenCompute command, Marten.IDocumentSession session)
            {
                var streams = await session.Events.FetchManyForWriting<Account>([command.Id, command.Id, Compute()]);
                streams[0].AppendOne(new RepeatThenComputeAppended());
            }

            static Guid Compute() => Guid.NewGuid();
        }
        public static class ComputeThenRepeatHandler
        {
            public static async Task Handle(ComputeThenRepeat command, Marten.IDocumentSession session)
            {
                var streams = await session.Events.FetchManyForWriting<Account>([Compute(), command.Id, command.Id]);
                streams[2].AppendOne(new ComputeThenRepeatAppended());
            }

            static Guid Compute() => Guid.NewGuid();
        }
        """);

    void Because() => _transcript = given.generation_transcript.Compare(
        new CritterStackScreenplayGenerator().Generate([Project], new CritterStackScreenplayOptions { Domain = "Mixed" }),
        "fetched-identity-repetition-with-an-uncomparable-expression");

    [Fact] void should_compile_the_input() => Project.Compilation.GetDiagnostics(TestContext.Current.CancellationToken).Where(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error).ShouldBeEmpty();
    [Fact] void should_preserve_every_diagnostic_and_relationship() => _transcript.Actual.ShouldEqual(_transcript.Expected);
}
