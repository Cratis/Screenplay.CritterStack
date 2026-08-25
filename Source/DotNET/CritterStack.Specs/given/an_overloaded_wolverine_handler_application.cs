// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Cratis.CritterStack.Screenplay.given;

public class an_overloaded_wolverine_handler_application : Specification
{
    const string FrameworkSource =
        """
        namespace Wolverine
        {
            public class WolverineOptions;
            public class OutgoingMessages : System.Collections.Generic.List<object>, Wolverine.Configuration.IWolverineReturnType;
        }

        namespace Wolverine.Configuration
        {
            public interface IWolverineReturnType;
        }
        """;

    const string ApplicationSource =
        """
        namespace OverloadedHandlers;

        public record FirstTrigger(string Id);
        public record SecondTrigger(string Id);
        public record FirstResult(string Id);
        public record SecondResult(string Id);

        public static class NotificationHandler
        {
            public static Wolverine.OutgoingMessages Handle(FirstTrigger message) => [new FirstResult(message.Id)];
            public static Wolverine.OutgoingMessages Handle(SecondTrigger message) => [new SecondResult(message.Id)];
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
            "OverloadedHandlers",
            [
                CSharpSyntaxTree.ParseText(FrameworkSource, path: "/workspace/Framework.cs"),
                CSharpSyntaxTree.ParseText(ApplicationSource, path: "/workspace/OverloadedHandlers/Handlers.cs")
            ],
            _references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, nullableContextOptions: NullableContextOptions.Enable));
        Project = new()
        {
            Name = "OverloadedHandlers",
            ProjectPath = "/workspace/OverloadedHandlers/OverloadedHandlers.csproj",
            SourceRoot = "/workspace",
            Compilation = compilation,
            AuthoredSyntaxTrees = compilation.SyntaxTrees.ToHashSet()
        };
        Contribution = new CritterStackScreenplayAdapter().Analyze(
            new DotNetAnalysisContext([Project]),
            new DotNetAdapterOptions());
    }
}
