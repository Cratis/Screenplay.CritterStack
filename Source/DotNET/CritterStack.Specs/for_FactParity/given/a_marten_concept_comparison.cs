// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Text.Json;
using Cratis.Screenplay.Generation.DotNet.Vogen;
using diagnostic_parity = Cratis.CritterStack.Screenplay.given.diagnostic_parity;

namespace Cratis.CritterStack.Screenplay.for_FactParity.given;

/// <summary>
/// Compares the public Marten rosters, with and without Vogen in either order, to the combined compatibility oracle.
/// </summary>
public class a_marten_concept_comparison : Specification
{
    const string VogenSharedTypesSource =
        """
        namespace Vogen
        {
            public sealed class ValueObjectAttribute<T> : System.Attribute;
        }
        """;

    const string FrameworkSource =
        """
        namespace Marten
        {
            public sealed class StoreOptions
            {
                public void RegisterValueType<T>() { }
            }
            public interface IDocumentSession
            {
                void Store<T>(T document);
            }
        }
        """;

    protected GeneratedScreenplayDefinition Expected { get; private set; } = null!;

    protected GeneratedScreenplayDefinition ExpectedWithVogen { get; private set; } = null!;

    protected GeneratedScreenplayDefinition MartenOnly { get; private set; } = null!;

    protected GeneratedScreenplayDefinition MartenThenVogen { get; private set; } = null!;

    protected GeneratedScreenplayDefinition VogenThenMarten { get; private set; } = null!;

    protected GeneratedScreenplayDefinition PublicMartenThenVogen { get; private set; } = null!;

    protected GeneratedScreenplayDefinition PublicVogenThenMarten { get; private set; } = null!;

    protected string Source { get; set; } = null!;

    protected string Generated { get; set; } = "namespace Ordering { }";

    protected DotNetProjectCompilation Project { get; private set; } = null!;

    protected static string Application(string declarations, string type = "Status", string registration = "", string extra = "", string more = "") =>
        $$"""
        namespace Ordering
        {
        {{declarations}}
        public sealed record PlaceOrder({{type}} Value{{more}});
        public sealed record Order(int Id, {{type}} Value{{more}});
        public sealed record OrderPlaced({{type}} Value{{more}});
        public static class PlaceOrderHandler
        {
            public static OrderPlaced Handle(PlaceOrder command, Order order, Marten.IDocumentSession session)
            {
                session.Store(new Order(1, command.Value{{(more.Length == 0 ? "" : ", command.Other")}}));
                return new(command.Value{{(more.Length == 0 ? "" : ", command.Other")}});
            }
        }
        public static class Configuration
        {
            public static void Configure(Marten.StoreOptions options)
            {
                {{registration}}
            }
        }
        }
        {{extra}}
        """;

    protected bool IsEqual(GeneratedScreenplayDefinition actual) => IsEqual(actual, Expected);

    protected bool IsEqualWithVogen(GeneratedScreenplayDefinition actual) => IsEqual(actual, ExpectedWithVogen);

    static bool IsEqual(GeneratedScreenplayDefinition actual, GeneratedScreenplayDefinition expected) =>
        Concepts(actual) == Concepts(expected) &&
        WholeGraphMatches(actual, expected) &&
        diagnostic_parity.Meaning(actual.Diagnostics).SequenceEqual(diagnostic_parity.Meaning(expected.Diagnostics)) &&
        actual.Source == expected.Source &&
        actual.IsSuccess == expected.IsSuccess;

    protected int Warnings(GeneratedScreenplayDefinition result, string code) => result.Diagnostics.Count(_ => _.Code == code);

