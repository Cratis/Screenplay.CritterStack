// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Cratis.CritterStack.Screenplay.for_MartenStoreIndex;

public class when_owning_results_from_executed_session_reads : given.a_marten_store_scope_application
{
    const string Source =
        """
        using System;
        using System.Collections.Generic;
        using System.Linq;
        using System.Threading.Tasks;
        using Marten;
        using Wolverine.Http;
        namespace StoreScope;
        public interface IBilling : IDocumentStore;
        public record Account(int Id);
        public class AccountById : Marten.Linq.ICompiledQuery<Account, Account>
        {
            public int Id { get; set; }
            public System.Linq.Expressions.Expression<Func<Marten.Linq.IMartenQueryable<Account>, Account>> QueryIs() => query => query.First(account => account.Id == Id);
        }
        public static class Configuration
        {
            public static void Configure(Microsoft.Extensions.DependencyInjection.IServiceCollection services)
            {
                services.AddMarten(options => options.EventModelName = "Primary");
                services.AddMartenStore<IBilling>(options => options.EventModelName = "Billing");
            }
        }
        public static class AccountEndpoints
        {
            [WolverineGet("/billing/load")]
            public static Account BillingLoad(Marten.Ancillary.IQuerySession<IBilling> session, int id) => session.Load<Account>(id);
            [WolverineGet("/billing/load-async")]
            public static Task<Account> BillingLoadAsync(Marten.Ancillary.IQuerySession<IBilling> session, int id) => session.LoadAsync<Account>(id);
            [WolverineGet("/billing/load-many")]
            public static IReadOnlyList<Account> BillingLoadMany(Marten.Ancillary.IQuerySession<IBilling> session) => session.LoadMany<Account>(1, 2);
            [WolverineGet("/billing/load-many-async")]
            public static Task<IReadOnlyList<Account>> BillingLoadManyAsync(Marten.Ancillary.IQuerySession<IBilling> session) => session.LoadManyAsync<Account>(1, 2);
            [WolverineGet("/billing/compiled")]
            public static Task<Account> BillingCompiled(Marten.Ancillary.IQuerySession<IBilling> session, int id) => session.QueryAsync(new AccountById { Id = id });
            [WolverineGet("/billing/aggregate")]
            public static Task<Account> BillingAggregate(Marten.Ancillary.IQuerySession<IBilling> session, Guid id) => session.Events.AggregateStreamAsync<Account>(id);
            [WolverineGet("/billing/fetch")]
            public static async Task<Account> BillingFetch(Marten.Ancillary.IQuerySession<IBilling> session, Guid id)
            {
                await session.Events.FetchStreamAsync(id);
                return new Account(1);
            }
            [WolverineGet("/primary/load-async")]
            public static Task<Account> PrimaryLoadAsync(IQuerySession session, int id) => session.LoadAsync<Account>(id);
            [WolverineGet("/primary/dormant-lambda")]
            public static Account DormantLambda(IQuerySession primary, Marten.Ancillary.IQuerySession<IBilling> billing)
            {
                Func<Account> unused = () => billing.Query<Account>().First();
                return primary.Query<Account>().First();
            }
            [WolverineGet("/primary/dormant-local")]
            public static Account DormantLocal(IQuerySession primary, Marten.Ancillary.IQuerySession<IBilling> billing)
            {
                Account Unused() => billing.Load<Account>(1);
                return primary.Load<Account>(1);
            }
            [WolverineGet("/primary/dormant-compiled")]
            public static Account DormantCompiled(IQuerySession primary, Marten.Ancillary.IQuerySession<IBilling> billing)
            {
                Func<Task<Account>> unused = () => billing.QueryAsync(new AccountById { Id = 1 });
                return primary.Query<Account>().First();
            }
            [WolverineGet("/billing/invoked-local")]
            public static Account InvokedLocal(Marten.Ancillary.IQuerySession<IBilling> billing)
            {
                Account Load() => billing.Load<Account>(1);
                return Load();
            }
            [WolverineGet("/billing/invoked-compiled")]
            public static Task<Account> InvokedCompiled(Marten.Ancillary.IQuerySession<IBilling> billing)
            {
                Task<Account> Load() => billing.QueryAsync(new AccountById { Id = 1 });
                return Load();
            }
            [WolverineGet("/billing/immediate-lambda")]
            public static Account ImmediateLambda(Marten.Ancillary.IQuerySession<IBilling> billing) => ((Func<Account>)(() => billing.Load<Account>(1)))();
            [WolverineGet("/mixed")]
            public static Account MixedReads(IQuerySession primary, Marten.Ancillary.IQuerySession<IBilling> billing)
            {
                primary.Load<Account>(1);
                return billing.Load<Account>(1);
            }
        }
        """;

