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
        var sourceRoot = FindRepositoryRoot(Path.GetDirectoryName(projectPath)!);
        using var workspace = MSBuildWorkspace.Create();
        var failures = new List<WorkspaceDiagnostic>();
        using var subscription = workspace.RegisterWorkspaceFailedHandler(args => failures.Add(args.Diagnostic));
        var rootProject = await workspace.OpenProjectAsync(projectPath);
        foreach (var failure in failures)
        {
            await Console.Error.WriteLineAsync(OperationalWorkspaceDiagnostic(failure, sourceRoot));
        }

        // Canonical applications are selected by their host project. Framework source projects referenced by the
        // upstream sample remain metadata in that host compilation and must not be interpreted as application code.
        ProjectId[] projectIds = [rootProject.Id];
        var projects = new List<DotNetProjectCompilation>();
        foreach (var projectId in projectIds)
        {
            var project = rootProject.Solution.GetProject(projectId)!;
            if (project.Language != LanguageNames.CSharp || IsSpecProject(project.Name))
            {
                continue;
            }

            var projectFilePath = project.FilePath ?? throw new InvalidDotNetProjectIdentity(project.Name);
            var projectDirectory = Path.GetDirectoryName(projectFilePath)!;
            var authoredDocuments = await Task.WhenAll(project.Documents.Select(async document =>
            {
                var syntaxTree = await document.GetSyntaxTreeAsync() ??
                                 throw new DotNetSourceTreeNotMapped(document.FilePath ?? document.Name);
                var documentPath = document.FilePath ?? throw new InvalidDotNetSourcePath(document.Name);
                return new DotNetSourceDocument
                {
                    SyntaxTree = syntaxTree,
                    ProjectRelativePath = PortableRelativePath(projectDirectory, documentPath),
                    WorkspaceRelativePath = PortableRelativePath(sourceRoot, documentPath)
                };
            }));
            var authoredSyntaxTrees = authoredDocuments.Select(_ => _.SyntaxTree).ToHashSet();
            var sourceContext = DotNetSourcePaths.Create(
                ProjectIdentityFor(sourceRoot, projectFilePath),
                new DotNetSourcePathPolicy
                {
                    Version = 1,
                    DisplayRoot = DotNetSourceDisplayRoot.Workspace,
                    CasePolicy = DotNetSourcePathCasePolicy.Ordinal
                },
                authoredDocuments);
            await Console.Out.WriteLineAsync(SourceContextPolicyLine(sourceContext));

            var compilation = await project.GetCompilationAsync();
            if (compilation is null)
            {
                await Console.Error.WriteLineAsync($"operational-compilation: project={project.Name} error=no-compilation");
                return 3;
            }

            compilation = FrameworkReferences.AddMissingTo(project, compilation);
            var compilationErrors = compilation.GetDiagnostics().Where(_ => _.Severity == DiagnosticSeverity.Error).ToArray();
            foreach (var diagnostic in compilationErrors)
            {
                await Console.Error.WriteLineAsync(OperationalCompilationDiagnostic(diagnostic, project.Name, sourceRoot, sourceContext));
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
                SourceContext = sourceContext,
                Compilation = compilation,
                AuthoredSyntaxTrees = authoredSyntaxTrees
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
            await Console.Out.WriteLineAsync($"generated-diagnostic: severity={diagnostic.Severity} code={diagnostic.Code} message={diagnostic.Message}");
        }
        foreach (var expectation in unmet)
        {
            await Console.Error.WriteLineAsync($"verification: unmet={expectation}");
        }
        if (unmet.Length > 0 || !result.IsSuccess)
        {
            foreach (var artifact in result.Graph.Artifacts)
            {
                await Console.Error.WriteLineAsync($"verification: artifact={artifact.Key.Subject.Value} kind={artifact.Key.Kind} name={artifact.Variants[0].Definition.Name}");
            }
            foreach (var placement in result.Graph.Placements)
            {
                var slice = placement.EffectiveVariants.Count == 0
                    ? "conflicted"
                    : placement.EffectiveVariants[0].Placement.Slice;
                await Console.Error.WriteLineAsync($"verification: placement={placement.Artifact.Subject.Value} kind={placement.Artifact.Kind} slice={slice}");
            }
        }

        if (!result.IsSuccess)
        {
            return 4;
        }

        return unmet.Length == 0 ? 0 : 5;
    }

    internal static string SourceContextPolicyLine(DotNetProjectSourceContext sourceContext) =>
        $"source-context: policy=v{sourceContext.Policy.Version} display-root={sourceContext.Policy.DisplayRoot} case={sourceContext.Policy.CasePolicy} project={sourceContext.ProjectIdentity} documents={sourceContext.Files.Count}";

    internal static string OperationalWorkspaceDiagnostic(WorkspaceDiagnostic diagnostic, string sourceRoot) =>
        $"operational-msbuild: kind={diagnostic.Kind} message={SanitizeOperationalMessage(diagnostic.Message, sourceRoot)}";

    internal static string SanitizeOperationalMessage(string message, string sourceRoot)
    {
        var normalizedRoot = Path.GetFullPath(sourceRoot).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        return message
            .Replace(normalizedRoot, "<workspace>", StringComparison.Ordinal)
            .Replace(normalizedRoot.Replace('\\', '/'), "<workspace>", StringComparison.Ordinal)
            .Replace(normalizedRoot.Replace('/', '\\'), "<workspace>", StringComparison.Ordinal);
    }

    static string OperationalCompilationDiagnostic(
        Diagnostic diagnostic,
        string projectName,
        string sourceRoot,
        DotNetProjectSourceContext sourceContext)
    {
        var source = diagnostic.Location.SourceTree is { } sourceTree && sourceContext.Files.TryGetValue(sourceTree, out var sourceFile)
            ? sourceFile.DisplayPath
            : "unmapped";
        return $"operational-compilation: project={projectName} severity={diagnostic.Severity} code={diagnostic.Id} source={source} message={SanitizeOperationalMessage(diagnostic.GetMessage(), sourceRoot)}";
    }

    static bool IsSpecProject(string name) => name.EndsWith("Tests", StringComparison.OrdinalIgnoreCase) ||
                                               name.EndsWith("Specs", StringComparison.OrdinalIgnoreCase);

    static string ProjectIdentityFor(string sourceRoot, string projectFilePath) =>
        Path.ChangeExtension(PortableRelativePath(sourceRoot, projectFilePath), null);

    static string PortableRelativePath(string root, string path)
    {
        if (!Path.IsPathFullyQualified(path))
        {
            throw new InvalidDotNetSourcePath(path);
        }

        var relativePath = Path.GetRelativePath(root, path).Replace('\\', '/');
        if (Path.IsPathFullyQualified(relativePath) ||
            relativePath == ".." ||
            relativePath.StartsWith("../", StringComparison.Ordinal))
        {
            throw new InvalidDotNetSourcePath(relativePath);
        }

        return relativePath;
    }

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
