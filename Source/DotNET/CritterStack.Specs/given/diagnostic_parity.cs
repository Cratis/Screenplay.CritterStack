// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Reflection;
using System.Text.Json;

namespace Cratis.CritterStack.Screenplay.given;

/// <summary>
/// Contract vectors for diagnostic transport, not an implementation of the unfinished atomic readers.
/// Only producer lineage is excluded from diagnostic comparisons.
/// </summary>
static class diagnostic_parity
{
    static readonly MetadataReference[] _references =
    [
        .. ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES"))
            .Split(Path.PathSeparator).Select(path => MetadataReference.CreateFromFile(path))
    ];

    public static DotNetProjectCompilation ProjectFrom(Type fixture, params string[] fields)
    {
        var sources = SourceFields(fixture).DistinctBy(field => field.Name)
            .Where(field => field.FieldType == typeof(string) && IsSource(field.Name, fields))
            .Select(field => (field.Name, Text: ReadSource(field.GetValue(null))))
            .Concat(SourceProperties(fixture, fields))
            .Select(source => CSharpSyntaxTree.ParseText(source.Text, path: $"/workspace/Diagnostics/{source.Name}.cs"))
            .ToArray();
        if (sources.Length == 0) throw new InvalidOperationException($"Fixture '{fixture.Name}' has no selected source constants");
        var compilation = CSharpCompilation.Create("Diagnostics", sources, _references, new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
        return new DotNetProjectCompilation
        {
            Name = "Diagnostics",
            ProjectPath = "/workspace/Diagnostics/Diagnostics.csproj",
            SourceRoot = "/workspace",
            Compilation = compilation,
            AuthoredSyntaxTrees = sources.ToHashSet()
        };
    }

    public static DotNetProjectCompilation ProjectWithSource(Type fixture, string source)
    {
        var framework = ProjectFrom(fixture, "FrameworkSource");
        var tree = CSharpSyntaxTree.ParseText(source, path: "/workspace/Diagnostics/ApplicationSource.cs");
        return new DotNetProjectCompilation
        {
            Name = framework.Name, ProjectPath = framework.ProjectPath, SourceRoot = framework.SourceRoot,
            Compilation = framework.Compilation.AddSyntaxTrees(tree), AuthoredSyntaxTrees = framework.AuthoredSyntaxTrees.Append(tree).ToHashSet()
        };
    }

    public static string[] Meaning(IEnumerable<GenerationDiagnostic> diagnostics) =>
    [
        .. diagnostics.Select(diagnostic => JsonSerializer.Serialize(new
        {
            diagnostic.Code, diagnostic.Severity, diagnostic.Outcome, diagnostic.Message, diagnostic.Source, diagnostic.Subject
        })).Order(StringComparer.Ordinal)
    ];

    public static GeneratedScreenplayDefinition Generate(params AdapterContribution[] contributions) => GenerateIn("Diagnostics", contributions);

    public static GeneratedScreenplayDefinition GenerateIn(string domain, params AdapterContribution[] contributions) =>
        new ScreenplayDefinitionGenerator().Generate(contributions, new ScreenplayGenerationOptions { Domain = domain });

    static IEnumerable<FieldInfo> SourceFields(Type fixture)
    {
        for (var type = fixture; type != typeof(Specification) && type is not null; type = type.BaseType)
        {
            foreach (var field in type.GetFields(BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.DeclaredOnly))
            {
                yield return field;
            }
        }
    }

    static IEnumerable<(string Name, string Text)> SourceProperties(Type fixture, string[] selected)
    {
        var properties = fixture.GetProperties(BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public)
            .Where(property => property.PropertyType == typeof(string) && IsSource(property.Name, selected)).ToArray();
        if (properties.Length == 0) return [];
        var instance = Activator.CreateInstance(fixture);
        return properties.Select(property => (property.Name, ReadSource(property.GetValue(instance))));
    }

    static string ReadSource(object? value) => value as string ?? throw new InvalidOperationException("Fixture source must be a non-null string");

    static bool IsSource(string name, string[] selected)
    {
        if (!name.EndsWith("Source", StringComparison.Ordinal)) return false;
        if (selected.Length > 0) return selected.Contains(name);
        return !name.Contains("Generated", StringComparison.Ordinal) && !name.Contains("NonAuthored", StringComparison.Ordinal);
    }

    public static AdapterContribution Reown(AdapterContribution contribution, string owner)
    {
        var adapter = Identity(owner);
        return contribution with
        {
            Adapter = adapter,
            Facts = [.. contribution.Facts.Select(fact => fact with
            {
                Id = new FactId { Value = $"{owner}:{fact.Id.Value}" }, Evidence = fact.Evidence with { Adapter = adapter }
            })],
            Diagnostics = [.. contribution.Diagnostics.Select(diagnostic => diagnostic with
            {
                Facts = diagnostic.Facts.IsDefaultOrEmpty ? [] : [.. diagnostic.Facts.Select(id => new FactId { Value = $"{owner}:{id.Value}" })]
            })]
        };
    }

    public static AdapterIdentity Identity(string id) => new() { Id = id, Version = "1.0.0" };

    public static Evidence Evidence(AdapterIdentity adapter, SourceRange? source = null) => new()
    {
        Adapter = adapter, Strength = EvidenceStrength.Exact, Source = source
    };
}
