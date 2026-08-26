// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Cratis.Screenplay.Generation;
using Cratis.Screenplay.Generation.DotNet;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Cratis.CritterStack.Screenplay.Marten;

static class MartenTenancyConfigurationDiscovery
{
    static readonly HashSet<string> _tenancyStyles = ["Single", "Conjoined"];
    static readonly HashSet<string> _tenancyStyleTypes =
    [
        WellKnownTypes.JasperFxTenancyStyle,
        WellKnownTypes.MartenLegacyTenancyStyle
    ];
    static readonly HashSet<string> _documentTenancyMethods =
    [
        "MultiTenanted",
        "SingleTenanted",
        "MultiTenantedWithPartitioning"
    ];
    static readonly HashSet<string> _policyMethods =
    [
        "AllDocumentsAreMultiTenanted",
        "AllDocumentsAreMultiTenantedWithPartitioning"
    ];

    public static IReadOnlyList<GenerationDiagnostic> Discover(
        DotNetProjectCompilation project,
        CritterStackSubjectResolver subjects)
    {
        var diagnostics = new List<GenerationDiagnostic>();
        foreach (var tree in project.Compilation.SyntaxTrees.Where(_ =>
                     project.AuthoredSyntaxTrees.Contains(_) &&
                     !DotNetGeneratedSource.IsGenerated(_)))
        {
            var semanticModel = project.Compilation.GetSemanticModel(tree);
            var root = tree.GetRoot();
            foreach (var assignment in root.DescendantNodes().OfType<AssignmentExpressionSyntax>())
            {
                DiscoverEventTenancy(project, assignment, semanticModel, diagnostics);
            }

            foreach (var invocation in root.DescendantNodes().OfType<InvocationExpressionSyntax>())
            {
                if (semanticModel.GetSymbolInfo(invocation).Symbol is not IMethodSymbol method)
                {
                    continue;
                }

                DiscoverDocumentTenancy(project, subjects, invocation, method, diagnostics);
                DiscoverPolicyTenancy(project, invocation, method, diagnostics);
            }

            foreach (var attribute in root.DescendantNodes().OfType<AttributeSyntax>())
            {
                DiscoverAttributeTenancy(project, subjects, attribute, semanticModel, diagnostics);
            }
        }

        return
        [
            .. diagnostics
                .OrderBy(_ => _.Source?.Path, StringComparer.Ordinal)
                .ThenBy(_ => _.Source?.StartLine)
                .ThenBy(_ => _.Source?.StartColumn)
                .ThenBy(_ => _.Message, StringComparer.Ordinal)
        ];
    }

    static void DiscoverEventTenancy(
        DotNetProjectCompilation project,
        AssignmentExpressionSyntax assignment,
        SemanticModel semanticModel,
        List<GenerationDiagnostic> diagnostics)
    {
        if (semanticModel.GetSymbolInfo(assignment.Left).Symbol is not IPropertySymbol { Name: "TenancyStyle" } property ||
            MetadataName(property.ContainingType.OriginalDefinition) != WellKnownTypes.MartenEventStoreOptions ||
            MetadataName(property.Type) is not { } enumMetadataName ||
            !_tenancyStyleTypes.Contains(enumMetadataName))
        {
            return;
        }

        var style = assignment.IsKind(Microsoft.CodeAnalysis.CSharp.SyntaxKind.SimpleAssignmentExpression)
            ? EnumMember(assignment.Right, semanticModel, enumMetadataName)
            : null;
        diagnostics.Add(style is null
            ? Diagnostic(
                project,
                ProjectSubject(project),
                "Marten has an authored event tenancy-style declaration with a computed, invalid, stale, or otherwise unresolved value; no tenancy style or effective state was guessed",
                assignment.GetLocation(),
                GenerationDiagnosticOutcome.Unknown)
            : Diagnostic(
                project,
                ProjectSubject(project),
                $"Marten has an authored event tenancy-style declaration '{style}'; database topology, runtime tenant resolution, effective state, and projection consequences were not inferred",
                assignment.GetLocation()));
    }

    static void DiscoverDocumentTenancy(
        DotNetProjectCompilation project,
        CritterStackSubjectResolver subjects,
        InvocationExpressionSyntax invocation,
        IMethodSymbol method,
        List<GenerationDiagnostic> diagnostics)
    {
        if (!IsDocumentTenancyMethod(method))
        {
            return;
        }

        if (method.ContainingType.TypeArguments is not [INamedTypeSymbol { TypeKind: not TypeKind.Error } documentType])
        {
            diagnostics.Add(Diagnostic(
                project,
                ProjectSubject(project),
                $"Marten has an authored document tenancy declaration '{method.Name}' with an otherwise unresolved generic document target; no document type or effective tenancy was guessed",
                invocation.GetLocation(),
                GenerationDiagnosticOutcome.Unknown));
            return;
        }

        diagnostics.Add(Diagnostic(
            project,
            subjects.SubjectForType(project, documentType),
            $"Marten has an authored document tenancy declaration '{method.Name}' for '{documentType.Name}'; partition callback behavior, precedence, effective state, tenant identities, and database topology were not inferred",
            invocation.GetLocation()));
    }

    static void DiscoverPolicyTenancy(
        DotNetProjectCompilation project,
        InvocationExpressionSyntax invocation,
        IMethodSymbol method,
        List<GenerationDiagnostic> diagnostics)
    {
        if (!IsPolicyTenancyMethod(method))
        {
            return;
        }

        diagnostics.Add(Diagnostic(
            project,
            ProjectSubject(project),
            $"Marten has an authored project-wide document tenancy policy declaration '{method.Name}'; the policy was not expanded to document types, and partition callback behavior, precedence, effective state, tenant identities, and database topology were not inferred",
            invocation.GetLocation()));
    }

