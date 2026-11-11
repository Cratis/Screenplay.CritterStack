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
        diagnostics.AddRange(AlterationTypeDiagnostics(project, subjects));
        diagnostics.AddRange(MessageDiscoveryCustomizationDiagnostics(project, subjects));

        return
        [
            .. diagnostics
                .OrderBy(_ => _.Source?.Path, StringComparer.Ordinal)
                .ThenBy(_ => _.Source?.StartLine)
                .ThenBy(_ => _.Source?.StartColumn)
                .ThenBy(_ => _.Subject?.Value, StringComparer.Ordinal)
        ];
    }

    static IEnumerable<GenerationDiagnostic> AlterationTypeDiagnostics(
        DotNetProjectCompilation project,
        CritterStackSubjectResolver subjects)
    {
        var catalog = new DotNetArtifactCatalog(project.Compilation);
        foreach (var type in catalog.Types.Where(_ => IsAuthoredConcreteType(_, project) && AltersConventions(_)))
        {
            var location = type.Locations.First(_ => IsAuthoredLocation(_, project));
            yield return new()
            {
                Code = WolverineDiagnosticCodes.ConventionAlterationOmitted,
                Severity = GenerationDiagnosticSeverity.Warning,
                Outcome = GenerationDiagnosticOutcome.Unsupported,
                Message = $"Authored Wolverine convention-alteration type '{type.Name}' may change handler discovery or chain behavior at runtime; the model reflects default conventions only",
                Source = CritterStackSource.RangeForProject(location, project),
                Subject = subjects.SubjectForType(project, type)
            };
        }
    }

    static IEnumerable<GenerationDiagnostic> MessageDiscoveryCustomizationDiagnostics(
        DotNetProjectCompilation project,
        CritterStackSubjectResolver subjects)
    {
        foreach (var tree in project.Compilation.SyntaxTrees
                     .Where(_ => project.AuthoredSyntaxTrees.Contains(_) && !DotNetGeneratedSource.IsGenerated(_))
                     .OrderBy(_ => _.FilePath, StringComparer.Ordinal))
        {
            var semanticModel = project.Compilation.GetSemanticModel(tree);
            foreach (var invocation in tree.GetRoot().DescendantNodes().OfType<InvocationExpressionSyntax>().OrderBy(_ => _.SpanStart))
            {
                if (!IsMessageDiscoveryCustomization(semanticModel, invocation))
                {
                    continue;
                }

                var containingType = semanticModel.GetEnclosingSymbol(invocation.SpanStart)?.ContainingType;
                yield return new()
                {
                    Code = WolverineDiagnosticCodes.ConventionAlterationOmitted,
                    Severity = GenerationDiagnosticSeverity.Warning,
                    Outcome = GenerationDiagnosticOutcome.Unsupported,
                    Message = "Authored Wolverine message discovery customization may change which types are treated as messages at runtime; the model reflects default conventions only",
                    Source = CritterStackSource.RangeForProject(invocation.GetLocation(), project),
                    Subject = containingType is null
                        ? new SubjectId { Value = $"dotnet://{project.Name}/#wolverine-message-discovery" }
                        : subjects.SubjectForType(project, containingType)
                };
            }
        }
    }

    static bool IsMessageDiscoveryCustomization(SemanticModel semanticModel, InvocationExpressionSyntax invocation) =>
        semanticModel.GetSymbolInfo(invocation).Symbol is IMethodSymbol { Name: "CustomizeMessageDiscovery" } method &&
        DotNetSubjectIds.MetadataName(method.ContainingType.OriginalDefinition) == WellKnownTypes.WolverineHandlerDiscovery;

    static bool AltersConventions(INamedTypeSymbol type) =>
        ImplementsConventionPolicy(type) || IsDerivedChainAttribute(type);

    static bool ImplementsConventionPolicy(INamedTypeSymbol type) =>
        DotNetSymbols.Implements(type, WellKnownTypes.WolverineHandlerPolicy) ||
        DotNetSymbols.Implements(type, WellKnownTypes.WolverinePolicy) ||
        DotNetSymbols.Implements(type, WellKnownTypes.WolverineExtension);

    static bool IsDerivedChainAttribute(INamedTypeSymbol type) =>
        DotNetSubjectIds.MetadataName(type.OriginalDefinition) != WellKnownTypes.WolverineModifyHandlerChainAttribute &&
        DotNetSymbols.IsOrInheritsFrom(type, WellKnownTypes.WolverineModifyHandlerChainAttribute);

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