    AdapterContribution _contribution = null!;

    void Because()
    {
        var project = ProjectFor(Source);
        project.Compilation.GetDiagnostics(TestContext.Current.CancellationToken).Where(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error).ShouldBeEmpty();
        _contribution = new CritterStackScreenplayAdapter().Analyze(new DotNetAnalysisContext([project]), new DotNetAdapterOptions());
    }

    [Fact] void should_return_load_async_results_from_billing() => Returns("BillingLoadAsync").Definition.Key.TargetScope.EventModelName.ShouldEqual("Billing");
    [Fact] void should_read_load_async_documents_from_billing() => Reads("BillingLoadAsync").Definition.Key.TargetScope.EventModelName.ShouldEqual("Billing");
    [Fact] void should_return_compiled_query_async_results_from_billing() => Returns("BillingCompiled").Definition.Key.TargetScope.EventModelName.ShouldEqual("Billing");
    [Fact] void should_read_compiled_query_async_documents_from_billing() => Reads("BillingCompiled").Definition.Key.TargetScope.EventModelName.ShouldEqual("Billing");
    [Fact] void should_keep_compiled_read_fact_identity() => Reads("BillingCompiled").Id.Value.StartsWith("wolverine:reads:compiled:", StringComparison.Ordinal).ShouldBeTrue();
    [Fact] void should_return_synchronous_load_results_from_billing() => Returns("BillingLoad").Definition.Key.TargetScope.EventModelName.ShouldEqual("Billing");
    [Fact] void should_read_synchronous_load_documents_from_billing() => Reads("BillingLoad").Definition.Key.TargetScope.EventModelName.ShouldEqual("Billing");
    [Fact] void should_return_load_many_results_from_billing() => Returns("BillingLoadMany").Definition.Key.TargetScope.EventModelName.ShouldEqual("Billing");
    [Fact] void should_read_load_many_documents_from_billing() => Reads("BillingLoadMany").Definition.Key.TargetScope.EventModelName.ShouldEqual("Billing");
    [Fact] void should_return_async_load_many_results_from_billing() => Returns("BillingLoadManyAsync").Definition.Key.TargetScope.EventModelName.ShouldEqual("Billing");
    [Fact] void should_read_async_load_many_documents_from_billing() => Reads("BillingLoadManyAsync").Definition.Key.TargetScope.EventModelName.ShouldEqual("Billing");
    [Fact] void should_return_aggregated_event_state_from_billing() => Returns("BillingAggregate").Definition.Key.TargetScope.EventModelName.ShouldEqual("Billing");
    [Fact] void should_read_aggregated_event_state_from_billing() => Reads("BillingAggregate").Definition.Key.TargetScope.EventModelName.ShouldEqual("Billing");
    [Fact] void should_use_event_fetch_ownership_even_without_a_document_type() => Returns("BillingFetch").Definition.Key.TargetScope.EventModelName.ShouldEqual("Billing");
    [Fact] void should_keep_default_load_async_results_in_primary() => Returns("PrimaryLoadAsync").Definition.Key.TargetScope.EventModelName.ShouldEqual("Primary");
    [Fact] void should_keep_default_load_async_reads_in_primary() => Reads("PrimaryLoadAsync").Definition.Key.TargetScope.EventModelName.ShouldEqual("Primary");
    [Fact] void should_ignore_an_uninvoked_billing_lambda_for_result_ownership() => Returns("DormantLambda").Definition.Key.TargetScope.EventModelName.ShouldEqual("Primary");
    [Fact] void should_ignore_an_uninvoked_billing_lambda_for_reads() => Reads("DormantLambda").Definition.Key.TargetScope.EventModelName.ShouldEqual("Primary");
    [Fact] void should_ignore_an_uninvoked_billing_local_function_for_results() => Returns("DormantLocal").Definition.Key.TargetScope.EventModelName.ShouldEqual("Primary");
    [Fact] void should_ignore_an_uninvoked_billing_local_function_for_reads() => Reads("DormantLocal").Definition.Key.TargetScope.EventModelName.ShouldEqual("Primary");
    [Fact] void should_ignore_an_uninvoked_compiled_query_for_results() => Returns("DormantCompiled").Definition.Key.TargetScope.EventModelName.ShouldEqual("Primary");
    [Fact] void should_ignore_an_uninvoked_compiled_query_for_reads() => Reads("DormantCompiled").Definition.Key.TargetScope.EventModelName.ShouldEqual("Primary");
    [Fact] void should_include_a_proven_local_function_read_in_results() => Returns("InvokedLocal").Definition.Key.TargetScope.EventModelName.ShouldEqual("Billing");
    [Fact] void should_include_a_proven_local_function_read_in_reads() => Reads("InvokedLocal").Definition.Key.TargetScope.EventModelName.ShouldEqual("Billing");
    [Fact] void should_include_a_proven_compiled_query_local_function_in_results() => Returns("InvokedCompiled").Definition.Key.TargetScope.EventModelName.ShouldEqual("Billing");
    [Fact] void should_include_a_proven_compiled_query_local_function_in_reads() => Reads("InvokedCompiled").Definition.Key.TargetScope.EventModelName.ShouldEqual("Billing");
    [Fact] void should_include_an_immediately_invoked_lambda_in_results() => Returns("ImmediateLambda").Definition.Key.TargetScope.EventModelName.ShouldEqual("Billing");
    [Fact] void should_include_an_immediately_invoked_lambda_in_reads() => Reads("ImmediateLambda").Definition.Key.TargetScope.EventModelName.ShouldEqual("Billing");
    [Fact] void should_not_return_a_guessed_backend_for_mixed_invoked_reads() => Edges("MixedReads", RelationshipKind.Returns).ShouldBeEmpty();
    [Fact] void should_report_conflicting_mixed_invoked_reads() => _contribution.Diagnostics.Any(diagnostic => diagnostic.Code == GenerationDiagnosticCodes.AmbiguousBackendScopeOwnership && diagnostic.Subject.Value.Contains("MixedReads", StringComparison.Ordinal)).ShouldBeTrue();
    [Fact] void should_not_report_false_ownership_conflicts_for_dormant_scopes() => _contribution.Diagnostics.Where(diagnostic => diagnostic.Code == GenerationDiagnosticCodes.AmbiguousBackendScopeOwnership && diagnostic.Subject.Value.Contains("Dormant", StringComparison.Ordinal)).ShouldBeEmpty();
    [Fact] void should_leave_query_sources_unscoped() => _contribution.Facts.OfType<RelationshipFact>().Where(fact => fact.Id.Value.StartsWith("wolverine:reads:", StringComparison.Ordinal) || fact.Definition.Key.Kind == RelationshipKind.Returns).All(fact => fact.Definition.Key.SourceScope is null).ShouldBeTrue();

    RelationshipFact Returns(string method) => Edges(method, RelationshipKind.Returns).Single();

    RelationshipFact Reads(string method) => Edges(method, RelationshipKind.Reads).Single();

    IEnumerable<RelationshipFact> Edges(string method, RelationshipKind kind) => _contribution.Facts.OfType<RelationshipFact>().Where(fact => fact.Definition.Key.Kind == kind && Uri.UnescapeDataString(fact.Definition.Key.Source.Value).Contains($"{method}(", StringComparison.Ordinal) && fact.Id.Value.StartsWith("wolverine:", StringComparison.Ordinal));
}
