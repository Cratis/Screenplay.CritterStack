// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Cratis.CritterStack.Screenplay.given;

public class an_enum_application : Specification
{
    const string FrameworkSource =
        """
        namespace Wolverine
        {
            public sealed class WolverineOptions;
        }
        namespace Marten
        {
            public sealed class StoreOptions
            {
                public void RegisterValueType<T>() { }
            }
            public interface IDocumentSession
            {
                void Store<T>(T document);
            }
        }
        """;

    protected const string EnumDeclaration = "public enum Priority : ulong { Urgent = ulong.MaxValue, Normal = 1, Alias = 1, @low = 0 }";
    protected DotNetProjectCompilation Project = null!;

    void Establish() => Project = CreateProject();

    protected static DotNetProjectCompilation CreateProject(
        string declaration = EnumDeclaration,
        string propertyType = "Priority",
        bool registerEnum = false,
        bool authoredEnum = true,
        string enumPath = "Priority.cs")
    {
        var framework = CSharpSyntaxTree.ParseText(FrameworkSource, path: "/workspace/Ordering/Framework.cs");
        var enumeration = CSharpSyntaxTree.ParseText($"namespace Ordering;\n{declaration}\npublic enum Unused {{ One, Two }}", path: $"/workspace/Ordering/{enumPath}");
        var application = CSharpSyntaxTree.ParseText(
            $$"""
            namespace Ordering.PlaceOrder;
            public sealed record PlaceOrder({{propertyType}} Priority);
            public sealed record Order(int Id, {{propertyType}} Priority);
            public sealed record OrderPlaced({{propertyType}} Priority);
            public static class PlaceOrderAggregateHandler
            {
                public static OrderPlaced Handle(PlaceOrder command, Order order, Marten.IDocumentSession session)
                {
                    session.Store(new Order(1, command.Priority));
                    return new(command.Priority);
                }
            }
            public static class Configuration
            {
                public static void Configure(Marten.StoreOptions options)
                {
                    {{(registerEnum ? "options.RegisterValueType<Priority>();" : string.Empty)}}
                }
            }
            """,
            path: "/workspace/Ordering/PlaceOrder/PlaceOrder.cs");
        var compilation = CSharpCompilation.Create(
            "Ordering",
            [framework, enumeration, application],
            ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")!).Split(Path.PathSeparator).Select(path => MetadataReference.CreateFromFile(path)),
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, nullableContextOptions: NullableContextOptions.Enable));
        compilation.GetDiagnostics().Where(_ => _.Severity == DiagnosticSeverity.Error).ShouldBeEmpty();
        SyntaxTree[] authored = authoredEnum ? [framework, enumeration, application] : [framework, application];

        return new DotNetProjectCompilation
        {
            Name = "Ordering",
            Compilation = compilation,
            SourceRoot = "/workspace",
            AuthoredSyntaxTrees = authored.ToHashSet<SyntaxTree>(),
            SourceContext = DotNetSourcePaths.Create(
                "Ordering/Ordering",
                new DotNetSourcePathPolicy { DisplayRoot = DotNetSourceDisplayRoot.Workspace, CasePolicy = DotNetSourcePathCasePolicy.Ordinal },
                [.. authored.Select(tree => new DotNetSourceDocument
                {
                    SyntaxTree = tree,
                    ProjectRelativePath = Path.GetRelativePath("/workspace/Ordering", tree.FilePath),
                    WorkspaceRelativePath = Path.GetRelativePath("/workspace", tree.FilePath)
                })])
        };
    }

    protected static GeneratedScreenplayDefinition Generate(DotNetProjectCompilation project) => new CritterStackScreenplayGenerator()
        .Generate([project], new CritterStackScreenplayOptions { Domain = "Ordering" });
}
