// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Cratis.CritterStack.Screenplay.for_FactParity;

/// <summary>A conventional command handler and its deleted documents carry the proved store scope.</summary>
public class when_conventional_handlers_delete_documents_in_a_named_store : given.a_document_delete_handler_comparison
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
                public static void Handle(CloseAccount message, Marten.IDocumentSession session) => session.Delete<Account>(message.Id);
            }
        }
        """;

    void Because() => CompareHandlers(Application);

    [Fact] void should_declare_the_scoped_command() => Commands(PublicComposed).Single().Key.Scope.ShouldNotBeNull();
    [Fact] void should_scope_both_ends_of_the_delete_relationship() => NativeDeletes.All(fact => fact.Definition.Key.SourceScope is not null && fact.Definition.Key.TargetScope is not null).ShouldBeTrue();
    [Fact] void should_emit_a_delete_relationship() => NativeDeletes.Count().ShouldEqual(1);
    [Fact] void should_be_admitted_in_both_orders() => AllAdmitted().ShouldBeTrue();
    [Fact] void should_match_artifacts_in_both_orders() => AllArtifactsMatch().ShouldBeTrue();
    [Fact] void should_match_relationships_in_both_orders() => AllRelationshipsMatch().ShouldBeTrue();
    [Fact] void should_match_full_diagnostics_in_both_orders() => AssertDiagnosticsMatch();
    [Fact] void should_match_printed_source_in_both_orders() => AllSourcesMatch().ShouldBeTrue();
    [Fact] void should_match_status_in_both_orders() => AllStatusesMatch().ShouldBeTrue();
}
