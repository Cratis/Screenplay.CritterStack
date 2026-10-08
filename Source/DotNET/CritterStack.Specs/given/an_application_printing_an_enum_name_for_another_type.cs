// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Cratis.CritterStack.Screenplay.given;

public class an_application_printing_an_enum_name_for_another_type : Specification
{
    protected const string Frameworks =
        """
        namespace Wolverine { public sealed class WolverineOptions; }
        namespace Marten
        {
            public sealed class StoreOptions;
            public interface IDocumentSession { void Store<T>(T document); }
        }
        """;

    protected DotNetProjectCompilation Project { get; private set; } = null!;

    protected void UseApplication(string source, string? generated = null)
    {
        var application = CSharpSyntaxTree.ParseText($"{Frameworks}{Environment.NewLine}{source}", path: "/workspace/Ordering/Application.cs");
        var trees = generated is null
            ? [application]
            : new[] { application, CSharpSyntaxTree.ParseText(generated, path: "/workspace/Ordering/Generated.g.cs") };
        Project = new DotNetProjectCompilation
        {
            Name = "Ordering",
            ProjectPath = "/workspace/Ordering/Ordering.csproj",
            SourceRoot = "/workspace",
            Compilation = CSharpCompilation.Create("Ordering", trees, ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")!).Split(Path.PathSeparator).Select(path => MetadataReference.CreateFromFile(path)), new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary)),
            AuthoredSyntaxTrees = new HashSet<SyntaxTree>(trees)
        };
    }
}
