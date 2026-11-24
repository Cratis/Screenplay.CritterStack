// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Cratis.Screenplay.Generation;
using Cratis.Screenplay.Generation.DotNet;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Cratis.CritterStack.Screenplay.Marten;

sealed record MartenSessionRead(
    INamedTypeSymbol? DocumentType,
    BackendOwnership Ownership,
    Evidence Evidence,
    bool IsCompiledQuery);

sealed class MartenSessionReadDiscovery(
    DotNetProjectCompilation project,
    AdapterIdentity adapter,
    MartenBackendOwnershipResolver? ownership)
{
    static readonly HashSet<string> _documentReadMethods = ["Load", "LoadAsync", "LoadMany", "LoadManyAsync", "Query"];
    static readonly HashSet<string> _eventReadMethods = ["FetchStream", "FetchStreamAsync", "AggregateStream", "AggregateStreamAsync"];

    public static bool IsDocumentReadMethod(string methodName) => _documentReadMethods.Contains(methodName);

    public static bool IsMarten(IMethodSymbol method, bool includeIntegrationApis)
    {
        var candidate = method.ReducedFrom ?? method;
        var @namespace = candidate.ContainingNamespace.ToDisplayString();
        return @namespace == "Marten" ||
               @namespace.StartsWith("Marten.", StringComparison.Ordinal) ||
               (includeIntegrationApis &&
                (@namespace == "Wolverine.Marten" ||
                 @namespace.StartsWith("Wolverine.Marten.", StringComparison.Ordinal)));
    }

    public static INamedTypeSymbol? DocumentTypeFrom(
        IMethodSymbol method,
        InvocationExpressionSyntax invocation,
        SemanticModel semanticModel)
    {
        var candidate = method.ReducedFrom ?? method;
        var typeArgument = method.TypeArguments.Concat(candidate.TypeArguments)
            .OfType<INamedTypeSymbol>()
            .FirstOrDefault(IsSourceType);
        return typeArgument ?? invocation.ArgumentList.Arguments
            .Select(argument => semanticModel.GetTypeInfo(argument.Expression).Type)
            .OfType<INamedTypeSymbol>()
            .FirstOrDefault(IsSourceType);
    }

    public IReadOnlyList<MartenSessionRead> Discover(IMethodSymbol entryPoint, BackendOwnership fallback) =>
    [
        .. DotNetSource.AuthoredDeclarationsOf(entryPoint, project.AuthoredSyntaxTrees)
            .Select(reference => reference.GetSyntax()).OfType<MethodDeclarationSyntax>()
            .Where(declaration => !DotNetGeneratedSource.IsGenerated(declaration.SyntaxTree))
            .SelectMany(declaration => InDeclaration(declaration, entryPoint, fallback))
    ];

    static bool IsReadMethod(IMethodSymbol method) => IsDocumentReadMethod(method.Name) ||
        _eventReadMethods.Contains(method.Name) || MartenCompiledQueryDiscovery.IsCompiledQueryExecution(method);

    static bool IsSourceType(INamedTypeSymbol type) => type.TypeKind != TypeKind.Error && type.Locations.Any(location => location.IsInSource);

    IEnumerable<MartenSessionRead> InDeclaration(MethodDeclarationSyntax declaration, IMethodSymbol entryPoint, BackendOwnership fallback)
    {
        var model = project.Compilation.GetSemanticModel(declaration.SyntaxTree);
        return declaration.DescendantNodes().OfType<InvocationExpressionSyntax>()
            .Where(invocation => MartenCompiledQueryDiscovery.IsInProvenEndpointFlow(invocation, declaration, model))
            .Select(invocation => ReadFor(invocation, model, entryPoint, fallback)).OfType<MartenSessionRead>();
    }

    MartenSessionRead? ReadFor(InvocationExpressionSyntax invocation, SemanticModel model, IMethodSymbol entryPoint, BackendOwnership fallback)
    {
        if (DotNetInvocations.MethodFor(invocation, model) is not { } method || !IsMarten(method, includeIntegrationApis: true) || !IsReadMethod(method))
        {
            return null;
        }

        var isCompiled = MartenCompiledQueryDiscovery.IsCompiledQueryExecution(method);
        var plan = MartenCompiledQueryDiscovery.TryObserve(invocation, model, out var resolved) ? resolved : null;
        var documentType = isCompiled ? plan?.DocumentType : DocumentTypeFrom(method, invocation, model);
        return new(
            documentType,
            ownership?.ForInvocation(project, invocation, entryPoint) ?? fallback,
            new Evidence
            {
                Adapter = adapter,
                Strength = EvidenceStrength.Exact,
                Source = CritterStackSource.RangeForProject(invocation.GetLocation(), project),
                Explanation = plan is null ? "Marten session query" : $"Marten compiled query '{plan.PlanType.Name}' is executed by application query entry point '{entryPoint.ContainingType.Name}.{entryPoint.Name}'"
            },
            isCompiled);
    }
}
