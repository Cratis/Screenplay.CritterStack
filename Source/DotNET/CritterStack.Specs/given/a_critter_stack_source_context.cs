// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Cratis.CritterStack.Screenplay.given;

public class a_critter_stack_source_context : Specification
{
    static readonly Type _critterStackSource = typeof(CritterStackScreenplayAdapter).Assembly
        .GetType("Cratis.CritterStack.Screenplay.CritterStackSource", throwOnError: true)!;
    static readonly IReadOnlyList<MetadataReference> _references =
    [
        .. ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")!)
            .Split(Path.PathSeparator)
            .Select(_ => MetadataReference.CreateFromFile(_))
    ];

    protected static SyntaxTree SourceTree(string source, string path) =>
        CSharpSyntaxTree.ParseText(source, path: path);

    protected static CSharpCompilation SourceCompilation(
        string name,
        IEnumerable<SyntaxTree> syntaxTrees,
        params MetadataReference[] additionalReferences) => CSharpCompilation.Create(
            name,
            syntaxTrees,
            [.. _references, .. additionalReferences],
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

    protected static DotNetProjectCompilation ReferencingProject(
        CSharpCompilation referencedCompilation,
        string? sourceRoot,
        string checkoutRoot = "/checkout",
        IEnumerable<SyntaxTree>? additionalAuthoredTrees = null)
    {
        var applicationTree = SourceTree(
            "namespace Application; public sealed class Marker;",
            $"{checkoutRoot}/Application/Marker.cs");
        var compilation = SourceCompilation(
            "Application",
            [applicationTree],
            referencedCompilation.ToMetadataReference());

        return new()
        {
            Name = "Application",
            ProjectPath = $"{checkoutRoot}/Application/Application.csproj",
            SourceRoot = sourceRoot,
            Compilation = compilation,
            AuthoredSyntaxTrees = new HashSet<SyntaxTree>([applicationTree, .. additionalAuthoredTrees ?? []])
        };
    }

    protected static INamedTypeSymbol ReferencedType(DotNetProjectCompilation project, string metadataName) =>
        project.Compilation.GetTypeByMetadataName(metadataName)!;

    protected static SourceRange? RangeForProject(Location location, DotNetProjectCompilation project) =>
        (SourceRange?)_critterStackSource
            .GetMethod("RangeForProject")!
            .Invoke(null, [location, project]);

    protected static Evidence EvidenceFor(ISymbol symbol, DotNetProjectCompilation project) =>
        (Evidence)_critterStackSource
            .GetMethod("EvidenceFor")!
            .Invoke(
                null,
                [
                    symbol,
                    new AdapterIdentity { Id = "spec", Version = "1" },
                    project,
                    EvidenceStrength.Exact,
                    null
                ])!;
}
