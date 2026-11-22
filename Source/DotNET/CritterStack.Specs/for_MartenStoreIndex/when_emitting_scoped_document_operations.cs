// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Cratis.CritterStack.Screenplay.for_MartenStoreIndex;

public class when_emitting_scoped_document_operations : given.a_marten_store_scope_application
{
    const string Source =
        """
        using Marten;
        namespace StoreScope;
        public interface IAccounts : IDocumentStore;
        public interface IBilling : IDocumentStore;
        public record Address(string Street);
        public record Account(System.Guid Id, Address Address);
        public class Operations
        {
            [Wolverine.Persistence.Storage(typeof(IAccounts))]
            public void Accounts(IDocumentSession session, Account account) => session.Store(account);
            [Wolverine.Marten.MartenStore(typeof(IBilling))]
            public void Billing(IDocumentSession session, Account account) => session.Store(account);
            public void Unowned(IDocumentSession session, Account account) => session.Store(account);
        }
        public static class Configuration
        {
            public static void Configure(Microsoft.Extensions.DependencyInjection.IServiceCollection services)
            {
                services.AddMartenStore<IAccounts>(options => options.EventModelName = "Shared");
                services.AddMartenStore<IBilling>(options => options.EventModelName = "Shared");
            }
        }
        """;

    AdapterContribution _contribution = null!;
    ArtifactFact[] _artifacts = [];

    void Because()
    {
        _contribution = Analyze(ProjectFor(Source));
        _artifacts = [.. _contribution.Facts.OfType<ArtifactFact>()];
    }

    [Fact] void should_keep_two_document_occurrences() => _artifacts.Count(fact => fact.Definition.Key.Kind == ArtifactKind.Document).ShouldEqual(2);
    [Fact] void should_not_change_document_type_subjects() => _artifacts.Where(fact => fact.Definition.Key.Kind == ArtifactKind.Document).Select(fact => fact.Subject).Distinct().Count().ShouldEqual(1);
    [Fact] void should_not_merge_equal_event_model_names() => _artifacts.Where(fact => fact.Definition.Key.Kind == ArtifactKind.Document).Select(fact => fact.Definition.Key.Scope.Store).Distinct().Count().ShouldEqual(2);
    [Fact] void should_keep_payload_concepts_unscoped() => _artifacts.Where(fact => fact.Definition.Key.Kind == ArtifactKind.Concept).All(fact => fact.Definition.Key.Scope is null).ShouldBeTrue();
    [Fact] void should_drop_only_the_unowned_operation() => _contribution.Diagnostics.Count(diagnostic => diagnostic.Code == GenerationDiagnosticCodes.UnknownBackendScope && diagnostic.Source is not null).ShouldEqual(1);
    [Fact] void should_resolve_relationship_endpoints_without_orphans() => _contribution.Facts.OfType<RelationshipFact>().All(fact => _artifacts.Any(artifact => artifact.Subject == fact.Definition.Key.Source && artifact.Definition.Key.Scope == fact.Definition.Key.SourceScope) && _artifacts.Any(artifact => artifact.Subject == fact.Definition.Key.Target && artifact.Definition.Key.Scope == fact.Definition.Key.TargetScope)).ShouldBeTrue();
    [Fact] void should_use_compilable_operations() => ProjectFor(Source).Compilation.GetDiagnostics(TestContext.Current.CancellationToken).Where(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error).ShouldBeEmpty();
}
