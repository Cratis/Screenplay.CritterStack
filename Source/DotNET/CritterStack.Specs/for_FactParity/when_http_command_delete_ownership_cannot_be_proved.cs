// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Cratis.CritterStack.Screenplay.for_FactParity;

/// <summary>A delete endpoint claimed by two stores: no optimistic command or edge, and the oracle's ownership diagnostic is preserved exactly.</summary>
public class when_http_command_delete_ownership_cannot_be_proved : given.a_document_delete_http_command_comparison
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
            public static class RemoveSharedEndpoint
            {
                [Wolverine.Http.WolverineDelete("/shared")]
                public static void Delete(RemoveShared command, Marten.IDocumentSession session) => session.Delete<Shared>(command.Id);
            }
        }
        """;

    void Because() => CompareWithMarten(Application);

    [Fact] void should_not_emit_an_optimistic_command() => Native.Facts.OfType<ArtifactDeclarationFact>().ShouldBeEmpty();
    [Fact] void should_not_emit_an_optimistic_edge() => NativeDeletes.ShouldBeEmpty();
    [Fact] void should_not_report_an_unsupported_shape() => Native.Diagnostics.ShouldNotContain(_ => _.Code == WolverineFacts.NativeHttpCommandOmittedCode);
    [Fact] void should_report_the_ownership_diagnostic() => Native.Diagnostics.ShouldNotBeEmpty();
    [Fact] void should_be_admitted_by_the_real_runner() => Composites.All(Admitted).ShouldBeTrue();
    [Fact] void should_match_artifacts_in_every_order() => AllArtifactsMatch().ShouldBeTrue();
    [Fact] void should_match_relationships_in_every_order() => AllRelationshipsMatch().ShouldBeTrue();
    [Fact] void should_match_full_diagnostics_in_every_order() => AllDiagnosticsMatch().ShouldBeTrue();
    [Fact] void should_match_printed_source_in_every_order() => AllSourcesMatch().ShouldBeTrue();
    [Fact] void should_match_status_in_every_order() => AllStatusesMatch().ShouldBeTrue();
}
