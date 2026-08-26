// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Cratis.Screenplay.Generation;
using Cratis.Screenplay.Generation.DotNet;
using Microsoft.CodeAnalysis;

namespace Cratis.CritterStack.Screenplay;

sealed record CritterStackSubjectResolutionCheckpoint(int UnresolvedUseCount);

sealed class CritterStackSubjectResolver(DotNetAnalysisContext? context)
{
    readonly SortedDictionary<string, GenerationDiagnostic> _diagnostics = new(StringComparer.Ordinal);
    int _unresolvedUseCount;

    public IReadOnlyList<GenerationDiagnostic> Diagnostics => [.. _diagnostics.Values];

    public CritterStackSubjectResolutionCheckpoint Checkpoint() => new(_unresolvedUseCount);

    public bool HasBlockingDiagnosticsSince(CritterStackSubjectResolutionCheckpoint checkpoint) =>
        _unresolvedUseCount > checkpoint.UnresolvedUseCount;

    public SubjectId SubjectForType(DotNetProjectCompilation discoveringProject, INamedTypeSymbol type)
    {
        if (context is null)
        {
            return discoveringProject.SubjectForType(type);
        }

        var metadataName = DotNetSubjectIds.MetadataName(type);
        var assemblyIdentity = type.ContainingAssembly.Identity.ToString();
        var owners = ExactOwners(type, metadataName);
        if (owners.Count == 1)
        {
            return owners[0];
        }
        if (owners.Count == 0 && !RequiresSourceOwnership(discoveringProject, type))
        {
            return new SubjectId
            {
                Value = $"dotnet://external-type/{Uri.EscapeDataString(assemblyIdentity)}/{Uri.EscapeDataString(metadataName)}"
            };
        }

        _unresolvedUseCount++;
        var typeIdentity = $"{assemblyIdentity}/{metadataName}";
        var outcome = owners.Count == 0
            ? GenerationDiagnosticOutcome.Unknown
            : GenerationDiagnosticOutcome.Conflict;
        var ownerDescription = owners.Count == 0
            ? "has no exact source owner among the analyzed projects"
            : $"has ambiguous exact source ownership among the analyzed projects: {string.Join(", ", owners.Select(_ => $"'{_.Value}'"))}";
        var diagnosticSubject = new SubjectId
        {
            Value = $"dotnet://unresolved-source-type/{Uri.EscapeDataString(assemblyIdentity)}/{Uri.EscapeDataString(metadataName)}"
        };
        var key = $"{typeIdentity}\u001f{(int)outcome}\u001f{string.Join('\u001f', owners.Select(_ => _.Value))}";
        _diagnostics.TryAdd(key, new GenerationDiagnostic
        {
            Code = DotNetSourceStructureDiagnosticCodes.MissingSourceMapping,
            Severity = GenerationDiagnosticSeverity.Error,
            Outcome = outcome,
            Message = $"Type '{typeIdentity}' {ownerDescription}; semantic facts using that unresolved type were omitted",
            Subject = diagnosticSubject
        });

        return new SubjectId
        {
            Value = $"dotnet://internal-unresolved-source-type/{Uri.EscapeDataString(typeIdentity)}"
        };
    }

    static bool RequiresSourceOwnership(DotNetProjectCompilation discoveringProject, INamedTypeSymbol type) =>
        SymbolEqualityComparer.Default.Equals(type.ContainingModule, discoveringProject.Compilation.SourceModule) ||
        discoveringProject.Compilation.References
            .OfType<CompilationReference>()
            .Any(reference => SymbolEqualityComparer.Default.Equals(reference.Compilation.Assembly, type.ContainingAssembly));

    IReadOnlyList<SubjectId> ExactOwners(INamedTypeSymbol inputType, string metadataName) =>
    [
        .. context!.Projects
            .Select(project => new { Project = project, Type = project.Compilation.GetTypeByMetadataName(metadataName) })
            .Where(_ => _.Type is { } candidate &&
                candidate.ContainingAssembly.Identity.Equals(inputType.ContainingAssembly.Identity) &&
                SymbolEqualityComparer.Default.Equals(candidate.ContainingModule, _.Project.Compilation.SourceModule) &&
                candidate.Locations.Any(location =>
                    location.IsInSource &&
                    location.SourceTree is not null &&
                    _.Project.Compilation.SyntaxTrees.Contains(location.SourceTree)))
            .Select(_ => _.Project.SubjectForType(_.Type!))
            .OrderBy(_ => _.Value, StringComparer.Ordinal)
    ];
}