    protected void Compare()
    {
        var framework = CSharpSyntaxTree.ParseText(FrameworkSource, path: "/workspace/Ordering/Framework.cs");
        var application = CSharpSyntaxTree.ParseText(Source, path: "/workspace/Ordering/Application.cs");
        var platform = ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")).Split(Path.PathSeparator).Select(path => MetadataReference.CreateFromFile(path)).ToArray();
        using var sharedTypes = new MemoryStream();
        CSharpCompilation.Create(
            "Vogen.SharedTypes",
            [CSharpSyntaxTree.ParseText(VogenSharedTypesSource)],
            platform,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary)).Emit(sharedTypes).Success.ShouldBeTrue();
        var compilation = CSharpCompilation.Create(
            "Ordering",
            [framework, application, CSharpSyntaxTree.ParseText(Generated, path: "/workspace/obj/Vogen.g.cs")],
            [.. platform, MetadataReference.CreateFromImage(sharedTypes.ToArray())],
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, nullableContextOptions: NullableContextOptions.Enable));
        compilation.GetDiagnostics().Where(_ => _.Severity == DiagnosticSeverity.Error).ShouldBeEmpty();
        var project = new DotNetProjectCompilation
        {
            Name = "Ordering",
            ProjectPath = "/workspace/Ordering/Ordering.csproj",
            SourceRoot = "/workspace",
            Compilation = compilation,
            AuthoredSyntaxTrees = new HashSet<SyntaxTree> { framework, application },
            SourceContext = DotNetSourcePaths.Create(
                "Ordering/Ordering",
                new DotNetSourcePathPolicy { DisplayRoot = DotNetSourceDisplayRoot.Workspace, CasePolicy = DotNetSourcePathCasePolicy.Ordinal },
                [.. new[] { framework, application }.Select(tree => new DotNetSourceDocument
                {
                    SyntaxTree = tree,
                    ProjectRelativePath = Path.GetRelativePath("/workspace/Ordering", tree.FilePath),
                    WorkspaceRelativePath = Path.GetRelativePath("/workspace", tree.FilePath)
                })])
        };
        Project = project;
        var options = new CritterStackScreenplayOptions { Domain = "Ordering" };
        var analysis = new DotNetAnalysisContext([project]);
        var adapterOptions = new DotNetAdapterOptions();
        var generation = new ScreenplayGenerationOptions { Domain = "Ordering" };
        var marten = new[] { new MartenScreenplayAdapter().Analyze(analysis, adapterOptions) };
        var vogen = new VogenConceptScreenplayAdapter().Analyze(analysis, adapterOptions);
        var compatibility = new CritterStackScreenplayAdapter().Analyze(analysis, adapterOptions);
        Expected = new ScreenplayDefinitionGenerator().Generate([compatibility], generation);

        // Compatibility composes an independent concept as the public generator does: bind member types to contributed concepts, then drop missing ones.
        ExpectedWithVogen = new ScreenplayDefinitionGenerator().Generate(ConceptTypeReferenceBinder.WithoutMissingConcepts(ConceptTypeReferenceBinder.Bind(analysis, [compatibility, vogen])), generation);
        MartenOnly = new ScreenplayDefinitionGenerator().Generate(marten, generation);
        MartenThenVogen = new ScreenplayDefinitionGenerator().Generate([.. marten, vogen], generation);
        VogenThenMarten = new ScreenplayDefinitionGenerator().Generate([vogen, .. marten], generation);
        PublicMartenThenVogen = new CritterStackScreenplayGenerator([new MartenScreenplayAdapter(), new WolverineScreenplayAdapter(), new WolverineMartenScreenplayAdapter(), new VogenConceptScreenplayAdapter()]).Generate([project], options);
        PublicVogenThenMarten = new CritterStackScreenplayGenerator([new VogenConceptScreenplayAdapter(), new MartenScreenplayAdapter(), new WolverineScreenplayAdapter(), new WolverineMartenScreenplayAdapter()]).Generate([project], options);
    }

    // Accepted metadata delta: decision-gap10-enum-concept-kind.md, Revision 2026-11-24. Native binds subject-bearing types to concepts; the oracle leaves the discriminator null.
    static bool HasOnlyAcceptedDiscriminatorDeltas(GeneratedScreenplayDefinition actual, GeneratedScreenplayDefinition expected) => Types(actual)
        .Zip(Types(expected))
        .All(pair => pair.First.TargetArtifactKind == pair.Second.TargetArtifactKind ||
            (pair.Second.TargetArtifactKind is null && pair.First.TargetArtifactKind == ArtifactKind.Concept && pair.First.Subject is not null));

    static IEnumerable<TypeReferenceDefinition> Types(GeneratedScreenplayDefinition result) => Definitions(result)
        .SelectMany(definition => definition.Properties)
        .Select(property => property.Type);

    static IEnumerable<ArtifactDefinition> Definitions(GeneratedScreenplayDefinition result) => result.Graph.Artifacts
        .SelectMany(artifact => artifact.Variants)
        .Select(variant => variant.Definition);

    static bool WholeGraphMatches(GeneratedScreenplayDefinition actual, GeneratedScreenplayDefinition expected) =>
        Artifacts(actual) == Artifacts(expected) &&
        HasOnlyAcceptedDiscriminatorDeltas(actual, expected) &&
        Relationships(actual) == Relationships(expected);

    static string Artifacts(GeneratedScreenplayDefinition result) => JsonSerializer.Serialize(Definitions(result)
        .Select(definition => definition with
        {
            Properties = [.. definition.Properties.Select(property => property with { Type = property.Type with { TargetArtifactKind = null } })]
        })
        .ToArray());

    static string Relationships(GeneratedScreenplayDefinition result) => JsonSerializer.Serialize(result.Graph.Relationships
        .SelectMany(relationship => relationship.Definitions)
        .ToArray());

    static string Concepts(GeneratedScreenplayDefinition result) => JsonSerializer.Serialize(result.Graph.Artifacts
        .Where(artifact => artifact.Key.Kind == ArtifactKind.Concept)
        .SelectMany(artifact => artifact.Variants)
        .Select(variant => variant.Definition)
        .ToArray());
}
