// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Cratis.Screenplay.Generation.DotNet;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Cratis.CritterStack.Screenplay.Wolverine;

static class WolverineMethodSyntax
{
    public static IEnumerable<(MethodDeclarationSyntax Declaration, SemanticModel SemanticModel)> Declarations(
        IMethodSymbol method,
        DotNetProjectCompilation project)
    {
        var authoredDeclarations = DotNetSource.AuthoredDeclarationsOf(method, project.AuthoredSyntaxTrees).ToHashSet();
        foreach (var syntaxReference in method.DeclaringSyntaxReferences)
        {
            if (authoredDeclarations.Contains(syntaxReference) &&
                syntaxReference.GetSyntax() is MethodDeclarationSyntax declaration &&
                !DotNetGeneratedSource.IsGenerated(declaration.SyntaxTree))
            {
                yield return (declaration, project.Compilation.GetSemanticModel(declaration.SyntaxTree));
            }
        }
    }
}
