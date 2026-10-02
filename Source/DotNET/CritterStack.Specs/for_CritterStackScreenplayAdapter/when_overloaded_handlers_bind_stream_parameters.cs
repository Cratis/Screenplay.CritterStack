// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Cratis.CritterStack.Screenplay.for_CritterStackScreenplayAdapter;

public class when_overloaded_handlers_bind_stream_parameters : given.a_wolverine_fetch_many_application
{
    AdapterContribution _result = null!;
    AdapterContribution _reversed = null!;

    void Establish() => UseApplication(
        """
        namespace Overloads;
        public class Account;
        public record Change;
        public record Changed;
        public static class ChangeHandler
        {
            public static void Handle(Change command, JasperFx.Events.IEventStream<Account> account, int unused) => account.AppendOne(new Changed());
            public static void Handle(Change command, JasperFx.Events.IEventStream<Account> account, string unused) => account.AppendOne(new Changed());
        }
        """);

    void Because()
    {
        var adapter = new CritterStackScreenplayAdapter();
        _result = adapter.Analyze(new([Project]), new());
        var reversed = Project with { Compilation = Project.Compilation.RemoveAllSyntaxTrees().AddSyntaxTrees(Project.Compilation.SyntaxTrees.Reverse()) };
        _reversed = adapter.Analyze(new([reversed]), new());
    }

    [Fact] void should_compile_the_input() => Project.Compilation.GetDiagnostics().Where(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error).ShouldBeEmpty();
    [Fact] void should_keep_overloaded_append_targets_distinct() => Discriminators(_result).Distinct().Count().ShouldEqual(2);
    [Fact] void should_keep_discriminators_stable_when_syntax_trees_are_reordered() => Discriminators(_result).ShouldContainOnly(Discriminators(_reversed));

    static IEnumerable<string?> Discriminators(AdapterContribution result) => result.Facts.OfType<RelationshipFact>().Where(fact => fact.Definition.Key.Kind == RelationshipKind.Appends).Select(fact => fact.Definition.Key.Discriminator);
}
