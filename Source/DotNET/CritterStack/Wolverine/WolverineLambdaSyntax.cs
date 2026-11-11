// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Cratis.CritterStack.Screenplay.Wolverine;

/// <summary>
/// Semantic helpers for the callback lambdas passed to Wolverine configuration calls.
/// </summary>
static class WolverineLambdaSyntax
{
    /// <summary>
    /// Gets the single parameter symbol of a lambda expression.
    /// </summary>
    /// <param name="lambda">The lambda expression.</param>
    /// <param name="semanticModel">The semantic model of the lambda.</param>
    /// <returns>The parameter symbol, or <see langword="null"/> when there is not exactly one resolvable parameter.</returns>
    public static IParameterSymbol? Parameter(LambdaExpressionSyntax lambda, SemanticModel semanticModel)
    {
        var parameter = lambda switch
        {
            SimpleLambdaExpressionSyntax simple => simple.Parameter,
            ParenthesizedLambdaExpressionSyntax { ParameterList.Parameters.Count: 1 } parenthesized => parenthesized.ParameterList.Parameters[0],
            _ => null
        };
        return parameter is null ? null : semanticModel.GetDeclaredSymbol(parameter) as IParameterSymbol;
    }

    /// <summary>
    /// Determines whether an expression is a member chain rooted in the given parameter.
    /// </summary>
    /// <param name="expression">The expression to inspect.</param>
    /// <param name="parameter">The parameter the chain must start from.</param>
    /// <param name="semanticModel">The semantic model of the expression.</param>
    /// <returns><see langword="true"/> when rooted in the parameter.</returns>
    public static bool IsRootedInParameter(
        ExpressionSyntax expression,
        IParameterSymbol parameter,
        SemanticModel semanticModel) => expression switch
        {
            IdentifierNameSyntax identifier => SymbolEqualityComparer.Default.Equals(
                semanticModel.GetSymbolInfo(identifier).Symbol,
                parameter),
            MemberAccessExpressionSyntax memberAccess => IsRootedInParameter(memberAccess.Expression, parameter, semanticModel),
            ParenthesizedExpressionSyntax parenthesized => IsRootedInParameter(parenthesized.Expression, parameter, semanticModel),
            CastExpressionSyntax cast => IsRootedInParameter(cast.Expression, parameter, semanticModel),
            _ => false
        };
}
