// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Cratis.Screenplay.Generation.DotNet;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Cratis.CritterStack.Screenplay.Wolverine;

static class WolverineReturnTypes
{
    static readonly HashSet<string> _nonEventTypes =
    [
        "Microsoft.AspNetCore.Http.IResult",
        WellKnownTypes.WolverineEvents,
        WellKnownTypes.WolverineEventsToAppend,
        WellKnownTypes.WolverineOutgoingMessages,
        WellKnownTypes.WolverineStartStream,
        WellKnownTypes.WolverineSideEffect,
        WellKnownTypes.WolverineMartenOp,
        WellKnownTypes.WolverineResponseAware,
        WellKnownTypes.WolverineLegacyResponseAware,
        WellKnownTypes.WolverineReturnType,
        WellKnownTypes.WolverineLegacyReturnType,
        "Wolverine.Http.UpdatedAggregate",
        "Wolverine.Marten.UpdatedAggregate",
        "Wolverine.Persistence.EventSourcing.UpdatedAggregate"
    ];

    static readonly HashSet<string> _responseContracts =
    [
        "Microsoft.AspNetCore.Http.IResult",
        "Wolverine.IResponse",
        WellKnownTypes.WolverineResponseAware,
        WellKnownTypes.WolverineLegacyResponseAware
    ];

    public static IReadOnlyList<ITypeSymbol> CreatedValues(IMethodSymbol method)
    {
        var returnType = UnwrapTask(method.ReturnType);
        if (returnType is null)
        {
            return [];
        }

        if (returnType is INamedTypeSymbol tuple && tuple.IsTupleType)
        {
            return [.. tuple.TupleElements.Select(_ => _.Type)];
        }

        return [returnType];
    }

    public static IReadOnlyList<IMethodSymbol?> MartenFactories(IMethodSymbol method, DotNetProjectCompilation project, int slot) =>
    [
        .. WolverineMethodSyntax.Declarations(method, project)
            .SelectMany(declaration => ReturnedExpressions(declaration.Declaration)
                .Select(expression => MartenFactory(ReturnSlot(expression, declaration.SemanticModel, slot), declaration.SemanticModel, [])))
    ];

    public static ITypeSymbol? UnwrapTask(ITypeSymbol type)
    {
        if (type.SpecialType == SpecialType.System_Void)
        {
            return null;
        }

        if (type is not INamedTypeSymbol named)
        {
            return type;
        }

        var metadataName = DotNetSubjectIds.MetadataName(named.OriginalDefinition);
        return metadataName switch
        {
            "System.Threading.Tasks.Task" or "System.Threading.Tasks.ValueTask" => null,
            "System.Threading.Tasks.Task`1" or "System.Threading.Tasks.ValueTask`1" => named.TypeArguments[0],
            _ => type
        };
    }

    public static bool IsSpecialReturn(ITypeSymbol type)
    {
        if (type is not INamedTypeSymbol named)
        {
            return false;
        }

        var metadataName = DotNetSubjectIds.MetadataName(named.OriginalDefinition);
        if (HasResponseName(named) || _nonEventTypes.Contains(metadataName))
        {
            return true;
        }

        return named.AllInterfaces.Any(_ => _nonEventTypes.Contains(DotNetSubjectIds.MetadataName(_.OriginalDefinition)));
    }

    public static (INamedTypeSymbol? Model, bool IsCollection, bool IsOptional) QueryModel(ITypeSymbol returnType)
    {
        var unwrapped = UnwrapTask(returnType);
        if (unwrapped is null)
        {
            return (null, false, false);
        }

        var optional = unwrapped.NullableAnnotation == NullableAnnotation.Annotated;
        if (unwrapped is INamedTypeSymbol nullable && nullable.OriginalDefinition.SpecialType == SpecialType.System_Nullable_T)
        {
            unwrapped = nullable.TypeArguments[0];
            optional = true;
        }

        if (unwrapped is IArrayTypeSymbol array)
        {
            return (array.ElementType as INamedTypeSymbol, true, optional);
        }

        if (unwrapped is INamedTypeSymbol named)
        {
            var enumerable = named.AllInterfaces
                .Concat([named])
                .FirstOrDefault(_ =>
                    _.IsGenericType &&
                    DotNetSubjectIds.MetadataName(_.OriginalDefinition) == "System.Collections.Generic.IEnumerable`1");
            if (enumerable is not null)
            {
                return (enumerable.TypeArguments[0] as INamedTypeSymbol, true, optional);
            }

            return (named, false, optional);
        }

        return (null, false, optional);
    }

    public static bool IsHttpResponseType(INamedTypeSymbol type)
    {
        if (HasResponseName(type))
        {
            return true;
        }

        return _responseContracts.Any(contract => IsAssignableTo(type, contract));
    }

