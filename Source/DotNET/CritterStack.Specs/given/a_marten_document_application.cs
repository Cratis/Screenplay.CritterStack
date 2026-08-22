// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Cratis.CritterStack.Screenplay.given;

public class a_marten_document_application : Specification
{
    const string FrameworkSource =
        """
        namespace Marten
        {
            public interface IDocumentStore;
            public class StoreOptions
            {
                public MartenRegistry Schema { get; } = new();
            }
            public class MartenRegistry
            {
                public DocumentMappingExpression<T> For<T>() => new();

                public class DocumentMappingExpression<T>
                {
                    public DocumentMappingExpression<T> Identity(System.Linq.Expressions.Expression<System.Func<T, object>> member) => this;
                }
            }
            public interface IDocumentSession
            {
                void Store<T>(T document);
                void Delete<T>(T document);
            }
            public interface IQuerySession
            {
                System.Linq.IQueryable<T> Query<T>();
            }
        }
        """;

    const string ApplicationSource =
        """
        namespace Students;

        public abstract class StudentDocument
        {
            public int StudentNumber { get; set; }
        }

        public class Student : StudentDocument
        {
            public int Id { get; set; }
            public string Name { get; set; } = string.Empty;
        }

        public class UnresolvedStudent
        {
            public int CandidateKey { get; set; }
            public string Name { get; set; } = string.Empty;
        }

        public abstract class ShadowedStudentDocument
        {
            public int StudentNumber { get; set; }
        }

        public class ShadowedStudent : ShadowedStudentDocument
        {
            public new int StudentNumber { get; set; }
            public string Name { get; set; } = string.Empty;
        }

        public static class StudentEndpoints
        {
            public static void Store(Student student, Marten.IDocumentSession session) => session.Store(student);
            public static void Delete(Student student, Marten.IDocumentSession session) => session.Delete(student);
            public static System.Linq.IQueryable<Student> Query(Marten.IQuerySession session) => session.Query<Student>();
        }

        public static class Configuration
        {
            public static void Configure(Marten.StoreOptions options)
            {
                options.Schema.For<Student>().Identity(student => student.StudentNumber);
                options.Schema.For<UnresolvedStudent>().Identity(student => student.Name.ToUpperInvariant());
                options.Schema.For<ShadowedStudent>().Identity(student => ((ShadowedStudentDocument)student).StudentNumber);
            }
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
        var compilation = CSharpCompilation.Create(
            "Students",
            [
                CSharpSyntaxTree.ParseText(FrameworkSource, path: "/workspace/Framework.cs"),
                CSharpSyntaxTree.ParseText(ApplicationSource, path: "/workspace/Students/Students.cs")
            ],
            _references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, nullableContextOptions: NullableContextOptions.Enable));
        var project = new DotNetProjectCompilation
        {
            Name = "Students",
            ProjectPath = "/workspace/Students/Students.csproj",
            SourceRoot = "/workspace",
            Compilation = compilation
        };
        Contribution = new CritterStackScreenplayAdapter().Analyze(
            new DotNetAnalysisContext([project]),
            new DotNetAdapterOptions());
    }
}
