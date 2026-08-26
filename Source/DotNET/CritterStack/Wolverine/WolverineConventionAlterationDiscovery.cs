// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Cratis.Screenplay.Generation;
using Cratis.Screenplay.Generation.DotNet;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Cratis.CritterStack.Screenplay.Wolverine;

static class WolverineConventionAlterationDiscovery
{
    public static IReadOnlyList<GenerationDiagnostic> Discover(
        DotNetProjectCompilation project,
        CritterStackSubjectResolver subjects)
    {
        var diagnostics = new List<GenerationDiagnostic>();
        var catalog = new DotNetArtifactCatalog(project.Compilation);
        foreach (var type in catalog.Types.Where(_ => IsAuthoredConcreteType(_, project) && AltersConventions(_)))
        {
            var location = type.Locations.First(_ => IsAuthoredLocation(_, project));
            diagnostics.Add(new()
            {
                Code = WolverineDiagnosticCodes.ConventionAlterationOmitted,
                Severity = GenerationDiagnosticSeverity.Warning,
                Outcome = GenerationDiagnosticOutcome.Unsupported,
                Message = $"Authored Wolverine convention-alteration type '{type.Name}' may change handler discovery or chain behavior at runtime; the model reflects default conventions only",
                Source = CritterStackSource.RangeForProject(location, project),
                Subject = subjects.SubjectForType(project, type)
            });
        }

        foreach (var tree in project.Compilation.SyntaxTrees
                     .Where(_ => project.AuthoredSyntaxTrees.Contains(_) && !DotNetGeneratedSource.IsGenerated(_))
                     .OrderBy(_ => _.FilePath, StringComparer.Ordinal))
        {
            var semanticModel = project.Compilation.GetSemanticModel(tree);
            foreach (var invocation in tree.GetRoot().DescendantNodes().OfType<InvocationExpressionSyntax>().OrderBy(_ => _.SpanStart))
            {
                if (semanticModel.GetSymbolInfo(invocation).Symbol is not IMethodSymbol method ||
                    !string.Equals(method.Name, "CustomizeMessageDiscovery", StringComparison.Ordinal) ||
                    DotNetSubjectIds.MetadataName(method.ContainingType.OriginalDefinition) != WellKnownTypes.WolverineHandlerDiscovery)
                {
                    continue;
                }

                var containingType = semanticModel.GetEnclosingSymbol(invocation.SpanStart)?.ContainingType;
                diagnostics.Add(new()
                {
                    Code = WolverineDiagnosticCodes.ConventionAlterationOmitted,
                    Severity = GenerationDiagnosticSeverity.Warning,
                    Outcome = GenerationDiagnosticOutcome.Unsupported,
                    Message = "Authored Wolverine message discovery customization may change which types are treated as messages at runtime; the model reflects default conventions only",
                    Source = CritterStackSource.RangeForProject(invocation.GetLocation(), project),
                    Subject = containingType is null
                        ? new SubjectId { Value = $"dotnet://{project.Name}/#wolverine-message-discovery" }
                        : subjects.SubjectForType(project, containingType)
                });
            }
        }

        return
        [
            .. diagnostics
                .OrderBy(_ => _.Source?.Path, StringComparer.Ordinal)
                .ThenBy(_ => _.Source?.StartLine)
                .ThenBy(_ => _.Source?.StartColumn)
                .ThenBy(_ => _.Subject?.Value, StringComparer.Ordinal)
        ];
    }

    static bool AltersConventions(INamedTypeSymbol type)
    {
        if (DotNetSymbols.Implements(type, WellKnownTypes.WolverineHandlerPolicy) ||
            DotNetSymbols.Implements(type, WellKnownTypes.WolverinePolicy) ||
            DotNetSymbols.Implements(type, WellKnownTypes.WolverineExtension))
        {
            return true;
        }

        return DotNetSubjectIds.MetadataName(type.OriginalDefinition) != WellKnownTypes.WolverineModifyHandlerChainAttribute &&
               DotNetSymbols.IsOrInheritsFrom(type, WellKnownTypes.WolverineModifyHandlerChainAttribute);
    }

    static bool IsAuthoredConcreteType(INamedTypeSymbol type, DotNetProjectCompilation project) =>
        type.TypeKind == TypeKind.Class &&
        !type.IsAbstract &&
        type.Locations.Any(_ => IsAuthoredLocation(_, project));

    static bool IsAuthoredLocation(Location location, DotNetProjectCompilation project) =>
        location.IsInSource &&
        location.SourceTree is not null &&
        project.AuthoredSyntaxTrees.Contains(location.SourceTree) &&
        !DotNetGeneratedSource.IsGenerated(location.SourceTree);
}
