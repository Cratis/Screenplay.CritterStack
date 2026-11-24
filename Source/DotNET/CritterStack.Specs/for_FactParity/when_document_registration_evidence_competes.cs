// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using static Cratis.CritterStack.Screenplay.for_FactParity.given.fact_vectors;

namespace Cratis.CritterStack.Screenplay.for_FactParity;

/// <summary>Registration evidence is provenance; the artifact file remains the authored type's file.</summary>
public class when_document_registration_evidence_competes : Specification
{
    ArtifactKey _key = null!;
    string _compatibilityFile = null!;
    GeneratedScreenplayDefinition _forward = null!;
    GeneratedScreenplayDefinition _reverse = null!;
    GeneratedScreenplayDefinition _classified = null!;
    GeneratedScreenplayDefinition _classifiedReverse = null!;

    void Because()
    {
        var project = Project(
        [
            Tree(Framework, "Framework"), Tree("namespace FactParity; public sealed record Document(int Id);", "Document"),
            Tree(Configuration, "Configuration"), Tree(Handler, "Handler")
        ]);
        var document = Type(project, "FactParity.Document");
        _key = Key(project, document, ArtifactKind.Document);
        var first = Type(project, "FactParity.Configuration").GetMembers("Configure").OfType<IMethodSymbol>().Single();
        var failure = Type(project, "FactParity.SaveHandler").GetMembers("OnException").OfType<IMethodSymbol>().Single();
        var surface = DotNetTypeUseFacts.Emit(document, _key, new([project]), Evidence(project, document, MartenAdapter));
        var facts = new List<GenerationFact>(surface) { Role(_key, "id", Evidence(project, document, MartenAdapter)) };
        facts.AddRange(ObserveRegistration(project, document, first));
        facts.AddRange(ObserveRegistration(project, document, failure));
        var declarations = Contribution(MartenAdapter, facts);
        var peer = Contribution(WolverineAdapter, []);
        var classifier = Failure(project, failure);
        _forward = Generate(declarations, peer);
        _reverse = Generate(peer, declarations);
        _classified = Generate(declarations, classifier);
        _classifiedReverse = Generate(classifier, declarations);
        _compatibilityFile = CompatibilityDefinition(new MartenScreenplayAdapter().Analyze(new([project]), new()), _key).File;
    }

    [Fact] void should_locate_the_compatibility_declaration_at_the_authored_type() => _compatibilityFile.ShouldEqual("FactParity/Document.cs");
    [Fact] void should_preserve_the_declaration_file() => Definition(_forward, _key).File.ShouldEqual(_compatibilityFile);
    [Fact] void should_verify_compatible_headers() => _forward.IsSuccess.ShouldBeTrue();
    [Fact] void should_verify_compatible_headers_in_reverse_order() => _reverse.IsSuccess.ShouldBeTrue();
    [Fact] void should_preserve_the_declaration_file_in_reverse_order() => Definition(_reverse, _key).File.ShouldEqual(_compatibilityFile);
    [Fact] void should_retain_both_registration_locations_as_evidence() => _forward.Graph.Artifacts.Single(_ => _.Key == _key).Variants.Single().Evidence.Select(_ => _.Source?.Path).ShouldContainOnly("FactParity/Document.cs", "FactParity/Configuration.cs", "FactParity/Handler.cs");
    [Fact] void should_select_the_remaining_header_after_failure_classification() => Definition(_classified, _key).File.ShouldEqual(_compatibilityFile);
    [Fact] void should_select_the_remaining_header_in_reverse_order() => Definition(_classifiedReverse, _key).File.ShouldEqual(_compatibilityFile);
    [Fact] void should_verify_the_remaining_header() => (_classified.IsSuccess && _classifiedReverse.IsSuccess).ShouldBeTrue();

    IReadOnlyList<GenerationFact> ObserveRegistration(DotNetProjectCompilation project, INamedTypeSymbol document, IMethodSymbol method)
    {
        var evidence = Evidence(project, method, MartenAdapter, EvidenceStrength.Configured);
        var declaration = Declaration(_key, document, evidence, method.Name) with
        {
            Definition = new() { Artifact = _key, Name = document.Name, File = Evidence(project, document, MartenAdapter).Source?.Path }
        };
        return [declaration, CritterStackMethodFacts.Support(declaration, [CritterStackMethodFacts.Observe(project, method)])];
    }

    static SyntaxTree Tree(string source, string name) => CSharpSyntaxTree.ParseText(source, path: $"/workspace/FactParity/{name}.cs");

    const string Framework = """
        namespace Wolverine { public sealed class WolverineOptions; }
        namespace Marten
        {
            public interface IDocumentSession { void Store<T>(T document); }
            public interface IDocumentStore;
            public sealed class StoreOptions { public MartenRegistry Schema { get; } = new(); }
            public sealed class MartenRegistry { public object For<T>() => new(); }
        }
        """;

    const string Configuration = """
        namespace FactParity;
        public static class Configuration
        {
            public static void Configure(Marten.StoreOptions options) => options.Schema.For<Document>();
        }
        """;

    const string Handler = """
        namespace FactParity;
        public sealed record Save;
        public static class SaveHandler
        {
            public static void Handle(Save message, Marten.IDocumentSession session) => session.Store(new Document(1));
            public static void OnException(Save message, System.Exception exception, Marten.StoreOptions options) => options.Schema.For<Document>();
        }
        """;
}
