// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Cratis.Screenplay.Generation;
using Cratis.Screenplay.Generation.DotNet;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Cratis.CritterStack.Screenplay.Marten;

static class MartenDocumentMembers
{
    public static string PropertyName(string name) => name.Length == 0 ? name : char.ToLowerInvariant(name[0]) + name[1..];

    /// <summary>
    /// Projects the compatibility type reference of a document member as a type use, so the member keeps exactly the
    /// name, subject, optionality and collection flags the compatibility reader reports.
    /// </summary>
    /// <param name="type">The member type.</param>
    /// <param name="context">The analysis context resolving source subjects.</param>
    /// <param name="member">The artifact member the type use describes.</param>
    /// <returns>The member type use definition.</returns>
    public static ArtifactMemberTypeUseDefinition TypeUseFor(ITypeSymbol type, DotNetAnalysisContext context, ArtifactMemberKey member)
    {
        // An array of arrays has no printable element name; the legacy reference is requested explicitly, with the array origins read from the symbols.
        if (DotNetTypeUseCompatibility.TryCreateLegacyJaggedArrayMember(type, context, member, out var jagged))
        {
            return jagged;
        }

        return new() { Member = member, Type = TypeUseFor(type, context) };
    }

    public static ISymbol? ResolveIdentityMember(
        InvocationExpressionSyntax invocation,
        SemanticModel semanticModel,
        INamedTypeSymbol documentType)
    {
        if (invocation.ArgumentList.Arguments.FirstOrDefault()?.Expression is not LambdaExpressionSyntax lambda ||
            lambda.ExpressionBody is null)
        {
            return null;
        }

        var expression = Unwrap(lambda.ExpressionBody);
        if (expression is not MemberAccessExpressionSyntax memberAccess)
        {
            return null;
        }

        if (Unwrap(memberAccess.Expression) is not IdentifierNameSyntax identifier ||
            !string.Equals(identifier.Identifier.ValueText, LambdaParameterName(lambda), StringComparison.Ordinal))
        {
            return null;
        }

        var member = semanticModel.GetSymbolInfo(memberAccess).Symbol;
        return member is IPropertySymbol or IFieldSymbol && IsMemberOfDocumentType(member, documentType)
            ? member
            : null;
    }

    public static IReadOnlyList<IPropertySymbol> PropertiesOf(INamedTypeSymbol documentType)
    {
        var properties = new List<IPropertySymbol>();
        for (var current = documentType; current is not null; current = current.BaseType)
        {
            properties.AddRange(current.GetMembers()
                .OfType<IPropertySymbol>()
                .Where(_ => IsPubliclyReadable(_) &&
                            properties.TrueForAll(existing => !string.Equals(existing.Name, _.Name, StringComparison.Ordinal)))
                .OrderBy(SourceOrder)
                .ThenBy(_ => _.Name, StringComparer.Ordinal));
        }

        return properties;
    }

    public static ISymbol? ConventionalIdentityMember(INamedTypeSymbol documentType)
    {
        var members = MembersOf(documentType).ToArray();
        var attributedProperty = members.OfType<IPropertySymbol>().FirstOrDefault(HasIdentityAttribute);
        var attributedField = members.OfType<IFieldSymbol>().FirstOrDefault(HasIdentityAttribute);
        var idProperty = members.OfType<IPropertySymbol>().FirstOrDefault(_ => string.Equals(_.Name, "Id", StringComparison.OrdinalIgnoreCase));
        var idField = members.OfType<IFieldSymbol>().FirstOrDefault(_ => string.Equals(_.Name, "Id", StringComparison.OrdinalIgnoreCase));
        return (ISymbol?)attributedProperty ?? (ISymbol?)attributedField ?? (ISymbol?)idProperty ?? idField;
    }

    public static bool IsIdentityConfiguration(IMethodSymbol method)
    {
        var candidate = method.ReducedFrom ?? method;
        return candidate.Name == "Identity" &&
               DotNetSubjectIds.MetadataName(candidate.ContainingType.OriginalDefinition) == WellKnownTypes.MartenDocumentMappingExpression;
    }

    static TypeUseDefinition TypeUseFor(ITypeSymbol type, DotNetAnalysisContext context)
    {
        var reference = DotNetTypeShapes.TypeReferenceFor(type, context);
        if (reference.Name.Length == 0)
        {
            // Any other shape the compatibility projection cannot name stays the exact observation, which keeps its ordinary unsupported-shape diagnostic.
            return DotNetTypeShapes.TypeUseFor(type, context);
        }

        List<TypeUseShapeKind> shape = [];
        if (reference.IsOptional)
        {
            shape.Add(TypeUseShapeKind.Optional);
        }

        if (reference.IsCollection)
        {
            shape.Add(TypeUseShapeKind.Collection);
        }

        shape.Add(TypeUseShapeKind.Named);
        return new() { Name = reference.Name, ObservedTypeSubject = reference.Subject, Shape = shape };
    }

    static ExpressionSyntax Unwrap(ExpressionSyntax expression)
    {
        while (true)
        {
            expression = expression switch
            {
                ParenthesizedExpressionSyntax parenthesized => parenthesized.Expression,
                CastExpressionSyntax cast => cast.Expression,
                _ => expression
            };
            if (expression is not (ParenthesizedExpressionSyntax or CastExpressionSyntax))
            {
                return expression;
            }
        }
    }

    static string? LambdaParameterName(LambdaExpressionSyntax lambda) => lambda switch
    {
        SimpleLambdaExpressionSyntax simple => simple.Parameter.Identifier.ValueText,
        ParenthesizedLambdaExpressionSyntax { ParameterList.Parameters.Count: 1 } parenthesized =>
            parenthesized.ParameterList.Parameters[0].Identifier.ValueText,
        _ => null
    };

    static bool IsMemberOfDocumentType(ISymbol member, INamedTypeSymbol documentType)
    {
        for (var current = documentType; current is not null; current = current.BaseType)
        {
            if (SymbolEqualityComparer.Default.Equals(member.ContainingType, current))
            {
                return true;
            }
        }

        return false;
    }

    static IEnumerable<ISymbol> MembersOf(INamedTypeSymbol type)
    {
        for (var current = type; current is not null; current = current.BaseType)
        {
            foreach (var member in current.GetMembers().Where(_ => !_.IsStatic))
            {
                yield return member;
            }
        }
    }

    static bool HasIdentityAttribute(ISymbol member) => member.GetAttributes().Any(_ =>
        _.AttributeClass is not null &&
        DotNetSubjectIds.MetadataName(_.AttributeClass) == WellKnownTypes.JasperFxIdentityAttribute);

    static bool IsPubliclyReadable(IPropertySymbol property) => property is
    {
        IsStatic: false,
        IsIndexer: false,
        DeclaredAccessibility: Accessibility.Public,
        GetMethod.DeclaredAccessibility: Accessibility.Public
    };

    static int SourceOrder(ISymbol member) => member.Locations
        .Where(_ => _.IsInSource)
        .Select(_ => _.SourceSpan.Start)
        .DefaultIfEmpty(int.MaxValue)
        .Min();
}
