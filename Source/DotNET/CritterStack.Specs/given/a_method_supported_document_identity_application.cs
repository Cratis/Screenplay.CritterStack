// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Cratis.CritterStack.Screenplay.given;

public class a_method_supported_document_identity_application : Specification
{
    const string Source =
        """
        namespace Wolverine { public sealed class WolverineOptions; }
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
        namespace IdentityParity
        {
            public sealed class Document
            {
                public int Id { get; set; }
                public int First { get; set; }
                public int Last { get; set; }
            }
            public static class Configuration
            {
                public static void Configure(Marten.StoreOptions options) => options.Schema.For<Document>().Identity(document => document.First);
            }
            public sealed record SaveDocument;
            public static class SaveDocumentHandler
            {
                public static void Handle(SaveDocument message, Marten.IDocumentSession session) => session.Store(new Document());
                public static void OnException(SaveDocument message, System.Exception exception, Marten.StoreOptions options) =>
                    options.Schema.For<Document>().Identity(document => document.Last);
            }
        }
        """;

    static readonly IReadOnlyList<MetadataReference> _references =
    [
        .. ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES"))
            .Split(Path.PathSeparator)
            .Select(_ => MetadataReference.CreateFromFile(_))
    ];

    protected DotNetProjectCompilation Project = null!;
    protected DotNetAnalysisContext Context = null!;
    protected AdapterContribution CompatibilityMarten = null!;
    protected AdapterContribution CompatibilityCombined = null!;
    protected INamedTypeSymbol Document = null!;
    protected IMethodSymbol Configure = null!;
    protected IMethodSymbol Failure = null!;

    void Establish()
    {
        var tree = CSharpSyntaxTree.ParseText(Source, path: "/workspace/IdentityParity/Application.cs");
        var compilation = CSharpCompilation.Create(
            "IdentityParity",
            [tree],
            _references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
        compilation.GetDiagnostics().Where(_ => _.Severity == DiagnosticSeverity.Error).ShouldBeEmpty();
        Project = new()
        {
            Name = "IdentityParity",
            ProjectPath = "/workspace/IdentityParity/IdentityParity.csproj",
            SourceRoot = "/workspace",
            Compilation = compilation,
            AuthoredSyntaxTrees = new HashSet<SyntaxTree> { tree }
        };
        Context = new([Project]);
        Document = compilation.GetTypeByMetadataName("IdentityParity.Document");
        Configure = compilation.GetTypeByMetadataName("IdentityParity.Configuration").GetMembers("Configure").OfType<IMethodSymbol>().Single();
        Failure = compilation.GetTypeByMetadataName("IdentityParity.SaveDocumentHandler").GetMembers("OnException").OfType<IMethodSymbol>().Single();
        CompatibilityMarten = new MartenScreenplayAdapter().Analyze(Context, new());
        CompatibilityCombined = new CritterStackScreenplayAdapter().Analyze(Context, new());
    }

    protected static string Identifiers(IEnumerable<PropertyDefinition> properties) =>
        string.Join(',', properties.Where(_ => _.IsIdentifier).Select(_ => _.Name));

    protected static string CompatibilityIdentifiers(AdapterContribution contribution) =>
        Identifiers(contribution.Facts.OfType<ArtifactFact>().Single(_ => _.Definition.Key.Kind == ArtifactKind.Document && _.Definition.Name == "Document").Definition.Properties);

    protected static string NativeIdentifiers(GeneratedScreenplayDefinition result) =>
        Identifiers(result.Graph.Artifacts.Single(_ => _.Key.Kind == ArtifactKind.Document && _.Variants.Single().Definition.Name == "Document").Variants.Single().Definition.Properties);
}
