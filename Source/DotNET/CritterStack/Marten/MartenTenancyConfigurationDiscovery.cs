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
        foreach (var tree in project.Compilation.SyntaxTrees.Where(_ => IsAuthoredNonGenerated(project, _)))
        {
            var scope = new TreeScope(project, subjects, project.Compilation.GetSemanticModel(tree), diagnostics);
            DiscoverTree(scope, tree.GetRoot());
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

    static bool IsAuthoredNonGenerated(DotNetProjectCompilation project, SyntaxTree tree) =>
        project.AuthoredSyntaxTrees.Contains(tree) && !DotNetGeneratedSource.IsGenerated(tree);

    static void DiscoverTree(TreeScope scope, SyntaxNode root)
    {
        foreach (var assignment in root.DescendantNodes().OfType<AssignmentExpressionSyntax>())
        {
            DiscoverEventTenancy(scope, assignment);
        }

        foreach (var invocation in root.DescendantNodes().OfType<InvocationExpressionSyntax>())
        {
            if (scope.SemanticModel.GetSymbolInfo(invocation).Symbol is not IMethodSymbol method)
            {
                continue;
            }

            DiscoverDocumentTenancy(scope, invocation, method);
            DiscoverPolicyTenancy(scope, invocation, method);
        }

        foreach (var attribute in root.DescendantNodes().OfType<AttributeSyntax>())
        {
            DiscoverAttributeTenancy(scope, attribute);
        }
    }

    static void DiscoverEventTenancy(TreeScope scope, AssignmentExpressionSyntax assignment)
    {
        if (EventTenancyStyleEnum(assignment, scope.SemanticModel) is not { } enumMetadataName)
        {
            return;
        }

        var style = assignment.IsKind(Microsoft.CodeAnalysis.CSharp.SyntaxKind.SimpleAssignmentExpression)
            ? EnumMember(assignment.Right, scope.SemanticModel, enumMetadataName)
            : null;
        if (style is null)
        {
            scope.Report(
                scope.ProjectSubject,
                "Marten has an authored event tenancy-style declaration with a computed, invalid, stale, or otherwise unresolved value; no tenancy style or effective state was guessed",
                assignment.GetLocation(),
                GenerationDiagnosticOutcome.Unknown);
            return;
        }

        scope.Report(
            scope.ProjectSubject,
            $"Marten has an authored event tenancy-style declaration '{style}'; database topology, runtime tenant resolution, effective state, and projection consequences were not inferred",
            assignment.GetLocation());
    }

    static string? EventTenancyStyleEnum(AssignmentExpressionSyntax assignment, SemanticModel semanticModel)
    {
        if (semanticModel.GetSymbolInfo(assignment.Left).Symbol is not IPropertySymbol { Name: "TenancyStyle" } property ||
            MetadataName(property.ContainingType.OriginalDefinition) != WellKnownTypes.MartenEventStoreOptions)
        {
            return null;
        }

        return MetadataName(property.Type) is { } enumMetadataName && _tenancyStyleTypes.Contains(enumMetadataName)
            ? enumMetadataName
            : null;
    }

    static void DiscoverDocumentTenancy(TreeScope scope, InvocationExpressionSyntax invocation, IMethodSymbol method)
    {
        if (!IsDocumentTenancyMethod(method))
        {
            return;
        }

        if (method.ContainingType.TypeArguments is not [INamedTypeSymbol { TypeKind: not TypeKind.Error } documentType])
        {
            scope.Report(
                scope.ProjectSubject,
                $"Marten has an authored document tenancy declaration '{method.Name}' with an otherwise unresolved generic document target; no document type or effective tenancy was guessed",
                invocation.GetLocation(),
                GenerationDiagnosticOutcome.Unknown);
            return;
        }

        scope.Report(
            scope.Subjects.SubjectForType(scope.Project, documentType),
            $"Marten has an authored document tenancy declaration '{method.Name}' for '{documentType.Name}'; partition callback behavior, precedence, effective state, tenant identities, and database topology were not inferred",
            invocation.GetLocation());
    }

    static void DiscoverPolicyTenancy(TreeScope scope, InvocationExpressionSyntax invocation, IMethodSymbol method)
    {
        if (!IsPolicyTenancyMethod(method))
        {
            return;
        }

        scope.Report(
            scope.ProjectSubject,
            $"Marten has an authored project-wide document tenancy policy declaration '{method.Name}'; the policy was not expanded to document types, and partition callback behavior, precedence, effective state, tenant identities, and database topology were not inferred",
            invocation.GetLocation());
    }

    static void DiscoverAttributeTenancy(TreeScope scope, AttributeSyntax attribute)
    {
        if (TenancyAttributeMetadataName(attribute, scope.SemanticModel) is not { } attributeMetadataName ||
            DeclaredDocumentType(attribute, scope.SemanticModel) is not { } documentType)
        {
            return;
        }

        var attributeName = attributeMetadataName == WellKnownTypes.MartenMultiTenantedAttribute
            ? "MultiTenanted"
            : "SingleTenanted";
        scope.Report(
            scope.Subjects.SubjectForType(scope.Project, documentType),
            $"Marten has an authored [{attributeName}] document tenancy declaration for '{documentType.Name}'; attribute evidence alone does not establish a Marten document, and precedence or effective state was not inferred",
            attribute.GetLocation());
    }

    static string? TenancyAttributeMetadataName(AttributeSyntax attribute, SemanticModel semanticModel) =>
        semanticModel.GetSymbolInfo(attribute).Symbol is IMethodSymbol constructor &&
        MetadataName(constructor.ContainingType) is { } name &&
        (string.Equals(name, WellKnownTypes.MartenMultiTenantedAttribute, StringComparison.Ordinal) ||
         string.Equals(name, WellKnownTypes.MartenSingleTenantedAttribute, StringComparison.Ordinal))
            ? name
            : null;

    static INamedTypeSymbol? DeclaredDocumentType(AttributeSyntax attribute, SemanticModel semanticModel) =>
        attribute.FirstAncestorOrSelf<TypeDeclarationSyntax>() is { } declaration
            ? semanticModel.GetDeclaredSymbol(declaration) as INamedTypeSymbol
            : null;

    static bool IsDocumentTenancyMethod(IMethodSymbol method)
    {
        var candidate = method.OriginalDefinition;
        return IsTenancyMethod(candidate, _documentTenancyMethods, WellKnownTypes.MartenDocumentMappingExpression) &&
               HasDocumentTenancyShape(candidate);
    }

    static bool HasDocumentTenancyShape(IMethodSymbol candidate) => candidate.Name switch
    {
        "MultiTenanted" or "SingleTenanted" => candidate.Parameters.Length == 0,
        "MultiTenantedWithPartitioning" => candidate.Parameters is [{ Type: INamedTypeSymbol action }] &&
                                            IsPartitioningAction(action),
        _ => false
    };

    static bool IsPolicyTenancyMethod(IMethodSymbol method)
    {
        var candidate = method.OriginalDefinition;
        return IsTenancyMethod(candidate, _policyMethods, WellKnownTypes.MartenPoliciesExpression) &&
               HasPolicyTenancyShape(candidate);
    }

    static bool HasPolicyTenancyShape(IMethodSymbol candidate)
    {
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

    static bool IsTenancyMethod(IMethodSymbol candidate, HashSet<string> names, string expressionType) =>
        names.Contains(candidate.Name) &&
        MetadataName(candidate.ContainingType.OriginalDefinition) == expressionType &&
        MetadataName(candidate.ReturnType) == expressionType &&
        candidate.TypeParameters.Length == 0;

    static bool IsPartitioningAction(INamedTypeSymbol action) =>
        MetadataName(action.OriginalDefinition) == "System.Action`1" &&
        action.TypeArguments is [INamedTypeSymbol partitioning] &&
        MetadataName(partitioning) == WellKnownTypes.MartenPartitioningExpression;

    static string? EnumMember(ExpressionSyntax expression, SemanticModel semanticModel, string enumMetadataName)
    {
        var symbol = semanticModel.GetSymbolInfo(expression).Symbol;
        if (symbol is IFieldSymbol field && IsTenancyStyleMember(field, enumMetadataName))
        {
            return field.Name;
        }

        return ConstantLocalInitializer(symbol) is { } initializer
            ? EnumMember(initializer, semanticModel, enumMetadataName)
            : null;
    }

    static bool IsTenancyStyleMember(IFieldSymbol field, string enumMetadataName) =>
        MetadataName(field.ContainingType) == enumMetadataName && _tenancyStyles.Contains(field.Name);

    static ExpressionSyntax? ConstantLocalInitializer(ISymbol? symbol) =>
        symbol is ILocalSymbol { IsConst: true } local &&
        local.DeclaringSyntaxReferences.SingleOrDefault()?.GetSyntax() is VariableDeclaratorSyntax { Initializer.Value: { } initializer }
            ? initializer
            : null;

    static string? MetadataName(ITypeSymbol type) => type is INamedTypeSymbol named
        ? DotNetSubjectIds.MetadataName(named.OriginalDefinition)
        : null;

    sealed record TreeScope(
        DotNetProjectCompilation Project,
        CritterStackSubjectResolver Subjects,
        SemanticModel SemanticModel,
        List<GenerationDiagnostic> Diagnostics)
    {
        public SubjectId ProjectSubject { get; } = new() { Value = $"dotnet:project/{Project.Name}" };

        public void Report(
            SubjectId subject,
            string message,
            Location location,
            GenerationDiagnosticOutcome outcome = GenerationDiagnosticOutcome.Unsupported) =>
            Diagnostics.Add(new()
            {
                Code = MartenDiagnosticCodes.TenancyConfigurationOmitted,
                Severity = GenerationDiagnosticSeverity.Warning,
                Outcome = outcome,
                Message = $"{message}. This authored declaration is retained as diagnostic evidence only; it does not originate, duplicate, or modify Screenplay artifacts or relationships, and runtime execution or precedence is not asserted",
                Source = CritterStackSource.RangeForProject(location, Project),
                Subject = subject
            });
    }
}
