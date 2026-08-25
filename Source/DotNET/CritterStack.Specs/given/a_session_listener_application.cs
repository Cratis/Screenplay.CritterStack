// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Cratis.CritterStack.Screenplay.given;

public class a_session_listener_application : Specification
{
    const string FrameworkSource =
        """
        namespace Marten
        {
            public interface IDocumentSessionListener;
            public interface IChangeListener;

            public class StoreOptions
            {
                public ListenerCollection Listeners { get; } = new();
            }

            public class ListenerCollection
            {
                public void Add(IDocumentSessionListener listener) { }
            }
        }
        """;

    const string ApplicationSource =
        """
        namespace SessionListeners;

        public class ApplicationMarker;
        """;

    const string ListenerSource =
        """
        namespace SessionListeners;

        public class CommitListener : Marten.IDocumentSessionListener, Marten.IChangeListener;

        public static class Configuration
        {
            public static void Configure(Marten.StoreOptions options) =>
                options.Listeners.Add(new CommitListener());
        }
        """;

    static readonly IReadOnlyList<MetadataReference> _references =
    [
        .. ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")!)
            .Split(Path.PathSeparator)
            .Select(_ => MetadataReference.CreateFromFile(_))
    ];

    protected DotNetProjectCompilation BaselineProject = null!;
    protected DotNetProjectCompilation Project = null!;
    protected AdapterContribution BaselineContribution = null!;
    protected AdapterContribution Contribution = null!;

    void Establish()
    {
        BaselineProject = CreateProject(includeListener: false);
        Project = CreateProject(includeListener: true);
        var adapter = new CritterStackScreenplayAdapter();
        BaselineContribution = adapter.Analyze(new DotNetAnalysisContext([BaselineProject]), new DotNetAdapterOptions());
        Contribution = adapter.Analyze(new DotNetAnalysisContext([Project]), new DotNetAdapterOptions());
    }

    static DotNetProjectCompilation CreateProject(bool includeListener)
    {
        var trees = new List<SyntaxTree>
        {
            CSharpSyntaxTree.ParseText(FrameworkSource, path: "/workspace/Framework.cs"),
            CSharpSyntaxTree.ParseText(ApplicationSource, path: "/workspace/SessionListeners/Application.cs")
        };
        if (includeListener)
        {
            trees.Add(CSharpSyntaxTree.ParseText(ListenerSource, path: "/workspace/SessionListeners/Listener.cs"));
        }

        var compilation = CSharpCompilation.Create(
            "SessionListeners",
            trees,
            _references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, nullableContextOptions: NullableContextOptions.Enable));
        return new()
        {
            Name = "SessionListeners",
            ProjectPath = "/workspace/SessionListeners/SessionListeners.csproj",
            SourceRoot = "/workspace",
            Compilation = compilation,
            AuthoredSyntaxTrees = compilation.SyntaxTrees.ToHashSet()
        };
    }
}
