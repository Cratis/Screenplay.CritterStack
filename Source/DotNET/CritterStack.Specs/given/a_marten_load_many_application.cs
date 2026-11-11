// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Cratis.CritterStack.Screenplay.given;

/// <summary>
/// Synchronous and asynchronous multi-document loads next to a compiled query whose document type does not resolve.
/// </summary>
public class a_marten_load_many_application : Specification
{
    const string FrameworkSource =
        """
        namespace Marten.Linq
        {
            public interface ICompiledQuery<TDoc, TOut>
            {
                System.Linq.Expressions.Expression<System.Func<System.Linq.IQueryable<TDoc>, TOut>> QueryIs();
            }
        }

        namespace Marten
        {
            public interface IDocumentStore;
            public interface IQuerySession
            {
                System.Collections.Generic.IReadOnlyList<T> LoadMany<T>(params System.Guid[] ids) where T : class;
                System.Threading.Tasks.Task<System.Collections.Generic.IReadOnlyList<T>> LoadManyAsync<T>(params System.Guid[] ids) where T : class;
                System.Threading.Tasks.Task<TOut> QueryAsync<TDoc, TOut>(Marten.Linq.ICompiledQuery<TDoc, TOut> query);
            }
        }
        """;

    const string ApplicationSource =
        """
        namespace Docs;

        public class Sync { public System.Guid Id { get; set; } }
        public class Async { public System.Guid Id { get; set; } }

        public static class Reads
        {
            public static void Many(Marten.IQuerySession session)
            {
                session.LoadMany<Sync>(System.Guid.Empty);
                session.LoadManyAsync<Async>(System.Guid.Empty);
            }
        }

        public class MissingPlan : Marten.Linq.ICompiledQuery<MissingDocument, int>
        {
            public System.Linq.Expressions.Expression<System.Func<System.Linq.IQueryable<MissingDocument>, int>> QueryIs() =>
                query => 0;
        }

        public static class Executions
        {
            public static void Run(Marten.IQuerySession session) => session.QueryAsync(new MissingPlan());
        }
        """;

    static readonly IReadOnlyList<MetadataReference> _references =
    [
        .. ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES"))
            .Split(Path.PathSeparator)
            .Select(_ => MetadataReference.CreateFromFile(_))
    ];

    protected AdapterContribution Contribution = null!;

    void Establish()
    {
        var frameworkTree = CSharpSyntaxTree.ParseText(FrameworkSource, path: "/workspace/Framework.cs");
        var applicationTree = CSharpSyntaxTree.ParseText(ApplicationSource, path: "/workspace/Docs/Docs.cs");
        var compilation = CSharpCompilation.Create(
            "Docs",
            [frameworkTree, applicationTree],
            _references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, nullableContextOptions: NullableContextOptions.Enable));
        var project = new DotNetProjectCompilation
        {
            Name = "Docs",
            ProjectPath = "/workspace/Docs/Docs.csproj",
            SourceRoot = "/workspace",
            Compilation = compilation,
            AuthoredSyntaxTrees = new HashSet<SyntaxTree> { frameworkTree, applicationTree }
        };
        Contribution = new CritterStackScreenplayAdapter().Analyze(
            new DotNetAnalysisContext([project]),
            new DotNetAdapterOptions());
    }
}
