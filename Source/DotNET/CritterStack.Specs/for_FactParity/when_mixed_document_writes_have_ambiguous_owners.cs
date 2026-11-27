// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Cratis.CritterStack.Screenplay.for_FactParity;

/// <summary>A mixed-write command cannot choose optimistically among ambiguous stores.</summary>
public class when_mixed_document_writes_have_ambiguous_owners : given.a_document_delete_handler_comparison
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
                public static void Handle(RemoveShared message, Marten.IDocumentSession session)
                {
                    session.Delete<Shared>(message.Id);
                    session.Insert(new Shared(message.Id));
                }
            }
        }
        """;

    void Because() => CompareHandlers(Application);

    [Fact]
    void should_diagnose_ambiguity_without_an_optimistic_command()
    {
        Commands(Expected).ShouldBeEmpty();
        Expected.Diagnostics.Any(diagnostic => diagnostic.Code == GenerationDiagnosticCodes.AmbiguousBackendScopeOwnership).ShouldBeTrue();
        Native.Diagnostics.Single().Code.ShouldEqual(GenerationDiagnosticCodes.AmbiguousBackendScopeOwnership);
        Native.Facts.ShouldBeEmpty();
    }

    [Fact] void should_admit_both_producers_in_all_compositions() => AllAdmitted().ShouldBeTrue();
    [Fact] void should_match_artifacts_in_all_compositions() => AllArtifactsMatch().ShouldBeTrue();
    [Fact] void should_match_relationships_in_all_compositions() => AllRelationshipsMatch().ShouldBeTrue();
    [Fact] void should_match_full_diagnostics_in_all_compositions() => AssertDiagnosticsMatch();
    [Fact] void should_match_printed_source_in_all_compositions() => AllSourcesMatch().ShouldBeTrue();
    [Fact] void should_match_status_in_all_compositions() => AllStatusesMatch().ShouldBeTrue();
}
