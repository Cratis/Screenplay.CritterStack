// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Cratis.Screenplay.Generation;
using Cratis.Screenplay.Generation.DotNet;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Operations;
using static Cratis.CritterStack.Screenplay.Wolverine.AuthoredSymbolKeys;

namespace Cratis.CritterStack.Screenplay.Wolverine;

/// <summary>
/// Syntax and operation analysis of the calls inside authored handler methods.
/// </summary>
static class AuthoredCalls
{
    static readonly string[] _frameworkNamespaces = ["System", "Microsoft", "Marten", "Wolverine", "JasperFx"];

    public static bool IsApplicationCall(IMethodSymbol method)
    {
        var name = method.ContainingNamespace.ToDisplayString();
        return !_frameworkNamespaces.Any(prefix => name == prefix || name.StartsWith($"{prefix}.", StringComparison.Ordinal));
    }

    public static AuthoredArgument Bind(IOperation operation, IReadOnlyDictionary<string, AuthoredArgument> arguments)
    {
        while (operation is IConversionOperation conversion)
        {
            operation = conversion.Operand;
        }

        if (operation is IParameterReferenceOperation parameter)
        {
            return arguments.GetValueOrDefault(ParameterKey(parameter.Parameter)) ?? new(null, null);
        }

        if (operation is IPropertyReferenceOperation { Instance: not null } property && Bind(property.Instance, arguments).RequestType is INamedTypeSymbol requestType)
        {
            return new(null, Hierarchy(requestType).SelectMany(type => type.GetMembers().OfType<IPropertySymbol>())
                .FirstOrDefault(member => MemberKey(member) == MemberKey(property.Property)));
        }

        return new(null, null);
    }

    public static bool HasReboundArguments(IMethodSymbol method, DotNetProjectCompilation project, IReadOnlyDictionary<string, AuthoredArgument> arguments) =>
        WritesForwardedArgument(method, project, arguments) || ForwardsArgumentByReference(method, project, arguments);

    public static bool IsConditionallyReached(AuthoredCall call) =>
        WolverineProductionLinks.IsConditional(call.Syntax, call.Declaration) ||
        call.Declaration.DescendantNodes().Any(node => (node is ReturnStatementSyntax or ThrowStatementSyntax or GotoStatementSyntax) &&
            node.Span.End <= call.Syntax.SpanStart && WolverineProductionLinks.IsInHandlerBody(node, call.Declaration));

    public static bool HasOpenType(ITypeSymbol type) =>
        type is ITypeParameterSymbol || (type is INamedTypeSymbol named && (named.IsUnboundGenericType || named.TypeArguments.Any(HasOpenType)));

    public static IEnumerable<AuthoredCall> CallsIn(IMethodSymbol method, DotNetProjectCompilation project) =>
        WolverineMethodSyntax.Declarations(method, project)
            .SelectMany(declaration => declaration.Declaration.DescendantNodes().OfType<InvocationExpressionSyntax>()
                .Where(syntax => WolverineProductionLinks.IsInHandlerBody(syntax, declaration.Declaration))
                .Select(syntax => new { Syntax = syntax, Operation = declaration.SemanticModel.GetOperation(syntax) as IInvocationOperation, declaration.Declaration })
                .Where(call => call.Operation is not null)
                .Select(call => new AuthoredCall(call.Operation!, call.Syntax, call.Declaration, project)));

    public static SourceRange? SourceOf(AuthoredCall call) => CritterStackSource.RangeForProject(call.Syntax.GetLocation(), call.Project);

    static bool WritesForwardedArgument(IMethodSymbol method, DotNetProjectCompilation project, IReadOnlyDictionary<string, AuthoredArgument> arguments) =>
        WolverineMethodSyntax.Declarations(method, project).Any(declaration =>
            WrittenExpressions(declaration.Declaration).Any(expression =>
                expression.DescendantNodesAndSelf().OfType<IdentifierNameSyntax>().Any(identifier =>
                    declaration.SemanticModel.GetSymbolInfo(identifier).Symbol is IParameterSymbol parameter && arguments.ContainsKey(ParameterKey(parameter)))));

    static bool ForwardsArgumentByReference(IMethodSymbol method, DotNetProjectCompilation project, IReadOnlyDictionary<string, AuthoredArgument> arguments) =>
        CallsIn(method, project).Any(call => call.Operation.Arguments.Any(argument =>
            argument.Parameter?.RefKind is not (null or RefKind.None) &&
            Bind(argument.Value, arguments) is { RequestType: not null } or { Identity: not null }));

    static IEnumerable<ExpressionSyntax> WrittenExpressions(MethodDeclarationSyntax declaration) =>
        declaration.DescendantNodes().OfType<AssignmentExpressionSyntax>().Select(assignment => assignment.Left)
            .Concat(declaration.DescendantNodes().OfType<PrefixUnaryExpressionSyntax>()
                .Where(expression => IsStep(expression.OperatorToken)).Select(expression => expression.Operand))
            .Concat(declaration.DescendantNodes().OfType<PostfixUnaryExpressionSyntax>()
                .Where(expression => IsStep(expression.OperatorToken)).Select(expression => expression.Operand));

    static bool IsStep(SyntaxToken token) => token.ValueText == "++" || token.ValueText == "--";
}
