// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Text.Json;
using static Cratis.CritterStack.Screenplay.for_FactParity.given.fact_vectors;
using application = Cratis.CritterStack.Screenplay.given.a_marten_store_scope_application;
using diagnostic_parity = Cratis.CritterStack.Screenplay.given.diagnostic_parity;

namespace Cratis.CritterStack.Screenplay.for_FactParity;

public class when_compiled_query_ownership_is_unknown : application
{
    const string Source =
        """
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
            public System.Linq.Expressions.Expression<System.Func<Marten.Linq.IMartenQueryable<Account>, Account>> QueryIs() => query => query.First(account => account.Id == Id);
        }
        public static class Configuration
        {
            public static void Configure(Microsoft.Extensions.DependencyInjection.IServiceCollection services) =>
                services.AddMarten(options => options.EventModelName = "Primary");
        }
        public static class AccountEndpoints
        {
            [WolverineGet("/unknown")]
            [Wolverine.Marten.MartenStore(typeof(IBilling))]
            public static Task<Account> Unknown(IQuerySession session) => session.QueryAsync(new AccountById { Id = 1 });
        }
        """;

    GeneratedScreenplayDefinition _expected = null!;
    GeneratedScreenplayDefinition _actual = null!;
    GeneratedScreenplayDefinition _reverse = null!;

    void Because()
    {
        var contributions = given.native_query_contributions.Observe(ProjectFor(Source));
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
    [Fact] void should_not_invent_reads_or_returns() => _actual.Graph.Relationships.ShouldBeEmpty();
    [Fact] void should_retain_the_unknown_ownership_error() => _actual.Diagnostics.ShouldContain(diagnostic => diagnostic.Code == GenerationDiagnosticCodes.UnknownBackendScope);

    static string Artifacts(GeneratedScreenplayDefinition result) => JsonSerializer.Serialize(result.Graph.Artifacts.SelectMany(artifact => artifact.Variants).Select(variant => variant.Definition).ToArray());
    static string Relationships(GeneratedScreenplayDefinition result) => JsonSerializer.Serialize(result.Graph.Relationships.SelectMany(relationship => relationship.Definitions).ToArray());
    static string[] Diagnostics(GeneratedScreenplayDefinition result) => diagnostic_parity.Meaning(result.Diagnostics);
}
