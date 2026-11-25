// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Text.Json;
using static Cratis.CritterStack.Screenplay.for_FactParity.given.fact_vectors;
using application = Cratis.CritterStack.Screenplay.given.a_marten_store_scope_application;
using diagnostic_parity = Cratis.CritterStack.Screenplay.given.diagnostic_parity;

namespace Cratis.CritterStack.Screenplay.for_FactParity;

public class when_session_reads_are_emitted_by_independent_adapters : application
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
        public record Dormant(int Id);
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
            [WolverineGet("/load")]
            public static Account Load(IQuerySession session, int id) => session.Load<Account>(id);
            [WolverineGet("/load-async")]
            public static Task<Account> LoadAsync(Marten.Ancillary.IQuerySession<IBilling> session, int id) => session.LoadAsync<Account>(id);
            [WolverineGet("/many")]
            public static IReadOnlyList<Account> Many(IQuerySession session) => session.LoadMany<Account>(1, 2);
            [WolverineGet("/many-async")]
            public static Task<IReadOnlyList<Account>> ManyAsync(Marten.Ancillary.IQuerySession<IBilling> session) => session.LoadManyAsync<Account>(1, 2);
            [WolverineGet("/query")]
            public static Account Query(IQuerySession session) => session.Query<Account>().First();
            [WolverineGet("/dormant")]
            public static Account WithDormant(IQuerySession session)
            {
                Func<Dormant> dormant = () => session.Load<Dormant>(1);
                return session.Load<Account>(1);
            }
        }
        """;

    AdapterContribution _marten = null!;
    GeneratedScreenplayDefinition _expected = null!;
    GeneratedScreenplayDefinition _actual = null!;
    GeneratedScreenplayDefinition _reverse = null!;

    void Because()
    {
        var project = ProjectFor(Source);
        project.Compilation.GetDiagnostics().Where(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error).ShouldBeEmpty();
        var contributions = given.native_query_contributions.Observe(project);
        _marten = contributions.Marten;
        _expected = Generate(contributions.Compatibility);
        _actual = Generate(contributions.Marten, contributions.Wolverine);
        _reverse = Generate(contributions.Wolverine, contributions.Marten);
    }

    [Fact] void should_preserve_every_artifact_field() => Artifacts(_actual).ShouldEqual(Artifacts(_expected));
    [Fact] void should_preserve_every_artifact_field_in_reverse_order() => Artifacts(_reverse).ShouldEqual(Artifacts(_expected));
    [Fact] void should_preserve_every_relationship_field() => Relationships(_actual).ShouldEqual(Relationships(_expected));
    [Fact] void should_preserve_every_relationship_field_in_reverse_order() => Relationships(_reverse).ShouldEqual(Relationships(_expected));
    [Fact] void should_preserve_complete_diagnostics() => Diagnostics(_actual).ShouldEqual(Diagnostics(_expected));
    [Fact] void should_preserve_complete_diagnostics_in_reverse_order() => Diagnostics(_reverse).ShouldEqual(Diagnostics(_expected));
    [Fact] void should_generate_identical_source() => _actual.Source.ShouldEqual(_expected.Source);
    [Fact] void should_generate_identical_source_in_reverse_order() => _reverse.Source.ShouldEqual(_expected.Source);
    [Fact] void should_preserve_verification_status() => _actual.IsSuccess.ShouldEqual(_expected.IsSuccess);
    [Fact] void should_preserve_verification_status_in_reverse_order() => _reverse.IsSuccess.ShouldEqual(_expected.IsSuccess);
    [Fact] void should_observe_all_six_executed_document_reads() => _marten.Facts.OfType<MethodArtifactUsageFact>().Count().ShouldEqual(6);
    [Fact] void should_leave_read_sources_unscoped() => _marten.Facts.OfType<MethodArtifactUsageFact>().ShouldEachConformTo(fact => fact.Definition.SourceScope is null);
    [Fact] void should_keep_both_proved_target_stores() => _marten.Facts.OfType<MethodArtifactUsageFact>().Select(fact => fact.Definition.TargetScope.EventModelName).Distinct().ShouldContainOnly("Primary", "Billing");
    [Fact] void should_not_execute_the_dormant_lambda() => _marten.Facts.OfType<MethodArtifactUsageFact>().ShouldNotContain(fact => fact.Definition.Declaration.Value.Contains("Dormant", StringComparison.Ordinal));

    static string Artifacts(GeneratedScreenplayDefinition result) => JsonSerializer.Serialize(result.Graph.Artifacts.SelectMany(artifact => artifact.Variants).Select(variant => variant.Definition).ToArray());
    static string Relationships(GeneratedScreenplayDefinition result) => JsonSerializer.Serialize(result.Graph.Relationships.SelectMany(relationship => relationship.Definitions).ToArray());
    static string[] Diagnostics(GeneratedScreenplayDefinition result) => diagnostic_parity.Meaning(result.Diagnostics);
}
