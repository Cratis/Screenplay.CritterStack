// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Cratis.Screenplay.Generation.DotNet;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Operations;

namespace Cratis.CritterStack.Screenplay.Wolverine;

internal static partial class WolverineFacts
{
    static bool HasDocumentPersistence(IMethodSymbol method, DotNetProjectCompilation project) =>
        WolverineMethodSyntax.Declarations(method, project).Any(declaration =>
            declaration.Declaration.DescendantNodes()
                .OfType<InvocationExpressionSyntax>()
                .Select(invocation => declaration.SemanticModel.GetSymbolInfo(invocation).Symbol as IMethodSymbol)
                .Any(invoked => invoked is not null &&
                                _documentPersistenceMethods.Contains(invoked.Name) &&
                                IsPersistenceNamespace(invoked)));

    static IEnumerable<INamedTypeSymbol> DocumentDeletes(IMethodSymbol method, DotNetProjectCompilation project)
    {
        foreach (var (declaration, semanticModel) in WolverineMethodSyntax.Declarations(method, project))
        {
            foreach (var invocation in declaration.DescendantNodes().OfType<InvocationExpressionSyntax>())
            {
                if (DeletedDocumentType(invocation, semanticModel) is { } documentType)
                {
                    yield return documentType;
                }
            }
        }
    }

    static INamedTypeSymbol? DeletedDocumentType(InvocationExpressionSyntax invocation, SemanticModel semanticModel) =>
        semanticModel.GetSymbolInfo(invocation).Symbol is IMethodSymbol invoked &&
        string.Equals(invoked.Name, "Delete", StringComparison.Ordinal) &&
        IsPersistenceNamespace(invoked)
            ? invoked.TypeArguments.FirstOrDefault() as INamedTypeSymbol
            : null;

    static IEnumerable<ITypeSymbol> PersistenceEvents(IMethodSymbol method, DotNetProjectCompilation project)
    {
        foreach (var (declaration, semanticModel) in WolverineMethodSyntax.Declarations(method, project))
        {
            foreach (var eventType in AppendedEvents(declaration, semanticModel, method, project))
            {
                yield return eventType;
            }

            foreach (var eventType in AssignedEvents(declaration, semanticModel, project))
            {
                yield return eventType;
            }

            foreach (var eventType in CollectionExpressionEvents(declaration, semanticModel, project))
            {
                yield return eventType;
            }
        }
    }

    static IEnumerable<INamedTypeSymbol> AppendedEvents(
        MethodDeclarationSyntax declaration,
        SemanticModel semanticModel,
        IMethodSymbol method,
        DotNetProjectCompilation project) =>
        declaration.DescendantNodes()
            .OfType<InvocationExpressionSyntax>()
            .Where(invocation => !IsExactEventStreamAppend(invocation, semanticModel, project) && IsPersistenceAppend(invocation, semanticModel))
            .SelectMany(invocation => invocation.ArgumentList.Arguments)
            .Select(argument => AuthoredEventType(semanticModel.GetTypeInfo(argument.Expression).Type, project))
            .OfType<INamedTypeSymbol>()
            .Where(eventType => !SymbolEqualityComparer.Default.Equals(eventType, method.ContainingType));

    static IEnumerable<INamedTypeSymbol> AssignedEvents(
        MethodDeclarationSyntax declaration,
        SemanticModel semanticModel,
        DotNetProjectCompilation project) =>
        declaration.DescendantNodes()
            .OfType<AssignmentExpressionSyntax>()
            .Where(assignment => assignment.IsKind(SyntaxKind.AddAssignmentExpression) &&
                                 IsEventCollectionType(semanticModel.GetTypeInfo(assignment.Left).Type))
            .Select(assignment => AuthoredEventType(semanticModel.GetTypeInfo(assignment.Right).Type, project))
            .OfType<INamedTypeSymbol>();

    static IEnumerable<INamedTypeSymbol> CollectionExpressionEvents(
        MethodDeclarationSyntax declaration,
        SemanticModel semanticModel,
        DotNetProjectCompilation project) =>
        declaration.DescendantNodes()
            .OfType<CollectionExpressionSyntax>()
            .Where(collection => IsEventCollectionType(semanticModel.GetTypeInfo(collection).ConvertedType))
            .SelectMany(collection => collection.Elements.OfType<ExpressionElementSyntax>())
            .Select(element => AuthoredEventType(semanticModel.GetTypeInfo(element.Expression).Type, project))
            .OfType<INamedTypeSymbol>();

    static bool IsExactEventStreamAppend(InvocationExpressionSyntax invocation, SemanticModel semanticModel, DotNetProjectCompilation project) =>
        semanticModel.GetOperation(invocation) is IInvocationOperation operation &&
        WolverineEventStreams.IsExactAppend(operation, project);

    static bool IsPersistenceAppend(InvocationExpressionSyntax invocation, SemanticModel semanticModel) =>
        semanticModel.GetSymbolInfo(invocation).Symbol is IMethodSymbol invoked &&
        _persistenceMethods.Contains(invoked.Name) &&
        IsPersistenceNamespace(invoked);

    static INamedTypeSymbol? AuthoredEventType(ITypeSymbol? type, DotNetProjectCompilation project) =>
        type is INamedTypeSymbol eventType &&
        IsEventPayloadType(eventType) &&
        !WolverineSagaTypes.IsSagaState(eventType, project)
            ? eventType
            : null;

    static bool IsEventCollectionType(ITypeSymbol? type) => type is INamedTypeSymbol named && IsEventCollection(named);

    static bool IsEventCollection(INamedTypeSymbol type)
    {
        var metadataName = DotNetSubjectIds.MetadataName(type.OriginalDefinition);
        return metadataName == WellKnownTypes.WolverineEvents ||
               metadataName == WellKnownTypes.WolverineEventsToAppend;
    }

    static bool IsPersistenceNamespace(IMethodSymbol method)
    {
        var candidate = method.ReducedFrom ?? method;
        var @namespace = candidate.ContainingNamespace.ToDisplayString();
        return @namespace.StartsWith("Marten", StringComparison.Ordinal) ||
               @namespace.StartsWith("JasperFx.Events", StringComparison.Ordinal) ||
               @namespace.StartsWith("Wolverine.Marten", StringComparison.Ordinal);
    }
}
