// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Cratis.Screenplay.Generation;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Cratis.CritterStack.Screenplay.Wolverine;

internal static partial class WolverineFacts
{
    static RelationshipFact Relationship(
        WolverineFactScope scope,
        string id,
        SubjectId source,
        RelationshipKind kind,
        SubjectId target,
        Evidence evidence,
        string? sourceMember = null,
        string? targetMember = null,
        string? discriminator = null,
        bool isCollection = false,
        bool isOptional = false,
        bool unscopedSource = false,
        BackendOwnership? targetOwnership = null) => Relationship(
            id,
            source,
            kind,
            target,
            evidence,
            sourceMember,
            targetMember,
            discriminator,
            isCollection,
            isOptional,
            unscopedSource ? null : scope.Ownership.Scope,
            targetOwnership is null ? scope.Ownership.Scope : targetOwnership.Scope);

    static RelationshipFact Relationship(WolverineFactScope scope, WolverineRelationshipFact relationship) => Relationship(relationship with
    {
        SourceScope = scope.Ownership.Scope,
        TargetScope = scope.Ownership.Scope
    });

    static BackendOwnership OwnershipAtSource(WolverineFactScope scope, IMethodSymbol method, SourceRange? source)
    {
        if (scope.Resolver is null) return scope.Ownership;
        var invocations = scope.Project.AuthoredSyntaxTrees.SelectMany(tree => tree.GetRoot().DescendantNodes().OfType<InvocationExpressionSyntax>())
            .Where(invocation => CritterStackSource.RangeForProject(invocation.GetLocation(), scope.Project) == source).ToArray();
        return invocations.Length == 1
            ? scope.Resolver.ForInvocation(scope.Project, invocations[0], method)
            : BackendOwnership.Unknown;
    }

    static IReadOnlyList<WolverineStateBinding> OwnedFetchedBindings(WolverineFactScope scope, IMethodSymbol method, IReadOnlyList<WolverineStateBinding> bindings) =>
    [
        .. bindings.Select(binding => binding with { Ownership = FetchedOwnership(scope, method, binding) })
    ];

    static BackendOwnership FetchedOwnership(WolverineFactScope scope, IMethodSymbol method, WolverineStateBinding binding)
    {
        var variable = binding.Fetched?.Local.DeclaringSyntaxReferences.SingleOrDefault()?.GetSyntax() as VariableDeclaratorSyntax;
        var invocation = variable?.Initializer?.Value.DescendantNodesAndSelf().OfType<InvocationExpressionSyntax>().FirstOrDefault();
        return invocation is null ? BackendOwnership.Unknown : OwnershipAtSource(scope, method, CritterStackSource.RangeForProject(invocation.GetLocation(), scope.Project));
    }

    static IEnumerable<BackendOwnership> ProductionOwnerships(WolverineFactScope scope, StateChangeShape shape, WolverineProductionDecision production)
    {
        var appends = shape.SessionAppends.Where(append => SymbolEqualityComparer.Default.Equals(append.EventType, production.EventType)).ToArray();
        var ownerships = appends.Select(append => OwnershipAtSource(scope, shape.Method, append.Source)).ToList();
        if (appends.Length == 0 || shape.ReturnEvents.Concat(shape.Yields.Select(item => item.EventType)).Any(type => SymbolEqualityComparer.Default.Equals(type, production.EventType)))
        {
            ownerships.Add(scope.Ownership);
        }

        return ownerships.Distinct();
    }

    static bool HasMartenOperations(WolverineFactScope scope, IMethodSymbol method) =>
        WolverineMethodSyntax.Declarations(method, scope.Project).Any(declaration =>
            declaration.Declaration.DescendantNodes().OfType<InvocationExpressionSyntax>().Any(invocation =>
                declaration.SemanticModel.GetSymbolInfo(invocation).Symbol is IMethodSymbol called &&
                called.ContainingNamespace.ToDisplayString().StartsWith("Marten", StringComparison.Ordinal)));
}
