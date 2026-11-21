// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Cratis.Screenplay.Generation;
using Cratis.Screenplay.Generation.DotNet;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Operations;

namespace Cratis.CritterStack.Screenplay.Marten;

static class MartenProjectionKindDiscovery
{
    static readonly HashSet<string> _constituentRegistrations =
    [
        "Snapshot", "Add", "AddProjectionWithServices"
    ];

    public static IReadOnlyList<GenerationDiagnostic> Discover(DotNetProjectCompilation project)
    {
        var diagnostics = new List<GenerationDiagnostic>();
        foreach (var tree in project.Compilation.SyntaxTrees.Where(_ => project.AuthoredSyntaxTrees.Contains(_) && !DotNetGeneratedSource.IsGenerated(_)))
        {
            var semanticModel = project.Compilation.GetSemanticModel(tree);
            foreach (var invocation in tree.GetRoot().DescendantNodes().OfType<InvocationExpressionSyntax>())
            {
                if (RegistrationDiagnostic(project, invocation, semanticModel) is { } diagnostic)
                {
                    diagnostics.Add(diagnostic);
                }
            }
        }

        return diagnostics;
    }

    public static bool IsComposite(INamedTypeSymbol type) =>
        DotNetSymbols.IsOrInheritsFrom(type, WellKnownTypes.MartenCompositeProjection) ||
        DotNetSymbols.IsOrInheritsFrom(type, WellKnownTypes.JasperFxCompositeProjection);

    public static bool IsOpaqueProjection(INamedTypeSymbol type) =>
        IsComposite(type) || DotNetSymbols.IsOrInheritsFrom(type, WellKnownTypes.MartenFlatTableProjection);

    static GenerationDiagnostic? RegistrationDiagnostic(DotNetProjectCompilation project, InvocationExpressionSyntax invocation, SemanticModel semanticModel)
    {
        if (semanticModel.GetSymbolInfo(invocation).Symbol is not IMethodSymbol method ||
            DotNetSubjectIds.MetadataName(method.ContainingType.OriginalDefinition) != WellKnownTypes.MartenProjectionOptions)
        {
            return null;
        }

        if (method.Name == "CompositeProjectionFor")
        {
            return Report(project, invocation, "composite", ConstituentTypes(invocation, semanticModel));
        }

        return method.Name == "Add"
            ? OpaqueProjectionDiagnostic(project, invocation, RegistrationType(invocation, method, semanticModel))
            : null;
    }

    static GenerationDiagnostic? OpaqueProjectionDiagnostic(DotNetProjectCompilation project, InvocationExpressionSyntax invocation, INamedTypeSymbol? type)
    {
        if (type is null || !IsOpaqueProjection(type))
        {
            return null;
        }

        return Report(project, invocation, IsComposite(type) ? "composite" : "flat-table", [type.ToDisplayString()]);
    }

    static INamedTypeSymbol? RegistrationType(InvocationExpressionSyntax invocation, IMethodSymbol method, SemanticModel semanticModel)
    {
        if (method.TypeArguments.FirstOrDefault() is INamedTypeSymbol type)
        {
            return type;
        }

        return (semanticModel.GetOperation(invocation) as IInvocationOperation)?.Arguments
            .FirstOrDefault(_ => _.Parameter?.Ordinal == 0)?.Syntax is not ArgumentSyntax argument ? null : semanticModel.GetTypeInfo(argument.Expression).Type as INamedTypeSymbol;
    }

    static IReadOnlyList<string> ConstituentTypes(InvocationExpressionSyntax invocation, SemanticModel semanticModel)
    {
        var types = new List<string>();
        foreach (var nested in invocation.DescendantNodes().OfType<InvocationExpressionSyntax>())
        {
            if (ConstituentType(nested, semanticModel) is { TypeKind: not TypeKind.Error } type)
            {
                types.Add(type.ToDisplayString());
            }
        }

        return [.. types.Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal)];
    }

    static INamedTypeSymbol? ConstituentType(InvocationExpressionSyntax invocation, SemanticModel semanticModel)
    {
        if (semanticModel.GetSymbolInfo(invocation).Symbol is not IMethodSymbol method || !_constituentRegistrations.Contains(method.Name))
        {
            return null;
        }

        return IsComposite(method.ContainingType) ? RegistrationType(invocation, method, semanticModel) : null;
    }

    static GenerationDiagnostic Report(DotNetProjectCompilation project, InvocationExpressionSyntax invocation, string kind, IReadOnlyList<string> constituents) => new()
    {
        Code = kind == "composite" ? MartenDiagnosticCodes.CompositeProjectionOmitted : MartenDiagnosticCodes.FlatTableProjectionOmitted,
        Severity = GenerationDiagnosticSeverity.Warning,
        Outcome = GenerationDiagnosticOutcome.Unsupported,
        Message = $"Marten registers an opaque {kind} projection{(constituents.Count == 0 ? string.Empty : $" involving {string.Join(", ", constituents)}")}; no reducer, read-model, event relationship, or subprojection ordering was inferred from this registration",
        Source = CritterStackSource.RangeForProject(invocation.GetLocation(), project),
        Subject = new SubjectId { Value = $"dotnet:project/{project.Name}" }
    };
}
