// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Cratis.CritterStack.Screenplay.Marten;
using Cratis.Screenplay.Generation;
using Cratis.Screenplay.Generation.DotNet;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Operations;

namespace Cratis.CritterStack.Screenplay.Wolverine;

sealed record WolverineStoragePolicy(
    BackendOwnership Ownership,
    string? Namespace,
    string? Assembly,
    SyntaxTree Tree,
    SyntaxNode? Block,
    ISymbol? Receiver,
    int Position,
    bool IsKnown)
{
    public bool HasUnknownOwnership => !IsKnown || Ownership.Kind == BackendOwnershipKind.Unknown;
}

sealed class WolverineStorageRouting(IReadOnlyList<WolverineStoragePolicy> policies)
{
    public static WolverineStorageRouting Discover(DotNetAnalysisContext context, MartenStoreIndex stores) => new(
    [
        .. context.Projects.SelectMany(project => project.AuthoredSyntaxTrees
            .Where(tree => !DotNetGeneratedSource.IsGenerated(tree))
            .SelectMany(tree => InTree(project, tree, stores)))
    ]);

    public BackendOwnership? ForHandler(INamedTypeSymbol handler)
    {
        var ns = handler.ContainingNamespace.ToDisplayString();
        var matching = policies.Where(policy => !policy.IsKnown ||
            (policy.Namespace is { } prefix && (ns == prefix || ns.StartsWith(prefix + ".", StringComparison.Ordinal))) ||
            policy.Assembly == handler.ContainingAssembly.Identity.ToString()).ToArray();
        if (matching.Length == 0)
        {
            return null;
        }

        if (matching.Any(policy => policy.HasUnknownOwnership))
        {
            return BackendOwnership.Unknown;
        }

        var groups = matching.GroupBy(policy => (policy.Tree, policy.Block, policy.Receiver)).ToArray();
        var first = groups.Select(group => group.OrderBy(policy => policy.Position).First().Ownership).Distinct().ToArray();
        return first.Length == 1 ? first[0] : BackendOwnership.Conflict;
    }

    static IEnumerable<WolverineStoragePolicy> InTree(
        DotNetProjectCompilation project,
        SyntaxTree tree,
        MartenStoreIndex stores)
    {
        var model = project.Compilation.GetSemanticModel(tree);
        var nodes = tree.GetRoot().DescendantNodes().ToArray();
        return nodes.OfType<AssignmentExpressionSyntax>().Where(assignment => IsAncillaryMutation(assignment, model))
            .Select(assignment => new WolverineStoragePolicy(BackendOwnership.Unknown, null, null, tree, null, null, assignment.SpanStart, false))
            .Concat(nodes.OfType<InvocationExpressionSyntax>().Select(invocation => model.GetOperation(invocation))
                .OfType<IInvocationOperation>().Where(IsPolicy)
                .Select(operation => PolicyFor(project, operation, model, stores)));
    }

    static bool IsAncillaryMutation(AssignmentExpressionSyntax assignment, SemanticModel model) =>
        model.GetSymbolInfo(assignment.Left).Symbol is IPropertySymbol property && property.Name == "AncillaryStoreType" &&
        property.ContainingNamespace.ToDisplayString().StartsWith("Wolverine", StringComparison.Ordinal);

    static bool IsPolicy(IInvocationOperation operation) => operation.TargetMethod.ContainingNamespace.ToDisplayString() == "Wolverine" &&
        operation.TargetMethod.Name.StartsWith("UseAncillaryStorageFrom", StringComparison.Ordinal);

    static WolverineStoragePolicy PolicyFor(DotNetProjectCompilation project, IInvocationOperation operation, SemanticModel model, MartenStoreIndex stores)
    {
        var invocation = (InvocationExpressionSyntax)operation.Syntax;
        var markerArgument = operation.Arguments.FirstOrDefault(argument => argument.Parameter?.Name == "storeType");
        var marker = TypeOf(markerArgument?.Value.Syntax, model);
        var ownership = marker is null ? BackendOwnership.Unknown : stores.ForMarker(project, marker);
        var (ns, assembly) = Selector(operation, model);
        var block = invocation.Ancestors().FirstOrDefault(node => node is LambdaExpressionSyntax or MethodDeclarationSyntax);
        var receiver = operation.Instance is null ? null : MartenOptionsReceiver.RootSymbol(operation.Instance.Syntax, model);
        var ordered = receiver is not null && !invocation.Ancestors().TakeWhile(node => node != block).Any(IsConditional);
        return new(ownership, ns, assembly, invocation.SyntaxTree, block, receiver, invocation.SpanStart, ordered && (ns is not null || assembly is not null));
    }

    static bool IsConditional(SyntaxNode node) => node is IfStatementSyntax or SwitchStatementSyntax or ConditionalExpressionSyntax or
        ForStatementSyntax or ForEachStatementSyntax or WhileStatementSyntax or DoStatementSyntax or TryStatementSyntax;

    static (string? Namespace, string? Assembly) Selector(IInvocationOperation operation, SemanticModel model)
    {
        var method = operation.TargetMethod;
        var anchor = method.TypeArguments.FirstOrDefault() as INamedTypeSymbol;
        return method.Name switch
        {
            "UseAncillaryStorageFromNamespaceContaining" when anchor is not null => (anchor.ContainingNamespace.ToDisplayString(), null),
            "UseAncillaryStorageFromAssemblyContaining" when anchor is not null => (null, anchor.ContainingAssembly.Identity.ToString()),
            "UseAncillaryStorageFromNamespace" => (NamespaceSelector(operation, model), null),
            "UseAncillaryStorageFromAssembly" => (null, AssemblySelector(operation, model)),
            _ => (null, null)
        };
    }

    static string? NamespaceSelector(IInvocationOperation operation, SemanticModel model) => operation.Arguments
        .Select(argument => model.GetConstantValue(argument.Value.Syntax)).Where(value => value.HasValue)
        .Select(value => value.Value).OfType<string>().FirstOrDefault();

    static string? AssemblySelector(IInvocationOperation operation, SemanticModel model) => operation.Arguments
        .Select(argument => AssemblyOf(argument.Value.Syntax, model)).OfType<string>().FirstOrDefault();

    static INamedTypeSymbol? TypeOf(SyntaxNode? syntax, SemanticModel model)
    {
        if (syntax is ArgumentSyntax argument)
        {
            syntax = argument.Expression;
        }

        return syntax is TypeOfExpressionSyntax expression ? model.GetTypeInfo(expression.Type).Type as INamedTypeSymbol : null;
    }

    static string? AssemblyOf(SyntaxNode syntax, SemanticModel model)
    {
        if (syntax is ArgumentSyntax argument)
        {
            syntax = argument.Expression;
        }

        return syntax is MemberAccessExpressionSyntax access && access.Name.Identifier.ValueText == "Assembly" &&
               TypeOf(access.Expression, model) is { } type ? type.ContainingAssembly.Identity.ToString() : null;
    }
}
