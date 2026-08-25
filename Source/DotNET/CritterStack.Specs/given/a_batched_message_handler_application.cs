// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Cratis.CritterStack.Screenplay.given;

public class a_batched_message_handler_application : Specification
{
    const string FrameworkSource =
        """
        namespace Wolverine
        {
            public class WolverineOptions;
            public interface IWolverineHandler;
        }
        """;

    const string ApplicationSource =
        """
        namespace BatchedMessageHandlers;

        public record ServiceUpdates(string Id);

        public static class ServiceUpdatesBatchHandler
        {
            public static void Handle(ServiceUpdates[] batch) { }
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
            "BatchedMessageHandlers",
            [
                CSharpSyntaxTree.ParseText(FrameworkSource, path: "/workspace/Framework.cs"),
                CSharpSyntaxTree.ParseText(ApplicationSource, path: "/workspace/BatchedMessageHandlers/Handlers.cs")
            ],
            _references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, nullableContextOptions: NullableContextOptions.Enable));
        Project = new()
        {
            Name = "BatchedMessageHandlers",
            ProjectPath = "/workspace/BatchedMessageHandlers/BatchedMessageHandlers.csproj",
            SourceRoot = "/workspace",
            Compilation = compilation,
            AuthoredSyntaxTrees = compilation.SyntaxTrees.ToHashSet()
        };
        Contribution = new CritterStackScreenplayAdapter().Analyze(
            new DotNetAnalysisContext([Project]),
            new DotNetAdapterOptions());
    }
}
