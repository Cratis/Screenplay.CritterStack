// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Cratis.CritterStack.Screenplay.Marten;
using Cratis.Screenplay.Generation;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Cratis.CritterStack.Screenplay.Wolverine;

internal static partial class WolverineFacts
{
    static BackendOwnership QueryResultOwnership(WolverineFactScope scope, IMethodSymbol method)
    {
        var ownerships = SessionQueries(scope, method)
            .Select(invocation => scope.Resolver?.ForInvocation(scope.Project, invocation, method) ?? scope.Ownership)
            .Distinct().ToArray();
        return ownerships.Length switch
        {
            0 => scope.Ownership,
            1 => ownerships[0],
            _ => BackendOwnership.Conflict
        };
    }

    static void AddSessionQueryReads(WolverineFactScope scope, SubjectId querySubject, IMethodSymbol method)
    {
        var documents = SessionQueries(scope, method)
            .Select(invocation => DirectQueryDocument(invocation, scope.Project.Compilation.GetSemanticModel(invocation.SyntaxTree)))
            .OfType<INamedTypeSymbol>().Where(IsSourceType)
            .GroupBy(document => scope.Subjects.SubjectForType(scope.Project, document));
        foreach (var document in documents)
        {
            scope.Facts.Add(Relationship(
                scope,
                $"wolverine:reads:session:{querySubject.Value}:{document.Key.Value}",
                querySubject,
                RelationshipKind.Reads,
                document.Key,
                MethodEvidence(method, scope.Project, scope.Adapter, EvidenceStrength.Exact, "Marten session query"),
                unscopedSource: true));
        }
    }

    static INamedTypeSymbol? DirectQueryDocument(InvocationExpressionSyntax invocation, SemanticModel model)
    {
        if (MartenCompiledQueryDiscovery.TryResolve(invocation, model, out _)) return null;
        return (model.GetSymbolInfo(invocation).Symbol as IMethodSymbol)?.TypeArguments.FirstOrDefault() as INamedTypeSymbol;
    }

    static IEnumerable<InvocationExpressionSyntax> SessionQueries(WolverineFactScope scope, IMethodSymbol method) =>
        WolverineMethodSyntax.Declarations(method, scope.Project).SelectMany(declaration => declaration.Declaration.DescendantNodes()
            .OfType<InvocationExpressionSyntax>().Where(invocation => IsSessionQuery(invocation, declaration.SemanticModel)));

    static bool IsSessionQuery(InvocationExpressionSyntax invocation, SemanticModel model) =>
        model.GetSymbolInfo(invocation).Symbol is IMethodSymbol called && called.Name == "Query" && IsPersistenceNamespace(called);
}
