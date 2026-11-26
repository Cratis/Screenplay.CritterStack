// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Cratis.CritterStack.Screenplay.for_FactParity;

/// <summary>A delete endpoint owned by one named Marten store: the command and its Deletes edges carry the proved scopes.</summary>
public class when_http_commands_delete_documents_in_a_named_store : given.a_document_delete_http_command_comparison
{
    const string Application =
        """
        namespace FactParity
        {
            public interface IAccounts : Marten.IDocumentStore;
            public sealed record Account(System.Guid Id, string Name);
            public sealed record CloseAccount(System.Guid Id);
            public static class Configuration
            {
                public static void Configure(Microsoft.Extensions.DependencyInjection.IServiceCollection services) =>
                    Marten.MartenServiceCollectionExtensions.AddMartenStore<IAccounts>(services, options => options.Schema.For<Account>());
            }
            [Wolverine.Marten.MartenStore(typeof(IAccounts))]
            public static class CloseAccountEndpoint
            {
                [Wolverine.Http.WolverineDelete("/accounts")]
                public static void Delete(CloseAccount command, Marten.IDocumentSession session) => session.Delete<Account>(command.Id);
            }
        }
        """;

    void Because() => CompareWithMarten(Application);

    [Fact] void should_emit_the_command() => Commands(PublicComposed).Count().ShouldEqual(1);
    [Fact] void should_scope_the_command_and_target_to_the_store() => NativeDeletes.All(_ => _.Definition.Key.SourceScope is not null && _.Definition.Key.TargetScope is not null).ShouldBeTrue();
    [Fact] void should_emit_a_delete_edge() => NativeDeletes.Count().ShouldEqual(1);
    [Fact] void should_match_artifacts_in_every_order() => AllArtifactsMatch().ShouldBeTrue();
    [Fact] void should_match_relationships_in_every_order() => AllRelationshipsMatch().ShouldBeTrue();
    [Fact] void should_match_full_diagnostics_in_every_order() => AllDiagnosticsMatch().ShouldBeTrue();
    [Fact] void should_match_printed_source_in_every_order() => AllSourcesMatch().ShouldBeTrue();
    [Fact] void should_match_status_in_every_order() => AllStatusesMatch().ShouldBeTrue();
    [Fact] void should_be_admitted_by_the_real_descriptor() => WasAdmitted().ShouldBeTrue();
}