    public static bool IsAssignableTo(INamedTypeSymbol type, string metadataName)
    {
        for (var current = type; current is not null; current = current.BaseType)
        {
            if (DotNetSubjectIds.MetadataName(current.OriginalDefinition) == metadataName)
            {
                return true;
            }
        }

        return type.AllInterfaces.Any(@interface => DotNetSubjectIds.MetadataName(@interface.OriginalDefinition) == metadataName);
    }

    static bool HasResponseName(INamedTypeSymbol type) =>
        type.Name.StartsWith("UpdatedAggregate", StringComparison.Ordinal) ||
        type.Name.StartsWith("CreationResponse", StringComparison.Ordinal);

    static IEnumerable<ExpressionSyntax> ReturnedExpressions(MethodDeclarationSyntax declaration)
    {
        if (declaration.ExpressionBody is { } body)
        {
            yield return body.Expression;
        }

        foreach (var returned in declaration.DescendantNodes().OfType<ReturnStatementSyntax>())
        {
            if (returned.Expression is { } expression &&
                !returned.Ancestors().TakeWhile(_ => !ReferenceEquals(_, declaration)).Any(_ => _ is LocalFunctionStatementSyntax or AnonymousFunctionExpressionSyntax))
            {
                yield return expression;
            }
        }
    }

    static ExpressionSyntax? ReturnSlot(ExpressionSyntax expression, SemanticModel model, int slot)
    {
        expression = UnwrapExpression(expression, model);
        if (expression is TupleExpressionSyntax tuple)
        {
            return tuple.Arguments.ElementAtOrDefault(slot)?.Expression;
        }

        return slot == 0 ? expression : null;
    }

    static IMethodSymbol? MartenFactory(ExpressionSyntax? expression, SemanticModel model, HashSet<ILocalSymbol> visited)
    {
        if (expression is null)
        {
            return null;
        }

        expression = UnwrapExpression(expression, model);
        if (expression is InvocationExpressionSyntax invocation && model.GetSymbolInfo(invocation).Symbol is IMethodSymbol factory)
        {
            return IsMartenFactory(factory) ? factory : null;
        }

        return LocalMartenFactory(expression, model, visited);
    }

    static bool IsMartenFactory(IMethodSymbol factory) =>
        factory.IsStatic && DotNetSubjectIds.MetadataName(factory.ContainingType) == WellKnownTypes.WolverineMartenOps;

    static IMethodSymbol? LocalMartenFactory(ExpressionSyntax expression, SemanticModel model, HashSet<ILocalSymbol> visited)
    {
        if (model.GetSymbolInfo(expression).Symbol is not ILocalSymbol local || !visited.Add(local))
        {
            return null;
        }

        if (HasReassignedLocal(expression, local, model))
        {
            return null;
        }

        var initializer = local.DeclaringSyntaxReferences.Select(_ => _.GetSyntax())
            .OfType<VariableDeclaratorSyntax>().Select(_ => _.Initializer?.Value).FirstOrDefault();
        return MartenFactory(initializer, model, visited);
    }

    static bool HasReassignedLocal(ExpressionSyntax expression, ILocalSymbol local, SemanticModel model)
    {
        var declaration = expression.FirstAncestorOrSelf<MethodDeclarationSyntax>();
        return declaration?.DescendantNodes().OfType<AssignmentExpressionSyntax>()
            .Any(_ => SymbolEqualityComparer.Default.Equals(model.GetSymbolInfo(_.Left).Symbol, local)) != false ||
            declaration.DescendantNodes().OfType<ArgumentSyntax>()
                .Any(_ => _.RefKindKeyword.RawKind != 0 && SymbolEqualityComparer.Default.Equals(model.GetSymbolInfo(_.Expression).Symbol, local));
    }

    static ExpressionSyntax UnwrapExpression(ExpressionSyntax expression, SemanticModel model)
    {
        while (true)
        {
            switch (expression)
            {
                case ParenthesizedExpressionSyntax parenthesized:
                    expression = parenthesized.Expression;
                    break;
                case CastExpressionSyntax cast:
                    expression = cast.Expression;
                    break;
                case AwaitExpressionSyntax awaited:
                    expression = awaited.Expression;
                    break;
                case InvocationExpressionSyntax invocation when IsTaskResult(invocation, model):
                    expression = invocation.ArgumentList.Arguments[0].Expression;
                    break;
                default:
                    return expression;
            }
        }
    }

    static bool IsTaskResult(InvocationExpressionSyntax invocation, SemanticModel model) =>
        model.GetSymbolInfo(invocation).Symbol is IMethodSymbol factory &&
        factory.Name == "FromResult" &&
        (DotNetSubjectIds.MetadataName(factory.ContainingType) == "System.Threading.Tasks.Task" ||
         DotNetSubjectIds.MetadataName(factory.ContainingType) == "System.Threading.Tasks.ValueTask") &&
        invocation.ArgumentList.Arguments.Count == 1;
}
