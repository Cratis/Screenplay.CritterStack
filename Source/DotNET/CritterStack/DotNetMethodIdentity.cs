// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Cratis.Screenplay.Generation;
using Cratis.Screenplay.Generation.DotNet;
using Microsoft.CodeAnalysis;

namespace Cratis.CritterStack.Screenplay;

static class DotNetMethodIdentity
{
    public static SubjectId SubjectFor(DotNetProjectCompilation project, IMethodSymbol method) => new()
    {
        Value = $"{project.SubjectForType(method.ContainingType).Value}#method:{Uri.EscapeDataString(DocumentationId(method))}"
    };

    public static string DisplayName(IMethodSymbol method)
    {
        var containingType = TypeName(method.ContainingType, method.ContainingNamespace);
        var arity = method.Arity == 0 ? string.Empty : $"`{method.Arity}";
        var parameters = string.Join(", ", method.Parameters.Select(parameter => ParameterName(parameter, method.ContainingNamespace)));

        return $"{containingType}.{method.Name}{arity}({parameters})";
    }

    static string DocumentationId(IMethodSymbol method) =>
        method.GetDocumentationCommentId() ?? DisplayName(method);

    static string ParameterName(IParameterSymbol parameter, INamespaceSymbol localNamespace)
    {
        var modifier = parameter.IsParams
            ? "params "
            : parameter.RefKind switch
            {
                RefKind.Ref => "ref ",
                RefKind.RefReadOnlyParameter => "ref readonly ",
                RefKind.In => "in ",
                RefKind.Out => "out ",
                _ => string.Empty
            };

        return $"{modifier}{TypeName(parameter.Type, localNamespace)}";
    }

    static string TypeName(ITypeSymbol type, INamespaceSymbol localNamespace)
    {
        var format = SymbolEqualityComparer.Default.Equals(type.ContainingNamespace, localNamespace)
            ? SymbolDisplayFormat.MinimallyQualifiedFormat
            : SymbolDisplayFormat.FullyQualifiedFormat;

        return type.ToDisplayString(format).Replace("global::", string.Empty, StringComparison.Ordinal);
    }
}
