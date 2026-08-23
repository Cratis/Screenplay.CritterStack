// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using AdapterIdentity = Cratis.Screenplay.Generation.AdapterIdentity;
using CompilationReference = Microsoft.CodeAnalysis.CompilationReference;
using DotNetGeneratedSource = Cratis.Screenplay.Generation.DotNet.DotNetGeneratedSource;
using DotNetProjectCompilation = Cratis.Screenplay.Generation.DotNet.DotNetProjectCompilation;
using DotNetSource = Cratis.Screenplay.Generation.DotNet.DotNetSource;
using Evidence = Cratis.Screenplay.Generation.Evidence;
using EvidenceStrength = Cratis.Screenplay.Generation.EvidenceStrength;
using IAssemblySymbol = Microsoft.CodeAnalysis.IAssemblySymbol;
using ISymbol = Microsoft.CodeAnalysis.ISymbol;
using Location = Microsoft.CodeAnalysis.Location;
using SourceRange = Cratis.Screenplay.Generation.SourceRange;
using SymbolEqualityComparer = Microsoft.CodeAnalysis.SymbolEqualityComparer;

namespace Cratis.CritterStack.Screenplay;

static class CritterStackSource
{
    public static SourceRange? RangeForProject(Location location, DotNetProjectCompilation project)
    {
        if (!IsAuthoredLocation(location, project))
        {
            return null;
        }

        return project.SourceContext is null
            ? LegacyRange(location, project.SourceRoot)
            : DotNetSource.RangeForProject(location, project);
    }

    public static Evidence EvidenceFor(
        ISymbol symbol,
        AdapterIdentity adapter,
        DotNetProjectCompilation project,
        EvidenceStrength strength,
        string? explanation = null) => new()
        {
            Adapter = adapter,
            Strength = strength,
            Source = SourceFor(symbol, project),
            Explanation = explanation
        };

    static SourceRange? SourceFor(ISymbol symbol, DotNetProjectCompilation project)
    {
        var projectDeclarations = DotNetSource.AuthoredDeclarationsOf(symbol, project.AuthoredSyntaxTrees)
            .Where(_ => !DotNetGeneratedSource.IsGenerated(_.SyntaxTree))
            .ToArray();
        if (projectDeclarations.Length > 0)
        {
            return FirstRange(projectDeclarations.Select(_ => RangeForProject(_.GetSyntax().GetLocation(), project)));
        }

        return CompatibilitySourceForReferencedSymbol(symbol, project);
    }

    static SourceRange? CompatibilitySourceForReferencedSymbol(ISymbol symbol, DotNetProjectCompilation project)
    {
        if (!IsSourceBackedReference(symbol, project))
        {
            return null;
        }

        // This compatibility path only decorates a symbol already admitted by Critter Stack discovery. The shared
        // authored-declaration heuristic excludes generated names and headers; it must never participate in discovery.
        var ranges = DotNetSource.AuthoredDeclarationsOf(symbol)
            .Where(_ => !project.AuthoredSyntaxTrees.Contains(_.SyntaxTree))
            .Select(_ => LegacyRange(_.GetSyntax().GetLocation(), project.SourceRoot));

        return FirstRange(ranges);
    }

    static bool IsSourceBackedReference(ISymbol symbol, DotNetProjectCompilation project)
    {
        if (symbol.ContainingAssembly is not { } containingAssembly ||
            SymbolEqualityComparer.Default.Equals(containingAssembly, project.Compilation.Assembly))
        {
            return false;
        }

        return project.Compilation.References
            .OfType<CompilationReference>()
            .Select(project.Compilation.GetAssemblyOrModuleSymbol)
            .OfType<IAssemblySymbol>()
            .Any(_ => SymbolEqualityComparer.Default.Equals(_, containingAssembly));
    }

    static SourceRange? FirstRange(IEnumerable<SourceRange?> ranges) => ranges
        .OfType<SourceRange>()
        .OrderBy(_ => _.Path, StringComparer.Ordinal)
        .ThenBy(_ => _.StartLine)
        .ThenBy(_ => _.StartColumn)
        .ThenBy(_ => _.EndLine)
        .ThenBy(_ => _.EndColumn)
        .FirstOrDefault();

    static bool IsAuthoredLocation(Location location, DotNetProjectCompilation project) =>
        location.IsInSource &&
        location.SourceTree is { } sourceTree &&
        project.AuthoredSyntaxTrees.Contains(sourceTree) &&
        !DotNetGeneratedSource.IsGenerated(sourceTree);

    static SourceRange? LegacyRange(Location location, string? sourceRoot)
    {
        if (!location.IsInSource ||
            location.SourceTree is null ||
            DotNetGeneratedSource.IsGenerated(location.SourceTree) ||
            !TryGetSafeRelativePath(location, sourceRoot, out var relativePath))
        {
            return null;
        }

        var lineSpan = location.GetLineSpan();
        return new()
        {
            Path = relativePath,
            StartLine = lineSpan.StartLinePosition.Line + 1,
            StartColumn = lineSpan.StartLinePosition.Character + 1,
            EndLine = lineSpan.EndLinePosition.Line + 1,
            EndColumn = lineSpan.EndLinePosition.Character + 1
        };
    }

    static bool TryGetSafeRelativePath(Location location, string? sourceRoot, out string relativePath)
    {
        relativePath = string.Empty;
        if (string.IsNullOrWhiteSpace(sourceRoot) || !Path.IsPathFullyQualified(sourceRoot))
        {
            return false;
        }

        var physicalPath = location.GetLineSpan().Path;
        if (string.IsNullOrWhiteSpace(physicalPath) || !Path.IsPathFullyQualified(physicalPath))
        {
            return false;
        }

        try
        {
            var normalizedRoot = Path.GetFullPath(sourceRoot);
            var normalizedPath = Path.GetFullPath(physicalPath);
            var candidate = Path.GetRelativePath(normalizedRoot, normalizedPath).Replace('\\', '/');
            if (string.IsNullOrWhiteSpace(candidate) ||
                Path.IsPathFullyQualified(candidate) ||
                string.Equals(candidate, ".", StringComparison.Ordinal) ||
                string.Equals(candidate, "..", StringComparison.Ordinal) ||
                candidate.StartsWith("../", StringComparison.Ordinal))
            {
                return false;
            }

            relativePath = candidate;
            return true;
        }
        catch (Exception exception) when (exception is ArgumentException or IOException or NotSupportedException)
        {
            return false;
        }
    }
}
