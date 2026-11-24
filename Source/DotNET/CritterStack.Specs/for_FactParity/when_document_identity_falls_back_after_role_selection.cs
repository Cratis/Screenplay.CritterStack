// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Cratis.CritterStack.Screenplay.given;
using Microsoft.CodeAnalysis.CSharp.Syntax;

using static Cratis.CritterStack.Screenplay.for_FactParity.given.fact_vectors;

namespace Cratis.CritterStack.Screenplay.for_FactParity;

/// <summary>
/// Source interpretation supplies ordered alternatives; shared method eligibility selects exactly one identity.
/// </summary>
public class when_document_identity_falls_back_after_role_selection : Specification
{
    readonly List<IdentityVector> _vectors = [];

    void Because()
    {
        Add("id", new());
        Add("first", new(Attributed: true));
        Add("last", new(Attributed: true, Expression: "document.Last"));
        Add(string.Empty, new(Expression: "document.First + document.Last"));
        Add("first", new(Attributed: true, Expression: "document.Last", FailureOnly: true));
        Add("id", new(Expression: "document.First + document.Last", FailureOnly: true));
        Add(string.Empty, new(Shape: IdentityShape.Private));
        Add(string.Empty, new(Shape: IdentityShape.Field));
        Add("inheritedKey", new(Shape: IdentityShape.Inherited));
        Add(string.Empty, new(Shape: IdentityShape.Hidden));
    }

    [Fact] void should_exercise_all_identity_shapes() => _vectors.Count.ShouldEqual(10);
    [Fact] void should_verify_all_atomic_candidates() => _vectors.TrueForAll(_ => _.Forward.IsSuccess && _.Reverse.IsSuccess).ShouldBeTrue();

    [Fact]
    void should_match_compatibility_in_either_order()
    {
        foreach (var vector in _vectors)
        {
            Identifiers(CompatibilityDefinition(vector.Compatibility, vector.Key)).ShouldEqual(vector.Expected);
            Identifiers(Definition(vector.Forward, vector.Key)).ShouldEqual(vector.Expected);
            Identifiers(Definition(vector.Reverse, vector.Key)).ShouldEqual(vector.Expected);
        }
    }

    [Fact]
    void should_publish_only_the_winning_identity_diagnostics()
    {
        foreach (var vector in _vectors)
        {
            var expected = IdentityDiagnostics(vector.Compatibility.Diagnostics);
            IdentityDiagnostics(vector.Forward.Diagnostics).ShouldEqual(expected);
            IdentityDiagnostics(vector.Reverse.Diagnostics).ShouldEqual(expected);
        }
    }

    void Add(string expected, IdentityInput input)
    {
        var project = Project(Source(input.Attributed, input.Expression, input.FailureOnly, input.Shape));
        var document = Type(project, "FactParity.Document");
        var key = Key(project, document, ArtifactKind.Document);
        var evidence = Evidence(project, document, MartenAdapter, EvidenceStrength.Exact);
        var facts = new List<GenerationFact> { Declaration(key, document, evidence) };
        facts.AddRange(DotNetTypeUseFacts.EmitWithSurface(document, key, new([project]), evidence, DotNetMemberOriginPolicy.AllOrigins, DotNetMemberSurfaceScope.Inherited));
        var baselineEvidence = InvocationEvidence(project, "Store");
        facts.Add(Selection(key, baselineEvidence, new(BaselineMember(input), 0, BaselineDiagnostics(key, input.Shape, baselineEvidence))));
        var methodType = Type(project, input.FailureOnly ? "FactParity.SaveDocumentHandler" : "FactParity.Configuration");
        var method = methodType.GetMembers(input.FailureOnly ? "OnException" : "Configure").OfType<IMethodSymbol>().Single();
        facts.AddRange(ConfiguredSelection(project, key, method, input.Expression));
        var contribution = Contribution(MartenAdapter, facts);
        var peer = input.FailureOnly ? Failure(project, method) : Contribution(WolverineAdapter, []);
        var compatibility = new CritterStackScreenplayAdapter().Analyze(new([project]), new());
        _vectors.Add(new(key, expected, compatibility, Generate(contribution, peer), Generate(peer, contribution)));
    }

    static string? BaselineMember(IdentityInput input) => input.Shape switch
    {
        IdentityShape.Private or IdentityShape.Field or IdentityShape.Hidden => null,
        IdentityShape.Inherited => "inheritedKey",
        _ => input.Attributed ? "first" : "id"
    };

