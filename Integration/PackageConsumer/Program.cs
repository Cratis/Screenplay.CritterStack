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
    Compilation = compilation,
    AuthoredSyntaxTrees = new HashSet<Microsoft.CodeAnalysis.SyntaxTree> { syntaxTree }
};

var generator = new CritterStackScreenplayGenerator();
ICritterStackScreenplayGenerator generatorContract = generator;
var composedGenerator = new CritterStackScreenplayGenerator(
    new CritterStackScreenplayAdapter(),
    new ScreenplayDefinitionGenerator());

AssertSuccess(generator.Generate(compilation, options));
AssertSuccess(generator.Generate([project], options));
AssertSuccess(generatorContract.Generate(compilation, options));
AssertSuccess(generatorContract.Generate([project], options));
AssertSuccess(composedGenerator.Generate(compilation, options));
AssertSuccess(composedGenerator.Generate([project], options));
AssertDependencyGraph();

static void AssertSuccess(GeneratedScreenplayDefinition result)
{
    if (!result.IsSuccess || string.IsNullOrWhiteSpace(result.Source))
    {
        throw new InvalidOperationException("Critter Stack generation did not produce a verified Screenplay definition");
    }
}

static void AssertDependencyGraph()
{
    var dependencyFile = Path.Combine(AppContext.BaseDirectory, "PackageConsumer.deps.json");
    using var document = JsonDocument.Parse(File.ReadAllText(dependencyFile));
    var libraries = document.RootElement.GetProperty("libraries");

    AssertPackage(libraries, "Cratis.Screenplay.Generation.Contracts/0.6.1");
    AssertPackage(libraries, "Cratis.Screenplay.Generation/0.6.1");
    AssertPackage(libraries, "Cratis.Screenplay.Generation.DotNet/0.6.1");

    if (libraries.EnumerateObject().Any(_ => _.Name.StartsWith("Cratis.Screenplay.Generation.DotNet.Vogen/", StringComparison.Ordinal)))
    {
        throw new InvalidOperationException("The Critter Stack package must not compose or depend on the Vogen adapter");
    }
}

static void AssertPackage(JsonElement libraries, string package)
{
    if (!libraries.TryGetProperty(package, out _))
    {
        throw new InvalidOperationException($"The clean consumer did not resolve required package '{package}'");
    }
}
