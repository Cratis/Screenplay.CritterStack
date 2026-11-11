// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Cratis.Screenplay.Generation;
using Cratis.Screenplay.Generation.DotNet;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Operations;

namespace Cratis.CritterStack.Screenplay.Wolverine;

internal static partial class WolverineDcb
{
    static IParameterSymbol? ReceiverParameter(IOperation? receiver)
    {
        var current = receiver;
        while (true)
        {
            switch (current)
            {
                case IParameterReferenceOperation parameter:
                    return parameter.Parameter;
                case IParenthesizedOperation parenthesized:
                    current = parenthesized.Operand;
                    break;
                case IConversionOperation conversion when IsTransparentConversion(conversion):
                    current = conversion.Operand;
                    break;
                default:
                    return null;
            }
        }
    }

    static bool IsTransparentConversion(IConversionOperation conversion) =>
        !conversion.Conversion.IsUserDefined &&
        (conversion.Conversion.IsIdentity || conversion.Conversion.IsReference) &&
        !IsObject(conversion.Type) &&
        !IsObject(conversion.Operand.Type) &&
        conversion.Operand.Type?.TypeKind != TypeKind.Dynamic;

    static bool IsObject(ITypeSymbol? type) => type?.SpecialType == SpecialType.System_Object;

    static IOperation? Unwrap(IOperation? operation)
    {
        var current = operation;
        while (true)
        {
            switch (current)
            {
                case IConversionOperation conversion when !conversion.Conversion.IsUserDefined:
                    current = conversion.Operand;
                    break;
                case IParenthesizedOperation parenthesized:
                    current = parenthesized.Operand;
                    break;
                default:
                    return current;
            }
        }
    }

    static IOperation? UnwrapReturn(IOperation? operation)
    {
        var current = Unwrap(operation);
        while (current is IAwaitOperation awaitOperation)
        {
            current = Unwrap(awaitOperation.Operation);
        }

        return current;
    }

    static bool IsInDirectMethodBody(SyntaxNode node, MethodDeclarationSyntax declaration) =>
        !node.Ancestors()
            .TakeWhile(_ => _ != declaration)
            .Any(_ => _ is LocalFunctionStatementSyntax or AnonymousFunctionExpressionSyntax);

    static bool IsAuthoredSourceType(INamedTypeSymbol type, DotNetProjectCompilation project) =>
        type.Locations.Any(location => IsAuthoredLocation(location, project));

    static bool IsAuthoredLocation(Location location, DotNetProjectCompilation project) =>
        location.IsInSource &&
        location.SourceTree is not null &&
        project.AuthoredSyntaxTrees.Contains(location.SourceTree) &&
        !DotNetGeneratedSource.IsGenerated(location.SourceTree);

    static SourceRange? SourceOf(AttributeData? attribute, IParameterSymbol parameter, DotNetProjectCompilation project)
    {
        if (attribute?.ApplicationSyntaxReference?.GetSyntax() is { } syntax)
        {
            return CritterStackSource.RangeForProject(syntax.GetLocation(), project);
        }

        return SourceOf(parameter, project);
    }

    static SourceRange? SourceOf(ISymbol symbol, DotNetProjectCompilation project) =>
        symbol.Locations.FirstOrDefault(location => IsAuthoredLocation(location, project)) is { } authored
            ? CritterStackSource.RangeForProject(authored, project)
            : null;

    static SourceRange? SourceOf(IInvocationOperation invocation, SemanticModel semanticModel, DotNetProjectCompilation project)
    {
        var syntax = invocation.Syntax;
        return semanticModel.SyntaxTree == syntax.SyntaxTree
            ? CritterStackSource.RangeForProject(syntax.GetLocation(), project)
            : null;
    }

    static string SourcePathOf(ISymbol symbol, DotNetProjectCompilation project) =>
        SourceOf(symbol, project)?.Path ?? string.Empty;

    static int SourcePositionOf(ISymbol symbol, DotNetProjectCompilation project) => symbol.Locations
        .Where(location => location.IsInSource && location.SourceTree is not null && project.AuthoredSyntaxTrees.Contains(location.SourceTree))
        .Select(location => location.SourceSpan.Start)
        .DefaultIfEmpty(int.MaxValue)
        .Min();

    static string LowerFirst(string value) => value.Length == 0
        ? value
        : $"{char.ToLowerInvariant(value[0])}{value[1..]}";
}
