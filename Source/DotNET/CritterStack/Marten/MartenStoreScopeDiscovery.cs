// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Cratis.Screenplay.Generation;
using Cratis.Screenplay.Generation.DotNet;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Cratis.CritterStack.Screenplay.Marten;

static class MartenStoreScopeDiscovery
{
    const string ModelBoundary = "Screenplay facts are not scoped by store; ownership not represented. Marten coalesces store-derived models with the same model name; store interfaces alone do not split models. Store selection, runtime tenancy, and ownership boundaries were not inferred";

    public static IReadOnlyList<GenerationDiagnostic> Discover(DotNetProjectCompilation project)
    {
        var scan = new StoreScopeScan(project, StoreCount(project) <= 1 && HasOnlyDefaultModelNames(project));
        foreach (var tree in AuthoredTrees(project))
        {
            scan.VisitTree(tree);
        }

        scan.ReportDefaultCollision();
        return scan.Diagnostics;
    }

    static IEnumerable<SyntaxTree> AuthoredTrees(DotNetProjectCompilation project) => project.Compilation.SyntaxTrees
        .Where(_ => project.AuthoredSyntaxTrees.Contains(_) && !DotNetGeneratedSource.IsGenerated(_));

    static bool IsModelName(ExpressionSyntax expression, SemanticModel semanticModel) =>
        semanticModel.GetSymbolInfo(expression).Symbol is IPropertySymbol property && property.Name == WellKnownTypes.MartenEventModelName &&
        DotNetSubjectIds.MetadataName(property.ContainingType.OriginalDefinition) == WellKnownTypes.MartenStoreOptions;

    static INamedTypeSymbol? StoreType(InvocationExpressionSyntax invocation, SemanticModel semanticModel)
    {
        if (semanticModel.GetSymbolInfo(invocation).Symbol is not IMethodSymbol { Name: "AddMartenStore", TypeArguments.Length: 1 } method ||
            DotNetSubjectIds.MetadataName(method.ContainingType.OriginalDefinition) != "Marten.MartenServiceCollectionExtensions")
        {
            return null;
        }

        return method.TypeArguments[0] is INamedTypeSymbol { TypeKind: not TypeKind.Error } store ? store : null;
    }

    static bool IsStoreAttribute(AttributeSyntax attribute, SemanticModel semanticModel) =>
        semanticModel.GetSymbolInfo(attribute).Symbol is IMethodSymbol constructor &&
        DotNetSubjectIds.MetadataName(constructor.ContainingType.OriginalDefinition) == WellKnownTypes.WolverineMartenStoreAttribute;

    static bool MayUseDefaultModelName(InvocationExpressionSyntax invocation, SemanticModel semanticModel)
    {
        var assignments = invocation.DescendantNodes().OfType<AssignmentExpressionSyntax>()
            .Where(_ => IsModelName(_.Left, semanticModel)).ToArray();
        return assignments.Length == 0 || assignments.Any(_ => IsDefaultModelName(semanticModel.GetConstantValue(_.Right)));
    }

    static bool IsDefaultModelName(Optional<object?> value) =>
        value.HasValue && (value.Value is null || Equals(value.Value, "EventModel"));

    static INamedTypeSymbol? SelectedStoreType(AttributeSyntax attribute, SemanticModel semanticModel) =>
        attribute.ArgumentList?.Arguments.FirstOrDefault()?.Expression is TypeOfExpressionSyntax typeOf
            ? semanticModel.GetTypeInfo(typeOf.Type).Type as INamedTypeSymbol
            : null;

    static int StoreCount(DotNetProjectCompilation project) => AuthoredTrees(project)
        .SelectMany(tree =>
        {
            var semanticModel = project.Compilation.GetSemanticModel(tree);
            return tree.GetRoot().DescendantNodes().Select(node => StoreTypeAt(node, semanticModel)?.ToDisplayString());
        })
        .OfType<string>().Distinct(StringComparer.Ordinal).Count();

    static INamedTypeSymbol? StoreTypeAt(SyntaxNode node, SemanticModel semanticModel) => node switch
    {
        InvocationExpressionSyntax invocation => StoreType(invocation, semanticModel),
        AttributeSyntax attribute when IsStoreAttribute(attribute, semanticModel) => SelectedStoreType(attribute, semanticModel),
        _ => null
    };

