// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Cratis.CritterStack.Screenplay.for_MartenScreenplayAdapter.given;

public class a_marten_adapter_context : Specification
{
    const string FrameworkSource =
        """
        namespace Marten;
        public interface IDocumentSession
        {
            void Store<T>(T document);
        }
        """;
    const string ApplicationSource =
        """
        namespace Students;
        public sealed record Student(int Id, string Name);
        public static class StudentEndpoint
        {
            public static void Store(Student student, Marten.IDocumentSession session) => session.Store(student);
        }
        """;
    const string AggregationFrameworkSource =
        """
        namespace Marten.Events.Aggregation;
        public static class AggregateExtensions
        {
            public static void SelfAggregate<T>(this object options) { }
        }
        """;
    const string AggregationApplicationSource =
        """
        namespace Students;
        public sealed record Student(int Id, string Name);
        public static class StudentProjection
        {
            public static void Configure(object options) => Marten.Events.Aggregation.AggregateExtensions.SelfAggregate<Student>(options);
        }
        """;
    static readonly IReadOnlyList<MetadataReference> _references =
    [
        .. ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")!)
            .Split(Path.PathSeparator)
            .Select(path => MetadataReference.CreateFromFile(path))
    ];

    protected MartenScreenplayAdapter Adapter = null!;
    protected DotNetAnalysisContext Context = null!;

    void Establish()
    {
        Adapter = new();
        Context = CreateContext(stableSource: true, authoredUse: true);
    }

    protected static DotNetAnalysisContext CreateContext(bool stableSource, bool authoredUse) => CreateContext(
        FrameworkSource,
        authoredUse ? ApplicationSource : "namespace Students; public sealed record Student(int Id, string Name);",
        stableSource);

    protected static DotNetAnalysisContext CreateAggregationContext() => CreateContext(
        AggregationFrameworkSource,
        AggregationApplicationSource,
        stableSource: true);

    static DotNetAnalysisContext CreateContext(string frameworkSource, string applicationSource, bool stableSource)
    {
        var framework = CSharpSyntaxTree.ParseText(frameworkSource, path: "/checkout/Students/Framework.cs");
        var application = CSharpSyntaxTree.ParseText(applicationSource, path: "/checkout/Students/Student.cs");
        var compilation = CSharpCompilation.Create(
            "Students",
            [framework, application],
            _references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, nullableContextOptions: NullableContextOptions.Enable));
        var authoredTrees = new HashSet<SyntaxTree> { framework, application };
        var sourceContext = stableSource
            ? DotNetSourcePaths.Create(
                "Students/Students",
                new DotNetSourcePathPolicy
                {
                    DisplayRoot = DotNetSourceDisplayRoot.Workspace,
                    CasePolicy = DotNetSourcePathCasePolicy.Ordinal
                },
                [
                    new DotNetSourceDocument
                    {
                        SyntaxTree = framework,
                        ProjectRelativePath = "Framework.cs",
                        WorkspaceRelativePath = "Students/Framework.cs"
                    },
                    new DotNetSourceDocument
                    {
                        SyntaxTree = application,
                        ProjectRelativePath = "Student.cs",
                        WorkspaceRelativePath = "Students/Student.cs"
                    }
                ])
            : null;
        return new DotNetAnalysisContext(
        [
            new DotNetProjectCompilation
            {
                Name = "Students",
                Compilation = compilation,
                AuthoredSyntaxTrees = authoredTrees,
                SourceContext = sourceContext,
                SourceRoot = stableSource ? null : "/checkout"
            }
        ]);
    }
}
