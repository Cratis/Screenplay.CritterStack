// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.CodeAnalysis;

namespace Cratis.CritterStack.Screenplay.Wolverine;

/// <summary>
/// Builds assembly-qualified metadata identities for symbols so that they compare across compilations.
/// </summary>
static class AuthoredSymbolKeys
{
    public static string ParameterKey(IParameterSymbol parameter) => $"{MemberKey(parameter.ContainingSymbol)}:{parameter.Ordinal}";

    public static string MethodKey(IMethodSymbol method) => MemberKey(method.OriginalDefinition);

    public static string MemberKey(ISymbol member) => $"{member.ContainingAssembly.Identity}:{member.OriginalDefinition.GetDocumentationCommentId()}";

    public static string TypeKey(ITypeSymbol type) => $"{type.ContainingAssembly.Identity}:{type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat)}";

    public static IEnumerable<INamedTypeSymbol> Hierarchy(INamedTypeSymbol type)
    {
        for (var current = type; current is not null; current = current.BaseType)
        {
            yield return current;
        }
    }
}
