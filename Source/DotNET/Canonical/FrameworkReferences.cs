// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Runtime.InteropServices;
using Microsoft.CodeAnalysis;

namespace Cratis.CritterStack.Screenplay.Canonical;

static class FrameworkReferences
{
    static readonly string[] _packs = ["Microsoft.NETCore.App.Ref", "Microsoft.AspNetCore.App.Ref"];

    public static Compilation AddMissingTo(Project project, Compilation compilation)
    {
        if (compilation.GetSpecialType(SpecialType.System_Object).TypeKind != TypeKind.Error)
        {
            return compilation;
        }

        var targetFramework = TargetFrameworkOf(project);
        if (targetFramework is null)
        {
            return compilation;
        }

        var existing = compilation.References
            .Select(_ => Path.GetFileNameWithoutExtension(_.Display))
            .Where(_ => !string.IsNullOrEmpty(_))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var references = PackRoots()
            .SelectMany(root => _packs.Select(pack => Path.Combine(root, pack)))
            .Where(Directory.Exists)
            .Select(pack => ReferenceDirectory(pack, targetFramework))
            .Where(_ => _ is not null)
            .SelectMany(_ => Directory.EnumerateFiles(_!, "*.dll", SearchOption.TopDirectoryOnly))
            .Where(_ => existing.Add(Path.GetFileNameWithoutExtension(_)))
            .Select(_ => MetadataReference.CreateFromFile(_))
            .ToArray();

        return references.Length == 0 ? compilation : compilation.AddReferences(references);
    }

    static string? TargetFrameworkOf(Project project)
    {
        var assemblyPath = project.CompilationOutputInfo.AssemblyPath;
        if (!string.IsNullOrWhiteSpace(assemblyPath))
        {
            var framework = new DirectoryInfo(Path.GetDirectoryName(assemblyPath)!).Name;
            if (framework.StartsWith("net", StringComparison.OrdinalIgnoreCase))
            {
                return framework;
            }
        }

        return null;
    }

    static IEnumerable<string> PackRoots()
    {
        var runtime = new DirectoryInfo(RuntimeEnvironment.GetRuntimeDirectory());
        var dotnetRoot = runtime.Parent?.Parent?.Parent;
        if (dotnetRoot is not null)
        {
            yield return Path.Combine(dotnetRoot.FullName, "packs");
        }

        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        if (!string.IsNullOrWhiteSpace(home))
        {
            yield return Path.Combine(home, ".nuget", "packages");
        }
    }

    static string? ReferenceDirectory(string pack, string targetFramework) => Directory.EnumerateDirectories(pack)
        .Select(version => Path.Combine(version, "ref", targetFramework))
        .Where(Directory.Exists)
        .OrderDescending(StringComparer.OrdinalIgnoreCase)
        .FirstOrDefault();
}
