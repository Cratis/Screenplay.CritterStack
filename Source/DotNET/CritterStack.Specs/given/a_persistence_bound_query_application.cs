// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Cratis.CritterStack.Screenplay.given;

public class a_persistence_bound_query_application : Specification
{
    const string FrameworkSource =
        """
        namespace Wolverine
        {
            public class WolverineOptions;
        }

        namespace Wolverine.Http
        {
            public abstract class WolverineHttpMethodAttribute(string route) : System.Attribute;
            public class WolverineGetAttribute(string route) : WolverineHttpMethodAttribute(route);
        }

        namespace Wolverine.Persistence
        {
            [System.AttributeUsage(System.AttributeTargets.Parameter)]
            public class EntityAttribute(string? key = null) : System.Attribute
            {
                public bool Required { get; set; } = true;
            }

            [System.AttributeUsage(System.AttributeTargets.Parameter)]
            public class FirstOrDefaultAttribute : System.Attribute;

            [System.AttributeUsage(System.AttributeTargets.Parameter)]
            public class QueryableAttribute : System.Attribute;
        }
        """;

    const string ApplicationSource =
        """
        using Wolverine.Http;
        using Wolverine.Persistence;

        namespace PersistenceBoundQueries;

        public record Student(int Id, string Name);
        public record Defaults(string Name);
        public record Heartbeat(string Id);

        public static class StudentEndpoints
        {
            [WolverineGet("/students/{id}")]
            public static Student? GetById(int id, [Entity("id")] Student? student) => student;

            [WolverineGet("/defaults")]
            public static Defaults? GetDefaults([FirstOrDefault] Defaults? defaults) => defaults;

            [WolverineGet("/heartbeats")]
            public static System.Linq.IQueryable<Heartbeat> GetHeartbeats(
                [Queryable] System.Linq.IQueryable<Heartbeat> heartbeats) => heartbeats;
        }
        """;

    static readonly IReadOnlyList<MetadataReference> _references =
    [
        .. ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")!)
            .Split(Path.PathSeparator)
            .Select(_ => MetadataReference.CreateFromFile(_))
    ];

    protected DotNetProjectCompilation Project = null!;
    protected AdapterContribution Contribution = null!;

    void Establish()
    {
        var compilation = CSharpCompilation.Create(
            "PersistenceBoundQueries",
            [
                CSharpSyntaxTree.ParseText(FrameworkSource, path: "/workspace/Framework.cs"),
                CSharpSyntaxTree.ParseText(ApplicationSource, path: "/workspace/PersistenceBoundQueries/Queries.cs")
            ],
            _references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, nullableContextOptions: NullableContextOptions.Enable));
        Project = new()
        {
            Name = "PersistenceBoundQueries",
            ProjectPath = "/workspace/PersistenceBoundQueries/PersistenceBoundQueries.csproj",
            SourceRoot = "/workspace",
            Compilation = compilation,
            AuthoredSyntaxTrees = compilation.SyntaxTrees.ToHashSet()
        };
        Contribution = new CritterStackScreenplayAdapter().Analyze(
            new DotNetAnalysisContext([Project]),
            new DotNetAdapterOptions());
    }
}