    static IReadOnlyList<GenerationFact> ConfiguredSelection(DotNetProjectCompilation project, ArtifactKey key, IMethodSymbol method, string? expression)
    {
        if (expression is null) return [];
        var evidence = InvocationEvidence(project, "Identity");
        var diagnostics = expression == "document.Last" ? [] : new[] { Unresolved(key, evidence) };
        var configured = Selection(key, evidence, new(expression == "document.Last" ? "last" : null, 1, diagnostics));
        return [configured, CritterStackMethodFacts.Support(configured, [CritterStackMethodFacts.Observe(project, method)])];
    }

    static ArtifactMemberRoleSelectionFact Selection(ArtifactKey key, Evidence evidence, IdentityAlternative alternative) => new()
    {
        Id = new() { Value = $"identity-selection:{key.Subject.Value}:{alternative.Priority}" },
        Subject = key.Subject,
        Evidence = evidence,
        Definition = new()
        {
            Artifact = key,
            Member = alternative.Member is null ? null : new() { Artifact = key, Name = alternative.Member },
            Role = ArtifactMemberRoleKind.EventSourceIdentifier,
            Priority = alternative.Priority,
            ObservationOrder = 0,
            Diagnostics = alternative.Diagnostics
        }
    };

    static GenerationDiagnostic Unresolved(ArtifactKey key, Evidence evidence) => new()
    {
        Code = MartenDiagnosticCodes.DocumentIdentityUnresolved,
        Severity = GenerationDiagnosticSeverity.Warning,
        Outcome = GenerationDiagnosticOutcome.Unknown,
        Message = "Marten identity configuration for 'Document' is not a direct member expression and cannot be resolved safely",
        Subject = key.Subject,
        Source = evidence.Source
    };

    static IReadOnlyList<GenerationDiagnostic> BaselineDiagnostics(ArtifactKey key, IdentityShape shape, Evidence evidence)
    {
        var member = shape switch
        {
            IdentityShape.Private => "Document.PrivateKey",
            IdentityShape.Field => "Document.FieldKey",
            IdentityShape.Hidden => "BaseDocument.First",
            _ => null
        };
        return member is null ? [] : [Unresolved(key, evidence) with
        {
            Outcome = GenerationDiagnosticOutcome.Unsupported,
            Message = $"Marten identity member '{member}' cannot be represented uniquely in the emitted 'Document' document property shape"
        }];
    }

    static Evidence InvocationEvidence(DotNetProjectCompilation project, string name)
    {
        var invocation = project.AuthoredSyntaxTrees.Single().GetRoot().DescendantNodes().OfType<InvocationExpressionSyntax>()
            .Single(_ => _.Expression is MemberAccessExpressionSyntax member && member.Name.Identifier.ValueText == name);
        return new()
        {
            Adapter = MartenAdapter,
            Strength = EvidenceStrength.Exact,
            Source = CritterStackSource.RangeForProject(invocation.GetLocation(), project)
        };
    }

    static string[] IdentityDiagnostics(IEnumerable<GenerationDiagnostic> diagnostics) =>
        diagnostic_parity.Meaning(diagnostics.Where(_ => _.Code == MartenDiagnosticCodes.DocumentIdentityUnresolved));

    static string Source(bool attributed, string? expression, bool failureOnly, IdentityShape shape)
    {
        var configuration = expression is null ? string.Empty : $"options.Schema.For<Document>().Identity(document => {expression});";
        var special = shape switch
        {
            IdentityShape.Private => "[JasperFx.Identity] private int PrivateKey { get; set; }",
            IdentityShape.Field => "[JasperFx.Identity] public int FieldKey;",
            _ => string.Empty
        };
        var inherited = shape == IdentityShape.Inherited ? "[JasperFx.Identity] public int InheritedKey { get; set; }" : string.Empty;
        var hidden = shape == IdentityShape.Hidden;
        return Framework + $$"""
            namespace FactParity
            {
                public class BaseDocument { {{inherited}} {{(hidden ? "[JasperFx.Identity] public int First { get; set; }" : string.Empty)}} }
                public sealed class Document : BaseDocument
                {
                    public int Id { get; set; }
                    {{(attributed ? "[JasperFx.Identity]" : string.Empty)}}
                    public {{(hidden ? "new " : string.Empty)}}int First { get; set; }
                    public int Last { get; set; }
                    {{special}}
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

    enum IdentityShape
    {
        Ordinary,
        Private,
        Field,
        Inherited,
        Hidden
    }

    sealed record IdentityInput(bool Attributed = false, string? Expression = null, bool FailureOnly = false, IdentityShape Shape = IdentityShape.Ordinary);

    sealed record IdentityAlternative(string? Member, int Priority, IReadOnlyList<GenerationDiagnostic> Diagnostics);

    sealed record IdentityVector(ArtifactKey Key, string Expected, AdapterContribution Compatibility, GeneratedScreenplayDefinition Forward, GeneratedScreenplayDefinition Reverse);
}
