// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Cratis.Screenplay.Generation.DotNet;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Operations;

namespace Cratis.CritterStack.Screenplay.Marten;

sealed record MartenEventModelName(bool IsKnown, string? Value);

static class MartenStoreOptionsBinding
{
    public static IReadOnlyList<ISymbol> OptionsFor(IInvocationOperation operation, SemanticModel model) =>
    [
        .. operation.Arguments.SelectMany(argument => argument.Syntax.DescendantNodesAndSelf()
                .OfType<LambdaExpressionSyntax>()
                .SelectMany(lambda => Parameters(lambda).Select(parameter => model.GetDeclaredSymbol(parameter)))
                .OfType<IParameterSymbol>()
                .Where(parameter => IsOptions(parameter.Type))
                .Cast<ISymbol>())
            .Concat(operation.Arguments.Select(argument => RootSymbol(argument.Value.Syntax, model)).OfType<ISymbol>())
            .Distinct(SymbolEqualityComparer.Default)
    ];

    public static ISymbol? RootSymbol(SyntaxNode syntax, SemanticModel model)
    {
        return MartenOptionsReceiver.RootSymbol(syntax, model);
    }

    public static MartenEventModelName EventModelName(
        IReadOnlyList<ISymbol> options,
        InvocationExpressionSyntax registration,
        SemanticModel model)
    {
        var assignments = registration.SyntaxTree.GetRoot().DescendantNodes()
            .OfType<AssignmentExpressionSyntax>()
            .Where(assignment => IsNameAssignment(assignment, model) && IsAssignmentFor(assignment, registration, options, model))
            .OrderBy(assignment => assignment.SpanStart)
            .ToArray();
        if (options.Any(option => IsReassigned(option, registration.SyntaxTree, model))) return new(false, null);
        if (assignments.Length == 0) return new(true, null);
        if (assignments.Any(assignment => !assignment.IsKind(SyntaxKind.SimpleAssignmentExpression) || !IsUnconditional(assignment, registration))) return new(false, null);
        return ConstantName(model.GetConstantValue(assignments[^1].Right));
    }

    public static bool IsAssignmentFor(
        AssignmentExpressionSyntax assignment,
        InvocationExpressionSyntax registration,
        IReadOnlyList<ISymbol> options,
        SemanticModel model)
    {
        if (registration.Span.Contains(assignment.Span))
        {
            return true;
        }

        var root = RootSymbol(assignment.Left, model);
        var initialized = assignment.Ancestors().OfType<VariableDeclaratorSyntax>().FirstOrDefault();
        var initializedSymbol = initialized is null ? null : model.GetDeclaredSymbol(initialized);
        return options.Any(option => SymbolEqualityComparer.Default.Equals(option, root) ||
            SymbolEqualityComparer.Default.Equals(option, initializedSymbol));
    }

    public static bool IsOptionsReceiver(InvocationExpressionSyntax invocation, SemanticModel model)
    {
        if (invocation.Expression is not MemberAccessExpressionSyntax access)
        {
            return false;
        }

        var type = RootSymbol(access.Expression, model) switch
        {
            IParameterSymbol parameter => parameter.Type,
            ILocalSymbol local => local.Type,
            _ => null
        };
        return type is not null && IsOptions(type);
    }

    public static bool IsReassigned(ISymbol local, SyntaxTree tree, SemanticModel model) =>
        tree.GetRoot().DescendantNodes().OfType<AssignmentExpressionSyntax>()
            .Any(assignment => SymbolEqualityComparer.Default.Equals(model.GetSymbolInfo(assignment.Left).Symbol, local));

    internal static bool IsNameAssignment(AssignmentExpressionSyntax assignment, SemanticModel model) =>
        model.GetSymbolInfo(assignment.Left).Symbol is IPropertySymbol property &&
        property.Name == "EventModelName" && IsOptions(property.ContainingType);

    static bool IsOptions(ITypeSymbol type) => type is INamedTypeSymbol named &&
        DotNetSubjectIds.MetadataName(named) == WellKnownTypes.MartenStoreOptions;

    static MartenEventModelName ConstantName(Optional<object?> constant)
    {
        if (!constant.HasValue) return new(false, null);
        if (constant.Value is null) return new(true, null);
        return constant.Value is string name && IsValidName(name) ? new(true, name) : new(false, null);
    }

    static bool IsValidName(string name) => !string.IsNullOrWhiteSpace(name) && !name.Any(char.IsControl);

    static SeparatedSyntaxList<ParameterSyntax> Parameters(LambdaExpressionSyntax lambda) => lambda switch
    {
        SimpleLambdaExpressionSyntax simple => [simple.Parameter],
        ParenthesizedLambdaExpressionSyntax parenthesized => parenthesized.ParameterList.Parameters,
        _ => []
    };

    static bool IsUnconditional(AssignmentExpressionSyntax assignment, InvocationExpressionSyntax registration)
    {
        var lambda = assignment.Ancestors().OfType<LambdaExpressionSyntax>().FirstOrDefault();
        if (lambda is not null && !registration.Span.Contains(lambda.Span)) return false;
        if (lambda is null && assignment.SpanStart > registration.SpanStart) return false;
        return !assignment.Ancestors().TakeWhile(node => node != lambda && node is not MethodDeclarationSyntax)
            .Any(node => node is IfStatementSyntax or SwitchStatementSyntax or SwitchExpressionSyntax or
                ConditionalExpressionSyntax or ForStatementSyntax or ForEachStatementSyntax or WhileStatementSyntax or
                DoStatementSyntax or TryStatementSyntax);
    }
}
