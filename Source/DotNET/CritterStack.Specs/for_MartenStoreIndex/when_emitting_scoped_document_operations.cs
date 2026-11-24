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
    GeneratedScreenplayDefinition _result = null!;
    ArtifactKey[] _artifacts = [];

    void Because()
    {
        _contribution = Analyze(ProjectFor(Source));
        _result = new ScreenplayDefinitionGenerator().Generate([_contribution], new() { Domain = "StoreScope" });
        _artifacts = [.. _result.Graph.Artifacts.Select(artifact => artifact.Key)];
    }

    [Fact] void should_keep_two_document_occurrences() => _artifacts.Count(artifact => artifact.Kind == ArtifactKind.Document).ShouldEqual(2);
    [Fact] void should_not_change_document_type_subjects() => _artifacts.Where(artifact => artifact.Kind == ArtifactKind.Document).Select(artifact => artifact.Subject).Distinct().Count().ShouldEqual(1);
    [Fact] void should_not_merge_equal_event_model_names() => _artifacts.Where(artifact => artifact.Kind == ArtifactKind.Document).Select(artifact => artifact.Scope.Store).Distinct().Count().ShouldEqual(2);
    [Fact] void should_keep_payload_concepts_unscoped() => _artifacts.Where(artifact => artifact.Kind == ArtifactKind.Concept).All(artifact => artifact.Scope is null).ShouldBeTrue();
    [Fact] void should_drop_only_the_unowned_operation() => _contribution.Diagnostics.Count(diagnostic => diagnostic.Code == GenerationDiagnosticCodes.UnknownBackendScope && diagnostic.Source is not null).ShouldEqual(1);
    [Fact] void should_resolve_relationship_endpoints_without_orphans() => _contribution.Facts.OfType<RelationshipFact>().All(fact => _artifacts.Contains(new() { Subject = fact.Definition.Key.Source, Kind = ArtifactKind.Handler, Scope = fact.Definition.Key.SourceScope }) && _artifacts.Contains(new() { Subject = fact.Definition.Key.Target, Kind = ArtifactKind.Document, Scope = fact.Definition.Key.TargetScope })).ShouldBeTrue();
    [Fact] void should_use_compilable_operations() => ProjectFor(Source).Compilation.GetDiagnostics(TestContext.Current.CancellationToken).Where(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error).ShouldBeEmpty();
}
