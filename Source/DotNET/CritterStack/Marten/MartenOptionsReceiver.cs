// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Cratis.Screenplay.Generation.DotNet;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Cratis.CritterStack.Screenplay.Marten;

static class MartenOptionsReceiver
{
    public static ISymbol? RootSymbol(SyntaxNode syntax, SemanticModel model) => syntax switch
    {
        MemberAccessExpressionSyntax access => RootSymbol(access.Expression, model),
        InvocationExpressionSyntax invocation => RootSymbol(invocation.Expression, model),
        ArgumentSyntax argument => RootSymbol(argument.Expression, model),
        ParenthesizedExpressionSyntax parenthesized => RootSymbol(parenthesized.Expression, model),
        CastExpressionSyntax cast => RootSymbol(cast.Expression, model),
        _ => Resolve(model.GetSymbolInfo(syntax).Symbol, syntax.SyntaxTree, model)
    };

    static ISymbol? Resolve(ISymbol? symbol, SyntaxTree tree, SemanticModel model) => symbol switch
    {
        IParameterSymbol parameter when CompositeCall(parameter, model) is { } call => RootSymbol(call.Expression, model),
        ILocalSymbol local => LocalRoot(local, tree, model),
        _ => symbol
    };

    static ISymbol? LocalRoot(ILocalSymbol local, SyntaxTree tree, SemanticModel model)
    {
        if (local.DeclaringSyntaxReferences.SingleOrDefault()?.GetSyntax() is not VariableDeclaratorSyntax
            { Initializer.Value: IdentifierNameSyntax alias })
        {
            return local;
        }
        return MartenStoreOptionsBinding.IsReassigned(local, tree, model) ? null : model.GetSymbolInfo(alias).Symbol;
    }

    static InvocationExpressionSyntax? CompositeCall(IParameterSymbol parameter, SemanticModel model)
    {
        if (parameter.Type is not INamedTypeSymbol type || DotNetSubjectIds.MetadataName(type) != "Marten.Events.Projections.CompositeProjection") return null;
        var lambda = parameter.DeclaringSyntaxReferences.FirstOrDefault()?.GetSyntax().Ancestors().OfType<LambdaExpressionSyntax>().FirstOrDefault();
        var call = (lambda?.Parent as ArgumentSyntax)?.Parent?.Parent as InvocationExpressionSyntax;
        return call is not null && model.GetSymbolInfo(call).Symbol is IMethodSymbol method && method.Name == "CompositeProjectionFor" ? call : null;
    }
}
