// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Cratis.CritterStack.Screenplay.for_MartenStoreIndex;

public class when_emitting_scoped_projection_occurrences : given.a_marten_store_scope_application
{
    const string Source =
        """
        using Marten;
        namespace StoreScope;
        public interface IAccounts : IDocumentStore;
        public interface IBilling : IDocumentStore;
        public record Opened(string Name);
        public class Account
        {
            public string Name { get; set; } = "";
            public static Account Create(Opened opened) => new Account { Name = opened.Name };
        }
        public static class Configuration
        {
            public static void Configure(Microsoft.Extensions.DependencyInjection.IServiceCollection services)
            {
                services.AddMartenStore<IAccounts>(options => { options.EventModelName = "Shared"; options.Projections.Snapshot<Account>(); });
                services.AddMartenStore<IBilling>(options => { options.EventModelName = "Shared"; options.Projections.Snapshot<Account>(); });
            }
        }
        """;

    AdapterContribution _contribution = null!;
    GeneratedScreenplayDefinition _generated = null!;
    ArtifactFact[] _artifacts = [];
    RelationshipFact[] _relationships = [];

    void Because()
    {
        _contribution = Analyze(ProjectFor(Source));
        _generated = new CritterStackScreenplayGenerator().Generate([ProjectFor(Source)], new() { Domain = "StoreScopes" });
        _artifacts = [.. _contribution.Facts.OfType<ArtifactFact>()];
        _relationships = [.. _contribution.Facts.OfType<RelationshipFact>()];
    }

    [Fact] void should_lower_print_and_compile_scoped_occurrences() => _generated.IsSuccess.ShouldBeTrue();
    [Fact] void should_emit_two_store_declarations() => _contribution.Facts.OfType<BackendScopeFact>().Count().ShouldEqual(2);
    [Fact] void should_preserve_the_same_type_subject_across_occurrences() => _artifacts.Where(fact => fact.Definition.Key.Kind == ArtifactKind.Aggregate).Select(fact => fact.Subject).Distinct().Count().ShouldEqual(1);
    [Fact] void should_not_merge_equal_names_in_distinct_stores() => _artifacts.Where(fact => fact.Definition.Key.Kind == ArtifactKind.Aggregate).Select(fact => fact.Definition.Key.Scope.Store).Distinct().Count().ShouldEqual(2);
    [Fact] void should_scope_every_projection_artifact() => _artifacts.All(fact => fact.Definition.Key.Scope is not null).ShouldBeTrue();
    [Fact] void should_keep_fact_ids_unique() => _contribution.Facts.Select(fact => fact.Id).Distinct().Count().ShouldEqual(_contribution.Facts.Count);
    [Fact] void should_resolve_both_relationship_endpoints() => _relationships.All(fact => _artifacts.Any(artifact => artifact.Subject == fact.Definition.Key.Source && artifact.Definition.Key.Scope == fact.Definition.Key.SourceScope) && _artifacts.Any(artifact => artifact.Subject == fact.Definition.Key.Target && artifact.Definition.Key.Scope == fact.Definition.Key.TargetScope)).ShouldBeTrue();
    [Fact] void should_not_report_scope_ownership_diagnostics() => _contribution.Diagnostics.Any(diagnostic => diagnostic.Code == GenerationDiagnosticCodes.UnknownBackendScope || diagnostic.Code == GenerationDiagnosticCodes.AmbiguousBackendScopeOwnership).ShouldBeFalse();
    [Fact] void should_use_compilable_configuration() => ProjectFor(Source).Compilation.GetDiagnostics(TestContext.Current.CancellationToken).Where(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error).ShouldBeEmpty();
}
