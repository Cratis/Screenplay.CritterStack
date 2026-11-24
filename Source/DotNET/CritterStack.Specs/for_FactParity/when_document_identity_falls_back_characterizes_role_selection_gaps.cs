// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using static Cratis.CritterStack.Screenplay.for_FactParity.given.fact_vectors;

namespace Cratis.CritterStack.Screenplay.for_FactParity;

/// <summary>
/// Static attribute/convention precedence belongs to source interpretation. A method-supported override,
/// including an unresolved override, needs selection after eligibility, which SG 0.34.0 cannot express.
/// </summary>
public class when_document_identity_falls_back_characterizes_role_selection_gaps : Specification
{
    IdentityVector _conventional = null!;
    IdentityVector _attributed = null!;
    IdentityVector _configured = null!;
    IdentityVector _unresolved = null!;
    IdentityVector _failureOnly = null!;

    void Because()
    {
        _conventional = Observe(false, null);
        _attributed = Observe(true, null);
        _configured = Observe(true, "document.Last");
        _unresolved = Observe(false, "document.First + document.Last");
        _failureOnly = Observe(true, "document.Last", true);
    }

    [Fact] void should_preserve_conventional_identity() => _conventional.ShouldMatch("id", "id");
    [Fact] void should_preserve_attribute_precedence_over_convention() => _attributed.ShouldMatch("first", "first");
    [Fact] void should_characterize_the_explicit_override_union() => _configured.ShouldMatch("last", "first,last");
    [Fact] void should_characterize_the_unresolved_override_suppression_gap() => _unresolved.ShouldMatch(string.Empty, "id");
    [Fact] void should_restore_the_attribute_after_failure_classification() => _failureOnly.ShouldMatch("first", "first");
    [Fact] void should_verify_all_atomic_candidates() => Vectors().All(_ => _.Forward.IsSuccess && _.Reverse.IsSuccess).ShouldBeTrue();
    [Fact] void should_not_depend_on_contribution_order() => Vectors().All(_ => Identifiers(Definition(_.Forward, _.Key)) == Identifiers(Definition(_.Reverse, _.Key))).ShouldBeTrue();
    [Fact] void should_observe_the_unresolved_configuration_diagnostic() => _unresolved.Compatibility.Diagnostics.Count(_ => _.Code == MartenDiagnosticCodes.DocumentIdentityUnresolved).ShouldEqual(1);

    IdentityVector[] Vectors() => [_conventional, _attributed, _configured, _unresolved, _failureOnly];

    static IdentityVector Observe(bool attributed, string? expression, bool failureOnly = false)
    {
        var project = Project(Source(attributed, expression, failureOnly));
        var document = Type(project, "FactParity.Document");
        var key = Key(project, document, ArtifactKind.Document);
        var facts = new List<GenerationFact>(Surface(project, document, key, MartenAdapter))
        {
            Role(key, attributed ? "first" : "id", Evidence(project, document, MartenAdapter, EvidenceStrength.Exact))
        };
        var methodType = Type(project, failureOnly ? "FactParity.SaveDocumentHandler" : "FactParity.Configuration");
        var method = methodType.GetMembers(failureOnly ? "OnException" : "Configure").OfType<IMethodSymbol>().Single();
        if (expression == "document.Last")
        {
            var role = Role(key, "last", Evidence(project, method, MartenAdapter, EvidenceStrength.Configured));
            facts.Add(role);
            facts.Add(CritterStackMethodFacts.Support(role, [CritterStackMethodFacts.Observe(project, method)]));
        }
        var contribution = Contribution(MartenAdapter, facts);
        var peer = failureOnly ? Failure(project, method) : Contribution(WolverineAdapter, []);
        var compatibility = new CritterStackScreenplayAdapter().Analyze(new([project]), new());
        return new(key, compatibility, Generate(contribution, peer), Generate(peer, contribution));
    }

    static string Source(bool attributed, string? expression, bool failureOnly)
    {
        var configuration = expression is null ? string.Empty : $"options.Schema.For<Document>().Identity(document => {expression});";
        return Framework + $$"""
            namespace FactParity
            {
                public sealed class Document
                {
                    public int Id { get; set; }
                    {{(attributed ? "[JasperFx.Identity]" : string.Empty)}}
                    public int First { get; set; }
                    public int Last { get; set; }
                }
                public static class Configuration
                {
                    public static void Configure(Marten.StoreOptions options) { {{(failureOnly ? string.Empty : configuration)}} }
                }
                public sealed record SaveDocument;
                public static class SaveDocumentHandler
                {
                    public static void Handle(SaveDocument message, Marten.IDocumentSession session) => session.Store(new Document());
                    public static void OnException(SaveDocument message, System.Exception exception, Marten.StoreOptions options) { {{(failureOnly ? configuration : string.Empty)}} }
                }
            }
            """;
    }

    const string Framework = """
        namespace Wolverine { public sealed class WolverineOptions; }
        namespace JasperFx { public sealed class IdentityAttribute : System.Attribute; }
        namespace Marten
        {
            public interface IDocumentStore;
            public interface IDocumentSession { void Store<T>(T document); }
            public sealed class StoreOptions { public MartenRegistry Schema { get; } = new(); }
            public sealed class MartenRegistry
            {
                public DocumentMappingExpression<T> For<T>() => new();
                public sealed class DocumentMappingExpression<T>
                {
                    public DocumentMappingExpression<T> Identity(System.Linq.Expressions.Expression<System.Func<T, object>> member) => this;
                }
            }
        }
        """;

    sealed record IdentityVector(ArtifactKey Key, AdapterContribution Compatibility, GeneratedScreenplayDefinition Forward, GeneratedScreenplayDefinition Reverse)
    {
        internal void ShouldMatch(string compatibility, string atomic)
        {
            Identifiers(CompatibilityDefinition(Compatibility, Key)).ShouldEqual(compatibility);
            Identifiers(Definition(Forward, Key)).ShouldEqual(atomic);
            Identifiers(Definition(Reverse, Key)).ShouldEqual(atomic);
        }
    }
}
