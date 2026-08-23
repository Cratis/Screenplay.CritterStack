// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Text.Json;
using Cratis.CritterStack.Screenplay;
using Cratis.Screenplay.Generation;
using Cratis.Screenplay.Generation.DotNet;
using Microsoft.CodeAnalysis.CSharp;

var syntaxTree = CSharpSyntaxTree.ParseText("namespace PackageConsumer; public sealed class Marker;");
var compilation = CSharpCompilation.Create("PackageConsumer", [syntaxTree]);
var options = new CritterStackScreenplayOptions
{
    Domain = "PackageConsumer",
    Module = "Smoke",
    NamespaceSegmentsToSkip = 1
};
var project = new DotNetProjectCompilation
{
    Name = "PackageConsumer",
    SourceContext = DotNetSourcePaths.Create(
        "Integration/PackageConsumer/PackageConsumer",
        new DotNetSourcePathPolicy
        {
            Version = 1,
            DisplayRoot = DotNetSourceDisplayRoot.Workspace,
            CasePolicy = DotNetSourcePathCasePolicy.Ordinal
        },
        [
            new DotNetSourceDocument
            {
                SyntaxTree = syntaxTree,
                ProjectRelativePath = "Marker.cs",
                WorkspaceRelativePath = "Integration/PackageConsumer/Marker.cs"
            }
        ]),
    Compilation = compilation,
    AuthoredSyntaxTrees = new HashSet<Microsoft.CodeAnalysis.SyntaxTree> { syntaxTree }
};

var generator = new CritterStackScreenplayGenerator();
ICritterStackScreenplayGenerator generatorContract = generator;
var composedGenerator = new CritterStackScreenplayGenerator(
    new CritterStackScreenplayAdapter(),
    new ScreenplayDefinitionGenerator());
var adapterListGenerator = new CritterStackScreenplayGenerator([new CritterStackScreenplayAdapter()]);

AssertSuccess(generator.Generate(compilation, options));
AssertSuccess(generator.Generate([project], options));
AssertSuccess(generatorContract.Generate(compilation, options));
AssertSuccess(generatorContract.Generate([project], options));
AssertSuccess(composedGenerator.Generate(compilation, options));
AssertSuccess(composedGenerator.Generate([project], options));
AssertSuccess(adapterListGenerator.Generate(compilation, options));
AssertSuccess(adapterListGenerator.Generate([project], options));
AssertSourceContext(project, syntaxTree);
AssertDependencyGraph();

static void AssertSuccess(GeneratedScreenplayDefinition result)
{
    if (!result.IsSuccess || string.IsNullOrWhiteSpace(result.Source))
    {
        throw new InvalidOperationException("Critter Stack generation did not produce a verified Screenplay definition");
    }
}

static void AssertSourceContext(DotNetProjectCompilation project, Microsoft.CodeAnalysis.SyntaxTree syntaxTree)
{
    var range = DotNetSource.RangeForProject(syntaxTree.GetRoot().GetLocation(), project) ??
                throw new InvalidOperationException("The project-aware source range was not created");
    if (range.Path != "Integration/PackageConsumer/Marker.cs" ||
        range.FileIdentity != new SourceFileIdentity
        {
            Project = "Integration/PackageConsumer/PackageConsumer",
            Path = "Marker.cs"
        })
    {
        throw new InvalidOperationException("The project-aware source range did not preserve display path and stable identity");
    }
}

static void AssertDependencyGraph()
{
    var dependencyFile = Path.Combine(AppContext.BaseDirectory, "PackageConsumer.deps.json");
    using var document = JsonDocument.Parse(File.ReadAllText(dependencyFile));
    var libraries = document.RootElement.GetProperty("libraries");

    AssertPackage(libraries, "Cratis.Screenplay.Generation.Contracts/0.9.0");
    AssertPackage(libraries, "Cratis.Screenplay.Generation/0.9.0");
    AssertPackage(libraries, "Cratis.Screenplay.Generation.DotNet/0.9.0");
    AssertPackage(libraries, "Cratis.Screenplay.Generation.DotNet.Vogen/0.9.0");

    if (libraries.EnumerateObject().Any(_ => _.Name.StartsWith("Vogen/", StringComparison.Ordinal)))
    {
        throw new InvalidOperationException("The generator facade must not bring the Vogen source-generator/runtime package into consumers");
    }
}

static void AssertPackage(JsonElement libraries, string package)
{
    if (!libraries.TryGetProperty(package, out _))
    {
        throw new InvalidOperationException($"The clean consumer did not resolve required package '{package}'");
    }
}
