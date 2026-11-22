// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Cratis.Screenplay.Generation.DotNet;
using Microsoft.CodeAnalysis;
using static Cratis.CritterStack.Screenplay.Wolverine.AuthoredSymbolKeys;

namespace Cratis.CritterStack.Screenplay.Wolverine;

/// <summary>
/// Resolves runtime types and implementations for authored dispatch across compilations.
/// </summary>
static class AuthoredTypeBinding
{
    public static INamedTypeSymbol? Construct(AuthoredDispatch dispatch, Dictionary<ITypeParameterSymbol, ITypeSymbol> bindings)
    {
        if (!dispatch.Type.IsGenericType)
        {
            return dispatch.Type;
        }

        if (dispatch.Type.ContainingType is not null || dispatch.Type.TypeParameters.Any(HasConstraint))
        {
            return null;
        }

        var arguments = ResolveAll(dispatch.Type.TypeParameters.Select(parameter => bindings.TryGetValue(parameter, out var type) ? ResolveType(type, dispatch.Project.Compilation) : null));
        return arguments is null ? null : dispatch.Type.Construct(arguments);
    }

    public static ITypeSymbol? ResolveType(ITypeSymbol type, Compilation compilation)
    {
        if (type is not INamedTypeSymbol named || SameAssemblyDefinition(named, compilation) is not { } definition)
        {
            return null;
        }

        if (!named.IsGenericType)
        {
            return definition;
        }

        var arguments = ResolveAll(named.TypeArguments.Select(argument => ResolveType(argument, compilation)));
        return arguments is null ? null : definition.Construct(arguments);
    }

    public static bool BindTypes(ITypeSymbol pattern, ITypeSymbol actual, Dictionary<ITypeParameterSymbol, ITypeSymbol> bindings)
    {
        if (pattern is ITypeParameterSymbol parameter)
        {
            return BindParameter(parameter, actual, bindings);
        }

        return pattern is INamedTypeSymbol left && actual is INamedTypeSymbol right &&
            MemberKey(left.OriginalDefinition) == MemberKey(right.OriginalDefinition) &&
            left.TypeArguments.Length == right.TypeArguments.Length &&
            left.TypeArguments.Zip(right.TypeArguments).All(pair => BindTypes(pair.First, pair.Second, bindings));
    }

    public static IMethodSymbol? ImplementationOf(INamedTypeSymbol runtimeType, IMethodSymbol contract)
    {
        if (contract.ContainingType.TypeKind == TypeKind.Interface)
        {
            return runtimeType.FindImplementationForInterfaceMember(contract) as IMethodSymbol;
        }

        return Hierarchy(runtimeType).SelectMany(type => type.GetMembers().OfType<IMethodSymbol>())
            .FirstOrDefault(method => Overrides(method, contract));
    }

    static INamedTypeSymbol? SameAssemblyDefinition(INamedTypeSymbol named, Compilation compilation)
    {
        var definition = compilation.GetTypeByMetadataName(DotNetSubjectIds.MetadataName(named.OriginalDefinition));
        return definition?.ContainingAssembly.Identity.Equals(named.ContainingAssembly.Identity) == true ? definition : null;
    }

    static bool BindParameter(ITypeParameterSymbol parameter, ITypeSymbol actual, Dictionary<ITypeParameterSymbol, ITypeSymbol> bindings)
    {
        if (bindings.TryGetValue(parameter, out var previous))
        {
            return TypeKey(previous) == TypeKey(actual);
        }

        bindings.Add(parameter, actual);
        return true;
    }

    static bool HasConstraint(ITypeParameterSymbol parameter) =>
        parameter.HasReferenceTypeConstraint || parameter.HasValueTypeConstraint || parameter.HasUnmanagedTypeConstraint ||
        parameter.HasConstructorConstraint || parameter.HasNotNullConstraint || parameter.ConstraintTypes.Length > 0;

    static ITypeSymbol[]? ResolveAll(IEnumerable<ITypeSymbol?> types)
    {
        var resolved = types.ToArray();
        return resolved.Any(type => type is null) ? null : [.. resolved.OfType<ITypeSymbol>()];
    }

    static bool Overrides(IMethodSymbol method, IMethodSymbol contract)
    {
        for (var current = method; current is not null; current = current.OverriddenMethod)
        {
            if (MethodKey(current) == MethodKey(contract))
            {
                return true;
            }
        }

        return false;
    }
}
