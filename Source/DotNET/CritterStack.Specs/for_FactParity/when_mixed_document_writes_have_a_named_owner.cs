// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Cratis.CritterStack.Screenplay.for_FactParity;

/// <summary>Both ends of an operation on a proved named store carry its exact scope.</summary>
public class when_mixed_document_writes_have_a_named_owner : given.a_document_delete_handler_comparison
{
    const string Application =
        """
        namespace FactParity
        {
            public interface IAccounts : Marten.IDocumentStore;
            public sealed record Account(System.Guid Id);
            public sealed record CloseAccount(System.Guid Id);
            public static class Configuration
            {
                public static void Configure(Microsoft.Extensions.DependencyInjection.IServiceCollection services) =>
                    Marten.MartenServiceCollectionExtensions.AddMartenStore<IAccounts>(services, options => options.Schema.For<Account>());
            }
            [Wolverine.Marten.MartenStore(typeof(IAccounts))]
            public static class CloseAccountHandler
            {
                public static void Handle(CloseAccount message, Marten.IDocumentSession session)
                {
                    session.Delete<Account>(message.Id);
                    session.Update(new Account(message.Id));
                }
            }
        }
        """;

    void Because() => CompareHandlers(Application);

    [Fact]
    void should_use_the_same_proved_scope_as_the_oracle()
    {
        var command = DocumentSubject("CloseAccount");
        var account = DocumentSubject("Account");
        var handler = MethodSubject("CloseAccountHandler");
        var scope = AccountScope();
        var oracle = Commands(Expected).Single();
        oracle.Name.ShouldEqual("CloseAccount");
        oracle.Key.Scope.ShouldEqual(scope);
        Commands(PublicComposed).Single().Key.Scope.ShouldEqual(scope);
        RelationshipKey[] operations = [Operation(handler, RelationshipKind.Deletes, account, scope), Operation(handler, RelationshipKind.Updates, account, scope)];
        Outgoing(Oracle, handler).ShouldEqual(operations);
        Outgoing(NativeMarten, handler).ShouldEqual(operations);
        Outgoing(Native, command).ShouldEqual([Operation(command, RelationshipKind.Deletes, account, scope)]);
    }

    BackendScopeKey AccountScope()
    {
        var registration = ComparedProject.AuthoredSyntaxTrees.SelectMany(tree => tree.GetRoot().DescendantNodes().OfType<InvocationExpressionSyntax>())
            .Single(invocation => invocation.Expression is MemberAccessExpressionSyntax member &&
                string.Equals(member.Name.Identifier.ValueText, "AddMartenStore", StringComparison.Ordinal));
        return new() { Store = new() { Value = $"dotnet:project/FactParity%2FFactParity/marten-store-registration/Application.cs@{registration.SpanStart}:{registration.Span.Length}" } };
    }

    [Fact] void should_admit_both_producers_in_all_compositions() => AllAdmitted().ShouldBeTrue();
    [Fact] void should_match_artifacts_in_all_compositions() => AllArtifactsMatch().ShouldBeTrue();
    [Fact] void should_match_relationships_in_all_compositions() => AllRelationshipsMatch().ShouldBeTrue();
    [Fact] void should_match_full_diagnostics_in_all_compositions() => AssertDiagnosticsMatch();
    [Fact] void should_match_printed_source_in_all_compositions() => AllSourcesMatch().ShouldBeTrue();
    [Fact] void should_match_status_in_all_compositions() => AllStatusesMatch().ShouldBeTrue();
}
