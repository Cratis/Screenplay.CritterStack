// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Cratis.CritterStack.Screenplay.for_CritterStackScreenplayGenerator;

public class when_characterizing_wolverine_dcb_query_variants : given.a_wolverine_dcb_competition_application
{
    const string Source =
        """
        using System.Threading.Tasks;
        using JasperFx.Events.Tags;
        namespace Accounts;

        public record AccountId(System.Guid Value);
        public sealed class AccountState;
        public record AccountOpened(AccountId AccountId);
        public record AccountClosed(AccountId AccountId);
        public record ValidFirst(AccountId AccountId);
        public record ValidFirstDone(AccountId AccountId);
        public record ValidOnly(AccountId AccountId);
        public record ValidOnlyDone(AccountId AccountId);
        public record ValueTaskCtor(AccountId AccountId);
        public record ValueTaskCtorDone(AccountId AccountId);

        public static class ValidFirstHandler
        {
            public static EventTagQuery Load(ValidFirst command) =>
                EventTagQuery.For<AccountId>(command.AccountId)
                    .AndEventsOfType<AccountOpened>()
                    .AndEventsOfType<AccountOpened[], AccountClosed>();

            public static ValidFirstDone Handle(
                ValidFirst command,
                [Wolverine.Persistence.EventSourcing.DcbModel] AccountState state) =>
                new(command.AccountId);
        }

        public static class ValidOnlyHandler
        {
            public static EventTagQuery Load(ValidOnly command) =>
                EventTagQuery.For<AccountId>(command.AccountId)
                    .AndEventsOfType<AccountOpened>()
                    .AndEventsOfType<AccountClosed>();

            public static ValidOnlyDone Handle(
                ValidOnly command,
                [Wolverine.Persistence.EventSourcing.DcbModel] AccountState state) =>
                new(command.AccountId);
        }

        public static class ValueTaskCtorHandler
        {
            public static ValueTask<EventTagQuery> LoadAsync(ValueTaskCtor command)
            {
                var query = EventTagQuery.For<AccountId>(command.AccountId).AndEventsOfType<AccountOpened>();
                return new ValueTask<EventTagQuery>(query);
            }

            public static ValueTaskCtorDone Handle(
                ValueTaskCtor command,
                [Wolverine.Persistence.EventSourcing.DcbModel] AccountState state) =>
                new(command.AccountId);
        }
        """;

    (string Expected, string Actual) _transcript;

    void Establish()
    {
        var framework = Project.Compilation.SyntaxTrees.First();
        var application = CSharpSyntaxTree.ParseText(Source, path: "/workspace/Accounts/Types.cs");
        Project = Project with
        {
            Compilation = Project.Compilation.RemoveAllSyntaxTrees().AddSyntaxTrees(framework, application),
            AuthoredSyntaxTrees = new HashSet<SyntaxTree> { framework, application }
        };
    }

    void Because() => _transcript = given.generation_transcript.Compare(
        new CritterStackScreenplayGenerator().Generate([Project], new CritterStackScreenplayOptions { Domain = "Accounts" }),
        "wolverine-dcb-query-variants");

    [Fact] void should_compile_the_input() => Project.Compilation.GetDiagnostics(TestContext.Current.CancellationToken).Where(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error).ShouldBeEmpty();
    [Fact] void should_preserve_every_diagnostic_and_relationship() => _transcript.Actual.ShouldEqual(_transcript.Expected);
}
