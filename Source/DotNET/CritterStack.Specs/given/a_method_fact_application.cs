// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Operations;

namespace Cratis.CritterStack.Screenplay.given;

public class a_method_fact_application : Specification
{
    const string Source =
        """
        namespace Methods;

        public static class Handler
        {
            public static void Handle(int value)
            {
                void Save() { }
                System.Action first = () =>
                {
                    void Nested() { }
                    Nested();
                };
                System.Action second = () => { };
                Save();
                first();
                second();
            }

            public static void Handle(string value)
            {
                void Save() { }
                Save();
            }
        }
        """;

    static readonly IReadOnlyList<MetadataReference> _references =
    [
        .. ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES"))
            .Split(Path.PathSeparator)
            .Select(path => MetadataReference.CreateFromFile(path))
    ];

    protected DotNetProjectCompilation Project = null!;
    protected IMethodSymbol IntegerHandler = null!;
    protected IMethodSymbol StringHandler = null!;
    protected IMethodSymbol[] Locals = [];
    protected IMethodSymbol[] Lambdas = [];

    void Establish()
    {
        var tree = CSharpSyntaxTree.ParseText(Source, path: "/workspace/Methods/Handler.cs");
        var compilation = CSharpCompilation.Create(
            "Methods",
            [tree],
            _references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
        Project = new()
        {
            Name = "Methods",
            ProjectPath = "/workspace/Methods/Methods.csproj",
            SourceRoot = "/workspace",
            Compilation = compilation,
            AuthoredSyntaxTrees = new HashSet<SyntaxTree> { tree }
        };
        var model = compilation.GetSemanticModel(tree);
        var nodes = tree.GetRoot().DescendantNodes().ToArray();
        var handlers = compilation.GetTypeByMetadataName("Methods.Handler").GetMembers("Handle").OfType<IMethodSymbol>().ToArray();
        IntegerHandler = handlers.Single(method => method.Parameters[0].Type.SpecialType == SpecialType.System_Int32);
        StringHandler = handlers.Single(method => method.Parameters[0].Type.SpecialType == SpecialType.System_String);
        Locals = [.. nodes.OfType<LocalFunctionStatementSyntax>().Select(node => model.GetDeclaredSymbol(node))];
        Lambdas = [.. nodes.OfType<LambdaExpressionSyntax>().Select(node => ((IAnonymousFunctionOperation)model.GetOperation(node)).Symbol)];
    }
}
