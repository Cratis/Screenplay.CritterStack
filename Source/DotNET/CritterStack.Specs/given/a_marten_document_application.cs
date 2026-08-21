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
            public class StoreOptions;
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

        public class Student
        {
            public int Id { get; set; }
            public string Name { get; set; } = string.Empty;
        }

        public static class StudentEndpoints
        {
            public static void Store(Student student, Marten.IDocumentSession session) => session.Store(student);
            public static void Delete(Student student, Marten.IDocumentSession session) => session.Delete(student);
            public static System.Linq.IQueryable<Student> Query(Marten.IQuerySession session) => session.Query<Student>();
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
