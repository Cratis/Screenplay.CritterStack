// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Cratis.CritterStack.Screenplay.given;

public class an_authored_abstraction_application : Specification
{
    const string FrameworkSource =
        """
        namespace Marten
        {
            public interface IDocumentSession { Marten.Events.IEventStoreOperations Events { get; } }
        }
        namespace Marten.Events
        {
            public interface IEventStoreOperations { void Append(System.Guid id, params object[] events); }
        }
        namespace Wolverine { public class WolverineOptions; }
        namespace Wolverine.Http
        {
            public class WolverinePostAttribute(string route) : System.Attribute;
        }
        """;

    protected const string RepositorySource =
        """
        namespace Persistence;
        public sealed class Repository(Marten.IDocumentSession session) : Contracts.IRepository<Contracts.Submit>
        {
            public void Append(Contracts.Submit command) => Store(command.Id);
            public void Append(string value) => throw new System.NotSupportedException();
            void Store(System.Guid id) => session.Events.Append(id, new Contracts.Made(id));
        }
        """;

    static readonly IReadOnlyList<MetadataReference> _references =
    [
        .. ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES"))
            .Split(Path.PathSeparator)
            .Select(path => MetadataReference.CreateFromFile(path))
    ];

    protected virtual string ContractSource =>
        """
        namespace Contracts;
        public record Submit(System.Guid Id);
        public record Made(System.Guid Id);
        public record OtherMade(System.Guid Id);
        public interface IRepository<T>
        {
            void Append(T command);
            void Append(string value);
        }
        """;

    protected virtual string ImplementationSource => RepositorySource;
    protected virtual string OtherImplementationSource => string.Empty;
    protected virtual string HandlerSource =>
        """
        namespace Application;
        public static class SubmitHandler
        {
            public static void Handle(Contracts.Submit command, Contracts.IRepository<Contracts.Submit> repository) => repository.Append(command);
        }
        """;

    protected virtual bool IncludeImplementation => true;
    protected virtual bool GeneratedImplementation => false;
    protected virtual bool IncludeOtherImplementation => true;
    protected virtual bool FollowAuthoredAbstractions => true;

    protected IReadOnlyList<DotNetProjectCompilation> Projects = null!;
    protected GeneratedScreenplayDefinition Result = null!;

    protected IReadOnlyList<GenerationDiagnostic> Unresolved =>
        [.. Result.Diagnostics.Where(diagnostic => diagnostic.Code == WolverineDiagnosticCodes.AuthoredAbstractionUnresolved)];

    protected IReadOnlyList<ResolvedRelationship> Productions =>
        [.. Result.Graph.Relationships.Where(relationship => relationship.Key.Kind == RelationshipKind.Produces)];

    void Establish() => Projects = CreateProjects();

    protected void Generate() => Result = GenerateFrom(Projects, FollowAuthoredAbstractions);

    protected static GeneratedScreenplayDefinition GenerateFrom(IEnumerable<DotNetProjectCompilation> projects, bool follow) =>
        new CritterStackScreenplayGenerator().Generate(
            projects.ToArray(),
            new CritterStackScreenplayOptions { Domain = "Authored", FollowAuthoredAbstractions = follow });

    protected IReadOnlyList<DotNetProjectCompilation> CreateProjects(string root = "/workspace")
    {
        var contracts = Project("Contracts", [FrameworkSource, ContractSource], [], root);
        var implementation = Project("Persistence", [ImplementationSource], [contracts], root, GeneratedImplementation);
        var other = Project("Other", [OtherImplementationSource], [contracts], root);
        var application = Project("Application", [HandlerSource], [contracts, implementation, other], root);
        return
        [
            contracts,
            application,
            .. IncludeImplementation ? new[] { implementation } : [],
            .. IncludeOtherImplementation ? new[] { other } : []
        ];
    }

    protected static string Transcript(GeneratedScreenplayDefinition result) =>
        System.Text.Json.JsonSerializer.Serialize(new { result.Source, result.Graph, result.Diagnostics });

    protected void ShouldRejectWithoutPersistence()
    {
        Unresolved.ShouldNotBeEmpty();
        Unresolved.ShouldEachConformTo(diagnostic => diagnostic.Severity == GenerationDiagnosticSeverity.Warning && diagnostic.Outcome == GenerationDiagnosticOutcome.Unknown);
        Productions.ShouldBeEmpty();
        Result.Graph.Artifacts.Where(artifact => artifact.Key.Kind == ArtifactKind.Event).ShouldBeEmpty();
    }

    static DotNetProjectCompilation Project(
        string name,
        IEnumerable<string> sources,
        IReadOnlyList<DotNetProjectCompilation> references,
        string root,
        bool generated = false)
    {
        var trees = sources.Select((source, index) => CSharpSyntaxTree.ParseText(source, path: $"{root}/{name}/Source{index}.cs")).ToArray();
        var compilation = CSharpCompilation.Create(
            name,
            trees,
            [.. _references, .. references.Select(project => project.Compilation.ToMetadataReference())],
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, nullableContextOptions: NullableContextOptions.Enable));
        compilation.GetDiagnostics().Where(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error).ShouldBeEmpty();
        return new DotNetProjectCompilation
        {
            Name = name,
            Compilation = compilation,
            AuthoredSyntaxTrees = generated ? new HashSet<SyntaxTree>() : trees.ToHashSet<SyntaxTree>(),
            SourceContext = DotNetSourcePaths.Create(
                name,
                new DotNetSourcePathPolicy
                {
                    Version = 1,
                    DisplayRoot = DotNetSourceDisplayRoot.Workspace,
                    CasePolicy = DotNetSourcePathCasePolicy.Ordinal
                },
                trees.Select((tree, index) => new DotNetSourceDocument
                {
                    SyntaxTree = tree,
                    ProjectRelativePath = $"Source{index}.cs",
                    WorkspaceRelativePath = $"{name}/Source{index}.cs"
                }))
        };
    }
}
