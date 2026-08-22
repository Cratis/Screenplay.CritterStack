// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Cratis.Screenplay.Generation;
using Cratis.Screenplay.Generation.DotNet;
using Microsoft.CodeAnalysis;

namespace Cratis.CritterStack.Screenplay;

static class ConceptTypeReferenceBinder
{
    public static IReadOnlyList<AdapterContribution> Bind(
        DotNetAnalysisContext context,
        IReadOnlyList<AdapterContribution> contributions)
    {
        var conceptSubjects = contributions
            .SelectMany(_ => _.Facts)
            .OfType<ArtifactFact>()
            .Where(_ => _.Definition.Key.Kind == ArtifactKind.Concept)
            .Select(_ => _.Subject)
            .ToHashSet();
        if (conceptSubjects.Count == 0)
        {
            return contributions;
        }

        var sourceTypes = context.Projects
            .SelectMany(project => new DotNetArtifactCatalog(project.Compilation).Types
                .Select(type => new { Subject = project.SubjectForType(type), Type = type }))
            .GroupBy(_ => _.Subject)
            .ToDictionary(_ => _.Key, _ => _.First().Type);

        return
        [
            .. contributions.Select(contribution => contribution with
            {
                Facts =
                [
                    .. contribution.Facts.Select(fact => fact is ArtifactFact artifact
                        ? BindArtifact(context, artifact, conceptSubjects, sourceTypes)
                        : fact)
                ]
            })
        ];
    }

    static ArtifactFact BindArtifact(
        DotNetAnalysisContext context,
        ArtifactFact artifact,
        IReadOnlySet<SubjectId> conceptSubjects,
        Dictionary<SubjectId, INamedTypeSymbol> sourceTypes)
    {
        if (!sourceTypes.TryGetValue(artifact.Subject, out var sourceType))
        {
            return artifact;
        }

        var sourceProperties = PropertiesOf(sourceType)
            .GroupBy(_ => PropertyName(_.Name), StringComparer.Ordinal)
            .ToDictionary(_ => _.Key, _ => _.First(), StringComparer.Ordinal);
        var properties = artifact.Definition.Properties
            .Select(property => BindProperty(context, property, conceptSubjects, sourceProperties))
            .ToArray();

        return artifact with { Definition = artifact.Definition with { Properties = properties } };
    }

    static PropertyDefinition BindProperty(
        DotNetAnalysisContext context,
        PropertyDefinition property,
        IReadOnlySet<SubjectId> conceptSubjects,
        Dictionary<string, IPropertySymbol> sourceProperties)
    {
        if (!sourceProperties.TryGetValue(property.Name, out var sourceProperty))
        {
            return property;
        }

        var type = DotNetTypeShapes.TypeReferenceFor(sourceProperty.Type, context);
        return type.Subject is not null && conceptSubjects.Contains(type.Subject)
            ? property with { Type = type }
            : property;
    }

    static IEnumerable<IPropertySymbol> PropertiesOf(INamedTypeSymbol type)
    {
        for (var current = type; current is not null; current = current.BaseType)
        {
            foreach (var property in current.GetMembers()
                         .OfType<IPropertySymbol>()
                         .Where(_ => !_.IsStatic &&
                                     !_.IsIndexer &&
                                     _.DeclaredAccessibility == Accessibility.Public &&
                                     _.GetMethod?.DeclaredAccessibility == Accessibility.Public)
                         .OrderBy(SourceOrder)
                         .ThenBy(_ => _.Name, StringComparer.Ordinal))
            {
                yield return property;
            }
        }
    }

    static int SourceOrder(ISymbol member) => member.Locations
        .Where(_ => _.IsInSource)
        .Select(_ => _.SourceSpan.Start)
        .DefaultIfEmpty(int.MaxValue)
        .Min();

    static string PropertyName(string value) => value.Length == 0
        ? value
        : $"{char.ToLowerInvariant(value[0])}{value[1..]}";
}
