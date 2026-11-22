// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Cratis.CritterStack.Screenplay.for_MartenStoreIndex;

public class when_emitting_scoped_wolverine_occurrences : given.a_marten_store_scope_application
{
    const string Source =
        """
        using Marten;
        using Wolverine.Marten;
        using Wolverine.Http;
        using Wolverine.Persistence;
        namespace StoreScope;
        public interface AccountsStore : IDocumentStore;
        public interface BillingStore : IDocumentStore;
        public record ChangeAccount(int Id);
        public record Account(int Id);
        public record AccountChanged(int Id);
        public record StartAccount(System.Guid AccountId);
        public record AccountStarted(System.Guid AccountId);
        public static class Configuration
        {
            public static void Configure(Microsoft.Extensions.DependencyInjection.IServiceCollection services)
            {
                services.AddMarten(o => { });
                services.AddMartenStore<AccountsStore>(o => o.EventModelName = "Accounts");
                services.AddMartenStore<BillingStore>(o => o.EventModelName = "Billing");
            }
        }
        [MartenStore(typeof(AccountsStore))]
        public static class AccountsHandler
        {
            public static void Handle(ChangeAccount command, IDocumentSession session, [Entity] Account existing) => session.Store(new Account(command.Id));
        }
        [MartenStore(typeof(BillingStore))]
        public static class BillingHandler
        {
            public static void Handle(ChangeAccount command, IDocumentSession session, [Entity] Account existing) => session.Store(new Account(command.Id));
        }
        public static class AccountEndpoints
        {
            [MartenStore(typeof(AccountsStore)), WolverineGet("/accounts")]
            public static Account GetAccount([Entity] Account account) => account;
            [MartenStore(typeof(BillingStore)), WolverineGet("/billing")]
            public static Account GetBilling([Entity] Account account) => account;
        }
        [MartenStore(typeof(AccountsStore))]
        public class AccountSaga : Wolverine.Saga
        {
            public AccountStarted Start(StartAccount command) => new(command.AccountId);
        }
        public class NativeSaga : Wolverine.Saga
        {
            public void Start(StartAccount command) { }
        }
        public static class MultiAppendHandler
        {
            public static void Handle(ChangeAccount command, Marten.Ancillary.IDocumentSession<AccountsStore> accounts, Marten.Ancillary.IDocumentSession<BillingStore> billing, [Entity] Account existing)
            {
                accounts.Events.Append(System.Guid.Empty, new AccountChanged(command.Id));
                billing.Events.Append(System.Guid.Empty, new AccountChanged(command.Id));
            }
        }
        public static class DirectHandler
        {
            public static void Handle(ChangeAccount command, Marten.Ancillary.IDocumentSession<BillingStore> session, [Entity] Account existing) => session.Store(new Account(command.Id));
        }
        """;

    AdapterContribution _contribution = null!;
    DotNetProjectCompilation _project = null!;

    void Because()
    {
        _project = ProjectFor(Source);
        _project.Compilation.GetDiagnostics(TestContext.Current.CancellationToken).Where(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error).ShouldBeEmpty();
        _contribution = new CritterStackScreenplayAdapter().Analyze(new DotNetAnalysisContext([_project]), new DotNetAdapterOptions());
    }

    [Fact] void should_emit_one_command_occurrence_for_each_handler_backend() => Artifacts(ArtifactKind.Command, "ChangeAccount").Select(fact => fact.Definition.Key.Scope?.EventModelName).Distinct().ShouldContainOnly(null, "Accounts", "Billing");
    [Fact] void should_keep_the_shared_request_subject_unchanged() => Artifacts(ArtifactKind.Command, "ChangeAccount").Select(fact => fact.Subject).Distinct().Count().ShouldEqual(1);
    [Fact] void should_leave_query_identity_unscoped() => _contribution.Facts.OfType<ArtifactFact>().Where(fact => fact.Definition.Key.Kind == ArtifactKind.Query).All(fact => fact.Definition.Key.Scope is null).ShouldBeTrue();
    [Fact] void should_scope_http_query_read_targets() => QueryEdges(RelationshipKind.Reads).Select(fact => fact.Definition.Key.TargetScope?.EventModelName).ShouldContainOnly("Accounts", "Billing");
    [Fact] void should_scope_http_query_return_targets() => QueryEdges(RelationshipKind.Returns).Select(fact => fact.Definition.Key.TargetScope?.EventModelName).ShouldContainOnly("Accounts", "Billing");
    [Fact] void should_not_scope_http_query_sources() => QueryEdges(RelationshipKind.Returns).All(fact => fact.Definition.Key.SourceScope is null).ShouldBeTrue();
    [Fact] void should_scope_a_proven_marten_backed_saga() => Artifacts(ArtifactKind.Saga, "AccountSaga").Single().Definition.Key.Scope.EventModelName.ShouldEqual("Accounts");
    [Fact] void should_leave_a_native_saga_unscoped() => Artifacts(ArtifactKind.Saga, "NativeSaga").Single().Definition.Key.Scope.ShouldBeNull();
    [Fact] void should_keep_saga_messages_neutral() => _contribution.Facts.OfType<ArtifactFact>().Where(fact => fact.Definition.Key.Kind == ArtifactKind.Message).All(fact => fact.Definition.Key.Scope is null).ShouldBeTrue();
    [Fact] void should_keep_direct_session_storage_separate_from_handler_ownership() => _contribution.Facts.OfType<RelationshipFact>().Single(fact => fact.Definition.Key.Kind == RelationshipKind.Stores && fact.Definition.Key.Source.Value.Contains("DirectHandler", StringComparison.Ordinal)).Definition.Key.SourceScope.EventModelName.ShouldBeNull();
    [Fact] void should_target_the_explicit_ancillary_session_backend() => _contribution.Facts.OfType<RelationshipFact>().Single(fact => fact.Definition.Key.Kind == RelationshipKind.Stores && fact.Definition.Key.Source.Value.Contains("DirectHandler", StringComparison.Ordinal)).Definition.Key.TargetScope.EventModelName.ShouldEqual("Billing");
    [Fact] void should_keep_the_same_appended_event_in_each_receiver_backend() => Artifacts(ArtifactKind.Event, "AccountChanged").Select(fact => fact.Definition.Key.Scope?.EventModelName).ShouldContainOnly("Accounts", "Billing");
    [Fact] void should_scope_each_production_target_independently_from_the_handler_chain() => _contribution.Facts.OfType<RelationshipFact>().Where(fact => fact.Definition.Key.Kind == RelationshipKind.Produces && fact.Definition.Key.Source.Value.Contains("ChangeAccount", StringComparison.Ordinal) && fact.Definition.Key.Target.Value.Contains("AccountChanged", StringComparison.Ordinal)).Select(fact => fact.Definition.Key.TargetScope?.EventModelName).ShouldContainOnly("Accounts", "Billing");
    [Fact] void should_have_unique_scoped_command_fact_ids() => Artifacts(ArtifactKind.Command, "ChangeAccount").Select(fact => fact.Id).Distinct().Count().ShouldEqual(3);

    IEnumerable<ArtifactFact> Artifacts(ArtifactKind kind, string name) => _contribution.Facts.OfType<ArtifactFact>().Where(fact => fact.Definition.Key.Kind == kind && fact.Definition.Name == name);

    IEnumerable<RelationshipFact> QueryEdges(RelationshipKind kind) => _contribution.Facts.OfType<RelationshipFact>().Where(fact => fact.Definition.Key.Kind == kind && fact.Definition.Key.Source.Value.Contains("AccountEndpoints", StringComparison.Ordinal));
}
