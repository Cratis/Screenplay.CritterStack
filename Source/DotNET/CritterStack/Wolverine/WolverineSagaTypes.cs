// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Cratis.Screenplay.Generation.DotNet;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Cratis.CritterStack.Screenplay.Wolverine;

static class WolverineSagaTypes
{
    public static bool IsSagaState(INamedTypeSymbol type, DotNetProjectCompilation project)
    {
        if (project.Compilation.GetTypeByMetadataName(WellKnownTypes.WolverineSaga) is not { } sagaType ||
            !WolverineSymbolAuthority.IsAuthoredOrMetadataSymbol(sagaType.OriginalDefinition, project))
        {
            return false;
        }

        return IsAuthoredOrMetadataAssignableTo(
            type,
            sagaType,
            project,
            new HashSet<INamedTypeSymbol>(SymbolEqualityComparer.Default));
    }

    static bool IsAuthoredOrMetadataAssignableTo(
        INamedTypeSymbol type,
        INamedTypeSymbol target,
        DotNetProjectCompilation project,
        HashSet<INamedTypeSymbol> visited)
    {
        if (!visited.Add(type))
        {
            return false;
        }

        if (SymbolEqualityComparer.Default.Equals(type.OriginalDefinition, target.OriginalDefinition))
        {
            return true;
        }

        if (type.DeclaringSyntaxReferences.Length == 0)
        {
            return type.BaseType is not null && IsAuthoredOrMetadataAssignableTo(type.BaseType, target, project, visited);
        }

        foreach (var syntaxReference in type.DeclaringSyntaxReferences)
        {
            if (syntaxReference.GetSyntax() is not TypeDeclarationSyntax { BaseList: not null } declaration ||
                !project.AuthoredSyntaxTrees.Contains(declaration.SyntaxTree) ||
                DotNetGeneratedSource.IsGenerated(declaration.SyntaxTree))
            {
                continue;
            }

            var semanticModel = project.Compilation.GetSemanticModel(declaration.SyntaxTree);
            foreach (var baseType in declaration.BaseList.Types)
            {
                if (semanticModel.GetTypeInfo(baseType.Type).Type is INamedTypeSymbol candidate &&
                    IsAuthoredOrMetadataAssignableTo(candidate, target, project, visited))
                {
                    return true;
                }
            }
        }

        return false;
    }
}
