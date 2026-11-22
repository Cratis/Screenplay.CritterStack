// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Cratis.CritterStack.Screenplay.given;

/// <summary>
/// A Wolverine HTTP query whose Marten compiled-query plan exposes both fields and properties,
/// one of which shares a normalized name with an endpoint parameter of a different type.
/// </summary>
public class a_compiled_query_member_vector_application : Specification
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

        namespace Marten.Linq
        {
            public interface IMartenQueryable<T> : System.Linq.IQueryable<T>;
            public interface ICompiledQuery<TDoc, TOut>
            {
                System.Linq.Expressions.Expression<System.Func<IMartenQueryable<TDoc>, TOut>> QueryIs();
            }
            public interface ICompiledListQuery<TDoc, TOut> : ICompiledQuery<TDoc, System.Collections.Generic.IEnumerable<TOut>>;
        }

        namespace Marten
        {
            public interface IDocumentStore;
            public class StoreOptions;
            public interface IQuerySession
            {
                System.Threading.Tasks.Task<TOut> QueryAsync<TDoc, TOut>(Marten.Linq.ICompiledQuery<TDoc, TOut> query, System.Threading.CancellationToken token = default);
            }
        }
        """;

    const string ApplicationSource =
        """
        using System.Linq;

        namespace Vector;

        public class Student
        {
            public int Id { get; set; }
            public string Name { get; set; } = string.Empty;
        }

        public record StudentResult(int Id, string Name);

        public class StudentsByName : Marten.Linq.ICompiledListQuery<Student, StudentResult>
        {
            public int Name { get; set; }
            public int Take;
            public string Tag { get; set; } = string.Empty;

            public System.Linq.Expressions.Expression<System.Func<Marten.Linq.IMartenQueryable<Student>, System.Collections.Generic.IEnumerable<StudentResult>>> QueryIs() =>
                query => query.Select(student => new StudentResult(student.Id, student.Name));
        }

        public static class StudentEndpoints
        {
            [Wolverine.Http.WolverineGet("/students/search")]
            public static async System.Threading.Tasks.Task<System.Collections.Generic.IReadOnlyList<StudentResult>> Search(
                string name,
                Marten.IQuerySession session)
            {
                return (await session.QueryAsync(new StudentsByName { Name = 1 })).ToArray();
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
    protected AdapterContribution Contribution = null!;

    void Establish()
    {
        var compilation = CSharpCompilation.Create(
            "Vector",
            [
                CSharpSyntaxTree.ParseText(FrameworkSource, path: "/workspace/Framework.cs"),
                CSharpSyntaxTree.ParseText(ApplicationSource, path: "/workspace/Vector/Students.cs")
            ],
            _references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, nullableContextOptions: NullableContextOptions.Enable));
        Project = new DotNetProjectCompilation
        {
            Name = "Vector",
            ProjectPath = "/workspace/Vector/Vector.csproj",
            SourceRoot = "/workspace",
            Compilation = compilation,
            AuthoredSyntaxTrees = compilation.SyntaxTrees.ToHashSet()
        };
        Contribution = new CritterStackScreenplayAdapter().Analyze(new DotNetAnalysisContext([Project]), new DotNetAdapterOptions());
    }
}
