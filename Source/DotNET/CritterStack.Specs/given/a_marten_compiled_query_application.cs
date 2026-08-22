// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Cratis.CritterStack.Screenplay.given;

public class a_marten_compiled_query_application : Specification
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

        namespace Marten.Events.CodeGeneration
        {
            public class MartenIgnoreAttribute : System.Attribute;
        }

        namespace Marten.Linq
        {
            public interface IMartenQueryable<T> : System.Linq.IQueryable<T>;
            public interface ICompiledQuery<TDoc, TOut>
            {
                System.Linq.Expressions.Expression<System.Func<IMartenQueryable<TDoc>, TOut>> QueryIs();
            }
            public interface ICompiledQuery<TDoc> : ICompiledQuery<TDoc, TDoc>;
            public interface ICompiledListQuery<TDoc, TOut> : ICompiledQuery<TDoc, System.Collections.Generic.IEnumerable<TOut>>;
            public interface ICompiledListQuery<TDoc> : ICompiledListQuery<TDoc, TDoc>;
        }

        namespace Marten
        {
            public interface IDocumentStore;
            public class StoreOptions;
            public interface IQueryPlan<T>;
            public interface IQuerySession
            {
                System.Threading.Tasks.Task<TOut> QueryAsync<TDoc, TOut>(Marten.Linq.ICompiledQuery<TDoc, TOut> query, System.Threading.CancellationToken token = default);
                System.Threading.Tasks.Task<T> QueryByPlanAsync<T>(IQueryPlan<T> plan, System.Threading.CancellationToken token = default);
            }
        }

        namespace Marten.Services.BatchQuerying
        {
            public interface IBatchedQuery
            {
                System.Threading.Tasks.Task<TOut> Query<TDoc, TOut>(Marten.Linq.ICompiledQuery<TDoc, TOut> query);
            }
        }
        """;

    const string ApplicationSource =
        """
        using System.Linq;

        namespace Students;

        public class Student
        {
            public int Id { get; set; }
            public string Name { get; set; } = string.Empty;
        }

        public class OtherStudent
        {
            public int Id { get; set; }
        }

        public record StudentResult(int Id, string Name);

        public abstract class PagedPlan
        {
            public int Page { get; set; }
        }

        public class StudentsByName : PagedPlan, Marten.Linq.ICompiledListQuery<Student, StudentResult>
        {
            public string Name { get; set; } = string.Empty;
            [Marten.Events.CodeGeneration.MartenIgnore]
            public string InternalToken { get; set; } = string.Empty;
            public string WriteOnly { set { } }

            public System.Linq.Expressions.Expression<System.Func<Marten.Linq.IMartenQueryable<Student>, System.Collections.Generic.IEnumerable<StudentResult>>> QueryIs() =>
                query => query.Where(student => student.Name == Name).Select(student => new StudentResult(student.Id, student.Name));
        }

        public class FirstStudent : Marten.Linq.ICompiledQuery<Student>
        {
            public int Id { get; set; }
            public System.Linq.Expressions.Expression<System.Func<Marten.Linq.IMartenQueryable<Student>, Student>> QueryIs() =>
                query => query.First(student => student.Id == Id);
        }

        public class UnusedStudentPlan : Marten.Linq.ICompiledQuery<Student>
        {
            public int Id { get; set; }
            public System.Linq.Expressions.Expression<System.Func<Marten.Linq.IMartenQueryable<Student>, Student>> QueryIs() =>
                query => query.First(student => student.Id == Id);
        }

        public class MultiDocumentPlan :
            Marten.Linq.ICompiledQuery<Student>,
            Marten.Linq.ICompiledQuery<OtherStudent>
        {
            public int this[int index] => index;

            System.Linq.Expressions.Expression<System.Func<Marten.Linq.IMartenQueryable<Student>, Student>>
                Marten.Linq.ICompiledQuery<Student, Student>.QueryIs() => query => query.First();

            System.Linq.Expressions.Expression<System.Func<Marten.Linq.IMartenQueryable<OtherStudent>, OtherStudent>>
                Marten.Linq.ICompiledQuery<OtherStudent, OtherStudent>.QueryIs() => query => query.First();
        }

        public class GeneralPlan : Marten.IQueryPlan<StudentResult>;

        public static class StudentEndpoints
        {
            [Wolverine.Http.WolverineGet("/students/search")]
            public static async System.Threading.Tasks.Task<System.Collections.Generic.IReadOnlyList<StudentResult>> Search(
                string name,
                Marten.IQuerySession session)
            {
                return (await session.QueryAsync(new StudentsByName { Name = name })).ToArray();
            }

            [Wolverine.Http.WolverineGet("/students/first")]
            public static System.Threading.Tasks.Task<Student> First(Marten.Services.BatchQuerying.IBatchedQuery batch) =>
                batch.Query(new FirstStudent { Id = 42 });

            [Wolverine.Http.WolverineGet("/students/multi-document")]
            public static System.Threading.Tasks.Task<OtherStudent> MultiDocument(Marten.IQuerySession session) =>
                session.QueryAsync<OtherStudent, OtherStudent>(new MultiDocumentPlan());

            [Wolverine.Http.WolverineGet("/students/multi-document-student")]
            public static System.Threading.Tasks.Task<Student> MultiDocumentStudent(Marten.IQuerySession session) =>
                session.QueryAsync<Student, Student>(new MultiDocumentPlan());

            [Wolverine.Http.WolverineGet("/students/called-local")]
            public static System.Threading.Tasks.Task<Student> CalledLocal(Marten.IQuerySession session)
            {
                System.Threading.Tasks.Task<Student> Local() => session.QueryAsync(new FirstStudent { Id = 42 });
                return Local();
            }

            [Wolverine.Http.WolverineGet("/students/called-lambda")]
            public static System.Threading.Tasks.Task<Student> CalledLambda(Marten.IQuerySession session) =>
                ((System.Func<System.Threading.Tasks.Task<Student>>)(() => session.QueryAsync(new FirstStudent { Id = 42 })))();

            [Wolverine.Http.WolverineGet("/students/nested")]
            public static System.Threading.Tasks.Task<Student> Nested(Marten.IQuerySession session)
            {
                System.Threading.Tasks.Task<Student> Local() => session.QueryAsync(new FirstStudent { Id = 42 });
                System.Func<System.Threading.Tasks.Task<Student>> stored = () => session.QueryAsync(new FirstStudent { Id = 42 });
                _ = Local;
                _ = stored;
                return System.Threading.Tasks.Task.FromResult(new Student());
            }

            [Wolverine.Http.WolverineGet("/students/general-plan")]
            public static System.Threading.Tasks.Task<StudentResult> General(Marten.IQuerySession session) =>
                session.QueryByPlanAsync(new GeneralPlan());
        }

        public static class StudentHelper
        {
            public static System.Threading.Tasks.Task<Student> Execute(Marten.IQuerySession session) =>
                session.QueryAsync(new FirstStudent { Id = 42 });
        }

        public sealed class UnrelatedSession
        {
            public System.Threading.Tasks.Task<TOut> QueryAsync<TDoc, TOut>(Marten.Linq.ICompiledQuery<TDoc, TOut> query) =>
                System.Threading.Tasks.Task.FromResult(default(TOut)!);
        }

        public static class UnrelatedEndpoint
        {
            [Wolverine.Http.WolverineGet("/students/unrelated")]
            public static System.Threading.Tasks.Task<Student> Get(UnrelatedSession session) =>
                session.QueryAsync(new FirstStudent { Id = 42 });
        }
        """;

    const string GeneratedApplicationSource =
        """
        // <auto-generated/>
        namespace Students;

        public static class StudentsByName_CompiledQueryHandler
        {
            [Wolverine.Http.WolverineGet("/generated")]
            public static System.Threading.Tasks.Task<Student> Get(Marten.IQuerySession session) =>
                session.QueryAsync(new FirstStudent { Id = 42 });
        }
        """;

    static readonly IReadOnlyList<MetadataReference> _references =
    [
        .. ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")!)
            .Split(Path.PathSeparator)
            .Select(_ => MetadataReference.CreateFromFile(_))
    ];

    protected AdapterContribution Contribution = null!;

    void Establish()
    {
        var frameworkTree = CSharpSyntaxTree.ParseText(FrameworkSource, path: "/workspace/Framework.cs");
        var applicationTree = CSharpSyntaxTree.ParseText(ApplicationSource, path: "/workspace/Students/Students.cs");
        var compilation = CSharpCompilation.Create(
            "Students",
            [
                frameworkTree,
                applicationTree,
                CSharpSyntaxTree.ParseText(GeneratedApplicationSource, path: "/workspace/Students/Internal/Generated/StudentsByName.g.cs")
            ],
            _references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, nullableContextOptions: NullableContextOptions.Enable));
        var project = new DotNetProjectCompilation
        {
            Name = "Students",
            ProjectPath = "/workspace/Students/Students.csproj",
            SourceRoot = "/workspace",
            Compilation = compilation,
            AuthoredSyntaxTrees = new HashSet<SyntaxTree> { frameworkTree, applicationTree }
        };
        Contribution = new CritterStackScreenplayAdapter().Analyze(
            new DotNetAnalysisContext([project]),
            new DotNetAdapterOptions());
    }
}
