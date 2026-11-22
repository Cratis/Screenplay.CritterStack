// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Cratis.CritterStack.Screenplay.for_MartenStoreIndex;

public class when_owning_http_query_results_by_session : given.a_marten_store_scope_application
{
    const string Source =
        """
        using System.Linq;
        using Marten;
        using Wolverine.Http;
        namespace StoreScope;
        public interface IBilling : IDocumentStore;
        public record Account(int Id);
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
            [WolverineGet("/billing/parameter")]
            public static Account ByParameter(Marten.Ancillary.IQuerySession<IBilling> session) => session.Query<Account>().First();
            [WolverineGet("/primary")]
            public static Account ByPrimary(IQuerySession session) => session.Query<Account>().First();
            [WolverineGet("/ambiguous")]
            public static Account ByAmbiguous(IQuerySession primary, Marten.Ancillary.IQuerySession<IBilling> billing, bool useBilling) => useBilling ? billing.Query<Account>().First() : primary.Query<Account>().First();
        }
        public class BillingReceiverEndpoints(Marten.Ancillary.IQuerySession<IBilling> session)
        {
            Marten.Ancillary.IQuerySession<IBilling> Session { get; } = session;
            [WolverineGet("/billing/receiver")]
            public Account ByReceiver() => Session.Query<Account>().First();
        }
        """;

    AdapterContribution _contribution = null!;

    void Because()
    {
        var project = ProjectFor(Source);
        project.Compilation.GetDiagnostics(TestContext.Current.CancellationToken).Where(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error).ShouldBeEmpty();
        _contribution = new CritterStackScreenplayAdapter().Analyze(new DotNetAnalysisContext([project]), new DotNetAdapterOptions());
    }

    [Fact] void should_return_the_parameter_session_backend() => Edges("ByParameter", RelationshipKind.Returns).Single().Definition.Key.TargetScope.EventModelName.ShouldEqual("Billing");
    [Fact] void should_read_the_parameter_session_backend() => Edges("ByParameter", RelationshipKind.Reads).Single().Definition.Key.TargetScope.EventModelName.ShouldEqual("Billing");
    [Fact] void should_return_the_receiver_session_backend() => Edges("ByReceiver", RelationshipKind.Returns).Single().Definition.Key.TargetScope.EventModelName.ShouldEqual("Billing");
    [Fact] void should_read_the_receiver_session_backend() => Edges("ByReceiver", RelationshipKind.Reads).Single().Definition.Key.TargetScope.EventModelName.ShouldEqual("Billing");
    [Fact] void should_keep_the_default_session_result_in_primary() => Edges("ByPrimary", RelationshipKind.Returns).Single().Definition.Key.TargetScope.EventModelName.ShouldEqual("Primary");
    [Fact] void should_keep_the_default_session_read_in_primary() => Edges("ByPrimary", RelationshipKind.Reads).Single().Definition.Key.TargetScope.EventModelName.ShouldEqual("Primary");
    [Fact] void should_not_invent_a_primary_result_for_mixed_session_backends() => Edges("ByAmbiguous", RelationshipKind.Returns).ShouldBeEmpty();
    [Fact] void should_report_mixed_result_ownership() => _contribution.Diagnostics.Any(diagnostic => diagnostic.Code == GenerationDiagnosticCodes.AmbiguousBackendScopeOwnership && diagnostic.Subject.Value.Contains("ByAmbiguous", StringComparison.Ordinal)).ShouldBeTrue();
    [Fact] void should_leave_query_identity_neutral() => _contribution.Facts.OfType<ArtifactFact>().Where(fact => fact.Definition.Key.Kind == ArtifactKind.Query).All(fact => fact.Definition.Key.Scope is null).ShouldBeTrue();
    [Fact] void should_leave_query_relationship_sources_neutral() => _contribution.Facts.OfType<RelationshipFact>().Where(fact => fact.Id.Value.StartsWith("wolverine:reads:session:", StringComparison.Ordinal) || fact.Definition.Key.Kind == RelationshipKind.Returns).All(fact => fact.Definition.Key.SourceScope is null).ShouldBeTrue();
    [Fact] void should_emit_only_the_two_proven_result_occurrences() => _contribution.Facts.OfType<ArtifactFact>().Where(fact => fact.Definition.Key.Kind == ArtifactKind.ReadModel && fact.Definition.Name == "Account").Select(fact => fact.Definition.Key.Scope.EventModelName).Distinct().ShouldContainOnly("Primary", "Billing");

    IEnumerable<RelationshipFact> Edges(string method, RelationshipKind kind) => _contribution.Facts.OfType<RelationshipFact>().Where(fact => fact.Definition.Key.Kind == kind && fact.Definition.Key.Source.Value.Contains(method, StringComparison.Ordinal) && fact.Id.Value.StartsWith("wolverine:", StringComparison.Ordinal));
}
