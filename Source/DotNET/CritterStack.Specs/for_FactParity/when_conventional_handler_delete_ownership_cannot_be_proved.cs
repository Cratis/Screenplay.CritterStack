// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Cratis.CritterStack.Screenplay.for_FactParity;

/// <summary>Ambiguous store ownership is diagnosed without emitting a command or delete edge.</summary>
public class when_conventional_handler_delete_ownership_cannot_be_proved : given.a_document_delete_handler_comparison
{
    const string Application =
        """
        namespace FactParity
        {
            public interface IAccounts : Marten.IDocumentStore;
            public interface IBilling : Marten.IDocumentStore;
            public sealed record Shared(System.Guid Id);
            public sealed record RemoveShared(System.Guid Id);
            public static class Configuration
            {
                public static void Configure(Microsoft.Extensions.DependencyInjection.IServiceCollection services)
                {
                    Marten.MartenServiceCollectionExtensions.AddMartenStore<IAccounts>(services, options => options.Schema.For<Shared>());
                    Marten.MartenServiceCollectionExtensions.AddMartenStore<IBilling>(services, options => options.Schema.For<Shared>());
                }
            }
            [Wolverine.Marten.MartenStore(typeof(IAccounts))]
            [Wolverine.Marten.MartenStore(typeof(IBilling))]
            public static class RemoveSharedHandler
            {
                public static void Handle(RemoveShared message, Marten.IDocumentSession session) => session.Delete<Shared>(message.Id);
            }
        }
        """;

    void Because() => CompareHandlers(Application);

    [Fact] void should_not_declare_an_optimistic_command() => Native.Facts.OfType<ArtifactDeclarationFact>().ShouldBeEmpty();
    [Fact] void should_not_emit_an_optimistic_delete() => NativeDeletes.ShouldBeEmpty();
    [Fact] void should_report_the_ownership_diagnostic() => Native.Diagnostics.Single().Code.ShouldEqual(GenerationDiagnosticCodes.AmbiguousBackendScopeOwnership);
    [Fact] void should_be_admitted_in_both_orders() => AllAdmitted().ShouldBeTrue();
    [Fact] void should_match_artifacts_in_both_orders() => AllArtifactsMatch().ShouldBeTrue();
    [Fact] void should_match_relationships_in_both_orders() => AllRelationshipsMatch().ShouldBeTrue();
    [Fact] void should_match_full_diagnostics_in_both_orders() => AssertDiagnosticsMatch();
    [Fact] void should_match_printed_source_in_both_orders() => AllSourcesMatch().ShouldBeTrue();
    [Fact] void should_match_status_in_both_orders() => AllStatusesMatch().ShouldBeTrue();
}
