// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;
using Cratis.Screenplay.Generation.DotNet;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.MSBuild;

namespace Cratis.CritterStack.Screenplay.Canonical;

static class CanonicalRunner
{
    [MethodImpl(MethodImplOptions.NoInlining)]
    public static async Task<int> Run(string projectPath, string expectedPath, string outputPath)
    {
        projectPath = Path.GetFullPath(projectPath);
        using var workspace = MSBuildWorkspace.Create();
        var failures = new List<string>();
        using var subscription = workspace.RegisterWorkspaceFailedHandler(args => failures.Add(args.Diagnostic.Message));
        var rootProject = await workspace.OpenProjectAsync(projectPath);
        foreach (var failure in failures)
        {
            await Console.Error.WriteLineAsync($"workspace: {failure}");
        }

        // Canonical applications are selected by their host project. Framework source projects referenced by the
        // upstream sample remain metadata in that host compilation and must not be interpreted as application code.
        ProjectId[] projectIds = [rootProject.Id];
        var sourceRoot = FindRepositoryRoot(Path.GetDirectoryName(projectPath)!);
        var projects = new List<DotNetProjectCompilation>();
        foreach (var projectId in projectIds)
        {
            var project = rootProject.Solution.GetProject(projectId)!;
            if (project.Language != LanguageNames.CSharp || IsSpecProject(project.Name))
            {
                continue;
            }

            var compilation = await project.GetCompilationAsync();
            if (compilation is null)
            {
                await Console.Error.WriteLineAsync($"No compilation for {project.Name}");
                return 3;
            }

            compilation = FrameworkReferences.AddMissingTo(project, compilation);
            var compilationErrors = compilation.GetDiagnostics().Where(_ => _.Severity == DiagnosticSeverity.Error).ToArray();
            foreach (var diagnostic in compilationErrors)
            {
                await Console.Error.WriteLineAsync($"compilation: {project.Name}: {diagnostic}");
            }
            if (compilationErrors.Length > 0)
            {
                return 3;
            }

            projects.Add(new DotNetProjectCompilation
            {
                Name = project.Name,
                ProjectPath = project.FilePath,
                SourceRoot = sourceRoot,
                Compilation = compilation
            });
        }

        var result = new CritterStackScreenplayGenerator().Generate(
            projects,
            new CritterStackScreenplayOptions { Domain = rootProject.Name });
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(outputPath))!);
        await File.WriteAllTextAsync(outputPath, result.Source);

        var unmet = Expectations.Read(expectedPath).Where(_ => !_.IsMetBy(result)).ToArray();
        foreach (var diagnostic in result.Diagnostics)
        {
            await Console.Out.WriteLineAsync($"{diagnostic.Severity} {diagnostic.Code}: {diagnostic.Message}");
        }
        foreach (var expectation in unmet)
        {
            await Console.Error.WriteLineAsync($"Unmet: {expectation}");
        }
        if (unmet.Length > 0 || !result.IsSuccess)
        {
            foreach (var artifact in result.Graph.Artifacts)
            {
                await Console.Error.WriteLineAsync($"Artifact: {artifact.Key.Subject.Value} [{artifact.Key.Kind}] {artifact.Variants[0].Definition.Name}");
            }
            foreach (var placement in result.Graph.Placements)
            {
                var slice = placement.EffectiveVariants.Count == 0
                    ? "conflicted"
                    : placement.EffectiveVariants[0].Placement.Slice;
                await Console.Error.WriteLineAsync($"Placement: {placement.Artifact.Subject.Value} [{placement.Artifact.Kind}] {slice}");
            }
        }

        if (!result.IsSuccess)
        {
            return 4;
        }

        return unmet.Length == 0 ? 0 : 5;
    }

    static bool IsSpecProject(string name) => name.EndsWith("Tests", StringComparison.OrdinalIgnoreCase) ||
                                               name.EndsWith("Specs", StringComparison.OrdinalIgnoreCase);

    static string FindRepositoryRoot(string path)
    {
        for (var current = new DirectoryInfo(path); current is not null; current = current.Parent)
        {
            if (Directory.Exists(Path.Combine(current.FullName, ".git")) ||
                File.Exists(Path.Combine(current.FullName, ".git")))
            {
                return current.FullName;
            }
        }

        return path;
    }
}
