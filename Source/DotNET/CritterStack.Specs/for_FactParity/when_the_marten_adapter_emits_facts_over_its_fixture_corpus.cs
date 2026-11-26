// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Cratis.CritterStack.Screenplay.for_FactParity;

/// <summary>
/// Guards that every fact kind the Marten adapter emits is a capability its descriptor declares. The SDK admission validator decides;
/// parity harnesses bypass admission, so undeclared capabilities (BackendScope, ArtifactUsageDeclaration, MethodArtifactUsage) were invisible.
/// </summary>
public class when_the_marten_adapter_emits_facts_over_its_fixture_corpus : given.a_marten_document_effects_comparison
{
    const string Application =
        """
        #nullable enable
        namespace FactParity
        {
            public interface IAccounts : Marten.IDocumentStore;
            public enum Tier { Basic, Plus }
            public sealed record Account(int Id, Tier Tier, System.Collections.Generic.List<Tier?> Past);
            public sealed record Ledger(int Id);
            public sealed record Entry(int Id);
            public sealed record Customer(int Id);
            public static class Configuration
            {
                public static void Configure(Microsoft.Extensions.DependencyInjection.IServiceCollection services) =>
                    Marten.MartenServiceCollectionExtensions.AddMartenStore<IAccounts>(services, options => options.Schema.For<Account>());

                public static void Project(Marten.StoreOptions options) => options.Projections.Add<PostingProjection>();
            }
            public sealed class PostingProjection : Marten.Events.Projections.EventProjection
            {
                public override System.Threading.Tasks.ValueTask ApplyAsync(Marten.IDocumentOperations operations, object e, System.Threading.CancellationToken token)
                {
                    operations.Store(new Ledger(1));
                    operations.DeleteWhere<Entry>(_ => _.Id > 5);
                    return default;
                }
            }
            public static class Opening
            {
                [Wolverine.Marten.MartenStore(typeof(IAccounts))]
                public static void Run(Marten.IDocumentSession session)
                {
                    session.Store(new Account(1, Tier.Basic, []));
                    session.Update(new Entry(1));
                }
            }
            public static class Typed
            {
                public static void Run(Marten.Ancillary.IDocumentSession<IAccounts> session) => session.Insert(new Ledger(2));
            }
            public static class Reading
            {
                public static Customer? Run(Marten.IQuerySession session) => session.Load<Customer>(1);
            }
        }
        """;

    void Because() => Compare(Application);

    [Fact]
    void should_declare_every_emitted_fact_capability() =>
        AdapterContributionAdmission.Admit(new MartenScreenplayAdapter().Descriptor, Contribution)
            .Diagnostics.ShouldNotContain(_ => _.Code == AdapterContributionAdmissionDiagnosticCode.UndeclaredFactCapability);

    [Fact] void should_emit_more_than_one_fact_kind() => Contribution.Facts.Select(_ => _.GetType()).Distinct().Count().ShouldBeGreaterThan(5);
}