    static void DiscoverAttributeTenancy(
        DotNetProjectCompilation project,
        CritterStackSubjectResolver subjects,
        AttributeSyntax attribute,
        SemanticModel semanticModel,
        List<GenerationDiagnostic> diagnostics)
    {
        if (semanticModel.GetSymbolInfo(attribute).Symbol is not IMethodSymbol constructor ||
            MetadataName(constructor.ContainingType) is not { } attributeMetadataName ||
            attributeMetadataName is not WellKnownTypes.MartenMultiTenantedAttribute and not WellKnownTypes.MartenSingleTenantedAttribute ||
            attribute.FirstAncestorOrSelf<TypeDeclarationSyntax>() is not { } declaration ||
            semanticModel.GetDeclaredSymbol(declaration) is not INamedTypeSymbol documentType)
        {
            return;
        }

        var attributeName = attributeMetadataName == WellKnownTypes.MartenMultiTenantedAttribute
            ? "MultiTenanted"
            : "SingleTenanted";
        diagnostics.Add(Diagnostic(
            project,
            subjects.SubjectForType(project, documentType),
            $"Marten has an authored [{attributeName}] document tenancy declaration for '{documentType.Name}'; attribute evidence alone does not establish a Marten document, and precedence or effective state was not inferred",
            attribute.GetLocation()));
    }

    static bool IsDocumentTenancyMethod(IMethodSymbol method)
    {
        var candidate = method.OriginalDefinition;
        if (!_documentTenancyMethods.Contains(candidate.Name) ||
            MetadataName(candidate.ContainingType.OriginalDefinition) != WellKnownTypes.MartenDocumentMappingExpression ||
            MetadataName(candidate.ReturnType) != WellKnownTypes.MartenDocumentMappingExpression ||
            candidate.TypeParameters.Length != 0)
        {
            return false;
        }

        return candidate.Name switch
        {
            "MultiTenanted" or "SingleTenanted" => candidate.Parameters.Length == 0,
            "MultiTenantedWithPartitioning" => candidate.Parameters is [{ Type: INamedTypeSymbol action }] &&
                                                IsPartitioningAction(action),
            _ => false
        };
    }

    static bool IsPolicyTenancyMethod(IMethodSymbol method)
    {
        var candidate = method.OriginalDefinition;
        if (!_policyMethods.Contains(candidate.Name) ||
            MetadataName(candidate.ContainingType.OriginalDefinition) != WellKnownTypes.MartenPoliciesExpression ||
            MetadataName(candidate.ReturnType) != WellKnownTypes.MartenPoliciesExpression ||
            candidate.TypeParameters.Length != 0)
        {
            return false;
        }

        if (candidate.Name == "AllDocumentsAreMultiTenanted")
        {
            return candidate.Parameters.Length == 0;
        }

        return candidate.Parameters switch
        {
            [{ Type: INamedTypeSymbol action }] => IsPartitioningAction(action),
            [{ Type: INamedTypeSymbol action }, { Type: INamedTypeSymbol ordering }] =>
                IsPartitioningAction(action) && MetadataName(ordering) == WellKnownTypes.MartenPrimaryKeyTenancyOrdering,
            _ => false
        };
    }

    static bool IsPartitioningAction(INamedTypeSymbol action) =>
        MetadataName(action.OriginalDefinition) == "System.Action`1" &&
        action.TypeArguments is [INamedTypeSymbol partitioning] &&
        MetadataName(partitioning) == WellKnownTypes.MartenPartitioningExpression;

    static string? EnumMember(ExpressionSyntax expression, SemanticModel semanticModel, string enumMetadataName)
    {
        var symbol = semanticModel.GetSymbolInfo(expression).Symbol;
        if (symbol is IFieldSymbol field &&
            MetadataName(field.ContainingType) == enumMetadataName &&
            _tenancyStyles.Contains(field.Name))
        {
            return field.Name;
        }

        if (symbol is ILocalSymbol { IsConst: true } local &&
            local.DeclaringSyntaxReferences.SingleOrDefault()?.GetSyntax() is VariableDeclaratorSyntax { Initializer.Value: { } initializer })
        {
            return EnumMember(initializer, semanticModel, enumMetadataName);
        }

        return null;
    }

    static GenerationDiagnostic Diagnostic(
        DotNetProjectCompilation project,
        SubjectId subject,
        string message,
        Location location,
        GenerationDiagnosticOutcome outcome = GenerationDiagnosticOutcome.Unsupported) => new()
        {
            Code = MartenDiagnosticCodes.TenancyConfigurationOmitted,
            Severity = GenerationDiagnosticSeverity.Warning,
            Outcome = outcome,
            Message = $"{message}. This authored declaration is retained as diagnostic evidence only; it does not originate, duplicate, or modify Screenplay artifacts or relationships, and runtime execution or precedence is not asserted",
            Source = CritterStackSource.RangeForProject(location, project),
            Subject = subject
        };

    static string? MetadataName(ITypeSymbol type) => type is INamedTypeSymbol named
        ? DotNetSubjectIds.MetadataName(named.OriginalDefinition)
        : null;

    static SubjectId ProjectSubject(DotNetProjectCompilation project) => new()
    {
        Value = $"dotnet:project/{project.Name}"
    };
}
