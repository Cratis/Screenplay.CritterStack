// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Cratis.CritterStack.Screenplay.given;

public class a_handler_chain_configuration_application : Specification
{
    const string FrameworkSource =
        """
        namespace Wolverine
        {
            public class WolverineOptions;
        }

        namespace Wolverine.Runtime.Handlers
        {
            public class HandlerChain;
        }
        """;

    const string ApplicationSource =
        """
        namespace HandlerChainConfiguration;

        public record RetryTrigger(string Id);

        public static partial class RetryTriggerHandler
        {
            public static void Handle(RetryTrigger message) { }
        }
        """;

    const string ConfigurationSource =
        """
        namespace HandlerChainConfiguration;

        public static partial class RetryTriggerHandler
        {
            public static void Configure(Wolverine.Runtime.Handlers.HandlerChain chain) { }
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
        BaselineProject = CreateProject(includeConfiguration: false);
        Project = CreateProject(includeConfiguration: true);
        var adapter = new CritterStackScreenplayAdapter();
        BaselineContribution = adapter.Analyze(new DotNetAnalysisContext([BaselineProject]), new DotNetAdapterOptions());
        Contribution = adapter.Analyze(new DotNetAnalysisContext([Project]), new DotNetAdapterOptions());
    }

    static DotNetProjectCompilation CreateProject(bool includeConfiguration)
    {
        var trees = new List<SyntaxTree>
        {
            CSharpSyntaxTree.ParseText(FrameworkSource, path: "/workspace/Framework.cs"),
            CSharpSyntaxTree.ParseText(ApplicationSource, path: "/workspace/HandlerChainConfiguration/Handler.cs")
        };
        if (includeConfiguration)
        {
            trees.Add(CSharpSyntaxTree.ParseText(ConfigurationSource, path: "/workspace/HandlerChainConfiguration/Configuration.cs"));
        }

        var compilation = CSharpCompilation.Create(
            "HandlerChainConfiguration",
            trees,
            _references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, nullableContextOptions: NullableContextOptions.Enable));
        return new()
        {
            Name = "HandlerChainConfiguration",
            ProjectPath = "/workspace/HandlerChainConfiguration/HandlerChainConfiguration.csproj",
            SourceRoot = "/workspace",
            Compilation = compilation,
            AuthoredSyntaxTrees = compilation.SyntaxTrees.ToHashSet()
        };
    }
}
