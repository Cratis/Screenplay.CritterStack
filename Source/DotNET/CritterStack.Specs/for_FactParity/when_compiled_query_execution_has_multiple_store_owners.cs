// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Text.Json;
using static Cratis.CritterStack.Screenplay.for_FactParity.given.fact_vectors;
using application = Cratis.CritterStack.Screenplay.given.a_marten_store_scope_application;
using diagnostic_parity = Cratis.CritterStack.Screenplay.given.diagnostic_parity;

namespace Cratis.CritterStack.Screenplay.for_FactParity;

public class when_compiled_query_execution_has_multiple_store_owners : application
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
            public static void Configure(Microsoft.Extensions.DependencyInjection.IServiceCollection services)
            {
                services.AddMarten(options => options.EventModelName = "Primary");
                services.AddMartenStore<IBilling>(options => options.EventModelName = "Billing");
            }
        }
        public static class AccountEndpoints
        {
            [WolverineGet("/primary")]
            public static Task<Account> Primary(IQuerySession session) => session.QueryAsync(new AccountById { Id = 1 });
            [WolverineGet("/billing")]
            public static Task<Account> Billing(Marten.Ancillary.IQuerySession<IBilling> session) => session.QueryAsync(new AccountById { Id = 1 });
            [WolverineGet("/mixed")]
            public static Task<Account> Mixed(IQuerySession primary, Marten.Ancillary.IQuerySession<IBilling> billing)
            {
                _ = primary.QueryAsync(new AccountById { Id = 1 });
                return billing.QueryAsync(new AccountById { Id = 1 });
            }
            [WolverineGet("/dormant")]
            public static Task<Account> Dormant(IQuerySession primary, Marten.Ancillary.IQuerySession<IBilling> billing)
            {
                System.Func<Task<Account>> unused = () => billing.QueryAsync(new AccountById { Id = 1 });
                return primary.QueryAsync(new AccountById { Id = 1 });
            }
        }
        """;

    DotNetProjectCompilation _project = null!;
    AdapterContribution _marten = null!;
    AdapterContribution _wolverine = null!;
    GeneratedScreenplayDefinition _expected = null!;
    GeneratedScreenplayDefinition _actual = null!;
    GeneratedScreenplayDefinition _reverse = null!;

    void Because()
    {
        _project = ProjectFor(Source);
        _project.Compilation.GetDiagnostics().Where(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error).ShouldBeEmpty();
        var contributions = given.native_query_contributions.Observe(_project);
        _marten = contributions.Marten;
        _wolverine = contributions.Wolverine;
        _expected = Generate(contributions.Compatibility);
        _actual = Generate(_marten, _wolverine);
        _reverse = Generate(_wolverine, _marten);
    }

    [Fact] void should_preserve_every_artifact_field() => Artifacts(_actual).ShouldEqual(Artifacts(_expected));
    [Fact] void should_preserve_every_artifact_field_in_reverse_order() => Artifacts(_reverse).ShouldEqual(Artifacts(_expected));
    [Fact] void should_preserve_every_relationship_except_the_two_accepted_proven_reads() => AssertRelationships(_actual);
    [Fact] void should_preserve_every_relationship_except_the_two_accepted_proven_reads_in_reverse_order() => AssertRelationships(_reverse);
    [Fact] void should_preserve_every_diagnostic_after_aligning_producer_identities() => AssertDiagnostics(_actual);
    [Fact] void should_preserve_every_diagnostic_after_aligning_producer_identities_in_reverse_order() => AssertDiagnostics(_reverse);
    [Fact] void should_generate_identical_source() => _actual.Source.ShouldEqual(_expected.Source);
    [Fact] void should_generate_identical_source_in_reverse_order() => _reverse.Source.ShouldEqual(_expected.Source);
    [Fact] void should_preserve_verification_status() => _actual.IsSuccess.ShouldEqual(_expected.IsSuccess);
    [Fact] void should_preserve_verification_status_in_reverse_order() => _reverse.IsSuccess.ShouldEqual(_expected.IsSuccess);
    [Fact] void should_keep_distinct_scoped_library_inputs() => _marten.Facts.OfType<DeclarationInputMemberTypeUseFact>().Select(fact => fact.Definition.Member.Scope?.EventModelName).Distinct().ShouldContainOnly("Primary", "Billing");
    [Fact] void should_keep_query_input_transfer_neutral() => _wolverine.Facts.OfType<EntryPointInputTransferFact>().ShouldEachConformTo(fact => fact.Definition.Artifact.Scope is null);
    [Fact] void should_leave_read_sources_neutral() => _actual.Graph.Relationships.Where(relationship => relationship.Key.Kind == RelationshipKind.Reads).ShouldEachConformTo(relationship => relationship.Key.SourceScope is null);
    [Fact] void should_keep_one_dead_flow_warning() => _actual.Diagnostics.Count(diagnostic => diagnostic.Code == MartenDiagnosticCodes.CompiledQueryFlowUnresolved).ShouldEqual(1);

    static string Artifacts(GeneratedScreenplayDefinition result) => JsonSerializer.Serialize(result.Graph.Artifacts.SelectMany(artifact => artifact.Variants).Select(variant => variant.Definition).ToArray());

    static bool IsScopeWarning(GenerationDiagnostic diagnostic) => diagnostic.Code == GenerationDiagnosticCodes.BackendScopeNotSupported;

    static int[] Coordinates(SourceRange source) => [source.StartLine, source.StartColumn, source.EndLine, source.EndColumn];

    static string ScopeMeaning(GenerationDiagnostic diagnostic) => JsonSerializer.Serialize(new
    {
        diagnostic.Code,
        diagnostic.Severity,
        diagnostic.Outcome,
        diagnostic.Message,
        diagnostic.Subject,
        Source = new { diagnostic.Source.Path, diagnostic.Source.FileIdentity }
    });

    static string RelationshipTranscript(IEnumerable<RelationshipDefinition> relationships) =>
        JsonSerializer.Serialize(relationships.Select(relationship => JsonSerializer.Serialize(relationship)).Order(StringComparer.Ordinal).ToArray());

    // Accepted bounded deltas: .ai-work/44/decision-gap11-12.md (2026-11-24).
    void AssertRelationships(GeneratedScreenplayDefinition result)
    {
        var additional = new[] { "Primary", "Billing" }.Select(name => new RelationshipDefinition
        {
            Key = new()
            {
                Kind = RelationshipKind.Reads,
                Source = Query("Mixed").Key.Subject,
                Target = Read(name).Key.Target,
                TargetScope = Read(name).Key.TargetScope
            }
        });
        var expected = _expected.Graph.Relationships.SelectMany(relationship => relationship.Definitions).Concat(additional);
        RelationshipTranscript(result.Graph.Relationships.SelectMany(relationship => relationship.Definitions))
            .ShouldEqual(RelationshipTranscript(expected));
        result.Graph.Relationships.Where(relationship => relationship.Key.Source == Query("Mixed").Key.Subject)
            .ShouldEachConformTo(relationship => relationship.Key.Kind != RelationshipKind.Returns);
        result.Graph.Artifacts.Single(artifact => artifact.Key == Query("Mixed").Key).Variants.Single().Definition.Properties
            .Select(property => property.Name).ShouldContain("id");
        result.Graph.Relationships.Where(relationship =>
            relationship.Key.Source == Query("Dormant").Key.Subject &&
            relationship.Key.Kind == RelationshipKind.Reads &&
            relationship.Key.TargetScope?.EventModelName == "Billing").ShouldBeEmpty();
    }

    void AssertDiagnostics(GeneratedScreenplayDefinition result)
    {
        result.Diagnostics.Count.ShouldEqual(14);
        _expected.Diagnostics.Count.ShouldEqual(14);
        var expectedScope = _expected.Diagnostics.Where(IsScopeWarning).ToArray();
        var actualScope = result.Diagnostics.Where(IsScopeWarning).ToArray();
        expectedScope.Length.ShouldEqual(2);
        actualScope.Length.ShouldEqual(2);
        diagnostic_parity.Meaning(result.Diagnostics.Where(diagnostic => !IsScopeWarning(diagnostic)))
            .ShouldEqual(diagnostic_parity.Meaning(_expected.Diagnostics.Where(diagnostic => !IsScopeWarning(diagnostic))));
        foreach (var name in new[] { "Primary", "Billing" })
        {
            var store = Read(name).Key.TargetScope.Store;
            var expected = expectedScope.Single(diagnostic => diagnostic.Subject == store);
            var actual = actualScope.Single(diagnostic => diagnostic.Subject == store);
            ScopeMeaning(actual).ShouldEqual(ScopeMeaning(expected));
            Coordinates(expected.Source).ShouldEqual(name == "Primary" ? [23, 5, 24, 114] : [25, 5, 26, 141]);

            // Aligning the real producer removes gap12's diagnostic delta; invocation evidence remains exact.
            Coordinates(actual.Source).ShouldEqual(Coordinates(expected.Source));
            var method = DotNetMethodIdentity.SubjectFor(_project, Method(name));
            var usage = _marten.Facts.OfType<MethodArtifactUsageFact>().Single(fact => fact.Definition.Method == method);
            Coordinates(usage.Evidence.Source).ShouldEqual(name == "Primary" ? [24, 67, 24, 113] : [26, 94, 26, 140]);
            Coordinates(usage.Evidence.Source).ShouldEqual(InvocationCoordinates(name));
        }

        diagnostic_parity.Meaning(_actual.Diagnostics).ShouldEqual(diagnostic_parity.Meaning(_reverse.Diagnostics));
    }

    ArtifactDefinition Query(string name) => _expected.Graph.Artifacts.SelectMany(artifact => artifact.Variants)
        .Select(variant => variant.Definition).Single(artifact => artifact.Key.Kind == ArtifactKind.Query && artifact.Name == name);

    RelationshipDefinition Read(string name) => _expected.Graph.Relationships.SelectMany(relationship => relationship.Definitions)
        .Single(relationship => relationship.Key.Kind == RelationshipKind.Reads && relationship.Key.Source == Query(name).Key.Subject);

    IMethodSymbol Method(string name) => _project.Compilation.GetTypeByMetadataName("StoreScope.AccountEndpoints").GetMembers(name).OfType<IMethodSymbol>().Single();

    int[] InvocationCoordinates(string name)
    {
        var method = Method(name);
        var invocation = MartenCompiledQueryDiscovery.Observe(method, DotNetMethodIdentity.SubjectFor(_project, method), _project, _marten.Adapter).Links.Single().Invocation;
        var range = invocation.GetLocation().GetLineSpan();
        return [range.StartLinePosition.Line + 1, range.StartLinePosition.Character + 1, range.EndLinePosition.Line + 1, range.EndLinePosition.Character + 1];
    }
}