    static bool HasOnlyDefaultModelNames(DotNetProjectCompilation project) => AuthoredTrees(project).All(tree =>
    {
        var semanticModel = project.Compilation.GetSemanticModel(tree);
        return tree.GetRoot().DescendantNodes().OfType<AssignmentExpressionSyntax>()
            .Where(_ => IsModelName(_.Left, semanticModel))
            .All(_ => _.IsKind(Microsoft.CodeAnalysis.CSharp.SyntaxKind.SimpleAssignmentExpression) &&
                IsDefaultModelName(semanticModel.GetConstantValue(_.Right)));
    });

    sealed class StoreScopeScan(DotNetProjectCompilation project, bool singleDefaultStore)
    {
        readonly List<(string Type, Location Location)> _defaultStores = [];

        public List<GenerationDiagnostic> Diagnostics { get; } = [];

        public void VisitTree(SyntaxTree tree)
        {
            var semanticModel = project.Compilation.GetSemanticModel(tree);
            foreach (var node in tree.GetRoot().DescendantNodes())
            {
                switch (node)
                {
                    case AssignmentExpressionSyntax assignment when IsModelName(assignment.Left, semanticModel):
                        ReportModelName(assignment, semanticModel);
                        break;
                    case InvocationExpressionSyntax invocation when StoreType(invocation, semanticModel) is { } store:
                        ReportStore(invocation, store, semanticModel);
                        break;
                    case AttributeSyntax attribute when IsStoreAttribute(attribute, semanticModel):
                        ReportSelection(attribute, semanticModel);
                        break;
                }
            }
        }

        public void ReportDefaultCollision()
        {
            var stores = _defaultStores.Select(_ => _.Type).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray();
            if (stores.Length > 1)
            {
                Report($"Multiple ancillary stores may use the default event-model name and coalesce into one model: {string.Join(", ", stores)}", _defaultStores[0].Location);
            }
        }

        void ReportModelName(AssignmentExpressionSyntax assignment, SemanticModel semanticModel)
        {
            var value = assignment.IsKind(Microsoft.CodeAnalysis.CSharp.SyntaxKind.SimpleAssignmentExpression)
                ? semanticModel.GetConstantValue(assignment.Right)
                : default;
            var description = "a computed event-model name; its value is unknown";
            if (value is { HasValue: true, Value: string name })
            {
                description = $"event-model name '{name}'";
            }
            else if (value is { HasValue: true, Value: null })
            {
                description = "the default event-model name";
            }

            Report(
                $"Marten has authored {description}",
                assignment.GetLocation(),
                singleDefaultStore && IsDefaultModelName(value) ? GenerationDiagnosticSeverity.Information : GenerationDiagnosticSeverity.Warning);
        }

        void ReportStore(InvocationExpressionSyntax invocation, INamedTypeSymbol store, SemanticModel semanticModel)
        {
            var usesDefault = MayUseDefaultModelName(invocation, semanticModel);
            Report(
                $"Marten registers ancillary store '{store.ToDisplayString()}'",
                invocation.GetLocation(),
                singleDefaultStore && usesDefault ? GenerationDiagnosticSeverity.Information : GenerationDiagnosticSeverity.Warning);
            if (usesDefault)
            {
                _defaultStores.Add((store.ToDisplayString(), invocation.GetLocation()));
            }
        }

        void ReportSelection(AttributeSyntax attribute, SemanticModel semanticModel)
        {
            var type = SelectedStoreType(attribute, semanticModel);
            var selection = type is { TypeKind: not TypeKind.Error } ? $"'{type.ToDisplayString()}'" : "an unresolved store type";
            Report(
                $"MartenStore selects {selection}",
                attribute.GetLocation(),
                singleDefaultStore && type is { TypeKind: not TypeKind.Error } ? GenerationDiagnosticSeverity.Information : GenerationDiagnosticSeverity.Warning);
        }

        void Report(string message, Location location, GenerationDiagnosticSeverity severity = GenerationDiagnosticSeverity.Warning) => Diagnostics.Add(new()
        {
            Code = MartenDiagnosticCodes.StoreScopeNotRepresented,
            Severity = severity,
            Outcome = GenerationDiagnosticOutcome.Unsupported,
            Message = $"{message}. {ModelBoundary}",
            Source = CritterStackSource.RangeForProject(location, project),
            Subject = new SubjectId { Value = $"dotnet:project/{project.Name}" }
        });
    }
}
