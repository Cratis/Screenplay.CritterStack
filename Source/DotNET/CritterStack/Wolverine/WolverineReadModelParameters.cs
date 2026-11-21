// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Cratis.Screenplay.Generation;
using Cratis.Screenplay.Generation.DotNet;
using Microsoft.CodeAnalysis;

namespace Cratis.CritterStack.Screenplay.Wolverine;

static class WolverineReadModelParameters
{
    public static AttributeData? ReadAttribute(IParameterSymbol parameter, DotNetProjectCompilation project) =>
        AttributeFor(parameter, project, WellKnownTypes.WolverineReadModelAttribute) ??
        AttributeFor(parameter, project, WellKnownTypes.WolverineLegacyReadAggregateAttribute);

    public static AttributeData? ConsistentAttribute(ISymbol symbol, DotNetProjectCompilation project) =>
        AttributeFor(symbol, project, WellKnownTypes.WolverineLegacyConsistentAggregateAttribute) ??
        AttributeFor(symbol, project, WellKnownTypes.WolverineHttpConsistentAggregateAttribute) ??
        AttributeFor(symbol, project, WellKnownTypes.WolverineConsistentAggregateHandlerAttribute);

    public static AttributeData? AttributeFor(
        ISymbol symbol,
        DotNetProjectCompilation project,
        string metadataName)
    {
        var definition = project.Compilation.GetTypeByMetadataName(metadataName);
        if (definition is null)
        {
            return null;
        }

        return symbol.GetAttributes().FirstOrDefault(attribute => IsAdmitted(attribute, definition, project));
    }

    public static bool EnforcesConsistency(AttributeData attribute)
    {
        var value = attribute.NamedArguments.FirstOrDefault(argument => argument.Key == "AlwaysEnforceConsistency").Value.Value;
        return value is not false;
    }

    public static SourceRange? SourceOf(AttributeData attribute, DotNetProjectCompilation project)
    {
        var syntax = attribute.ApplicationSyntaxReference?.GetSyntax();
        return syntax is null ? null : CritterStackSource.RangeForProject(syntax.GetLocation(), project);
    }

    static bool IsAdmitted(AttributeData attribute, INamedTypeSymbol definition, DotNetProjectCompilation project)
    {
        if (!IsAuthored(attribute, project))
        {
            return false;
        }

        for (var type = attribute.AttributeClass; type is not null; type = type.BaseType)
        {
            if (!WolverineSymbolAuthority.IsAuthoredOrMetadataSymbol(type, project))
            {
                return false;
            }

            if (SymbolEqualityComparer.Default.Equals(type.OriginalDefinition, definition))
            {
                return true;
            }
        }

        return false;
    }

    static bool IsAuthored(AttributeData attribute, DotNetProjectCompilation project)
    {
        var tree = attribute.ApplicationSyntaxReference?.SyntaxTree;
        if (tree is null)
        {
            return false;
        }

        if (!project.AuthoredSyntaxTrees.Contains(tree))
        {
            return false;
        }

        return !DotNetGeneratedSource.IsGenerated(tree);
    }
}
