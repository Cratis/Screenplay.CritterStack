// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Cratis.Screenplay.Generation;
using Cratis.Screenplay.Generation.DotNet;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Cratis.CritterStack.Screenplay.Marten;

enum ProjectionKind
{
    Snapshot,
    SingleStream,
    MultiStream,
    Event
}

sealed record ProjectionRegistration(
    INamedTypeSymbol Model,
    INamedTypeSymbol? Projection,
    ProjectionKind Kind,
    Evidence Evidence);

static class MartenProjectionDiscovery
{
    static readonly string[] _singleStreamProjectionTypes =
    [
        WellKnownTypes.MartenSingleStreamProjectionOneId,
        WellKnownTypes.MartenSingleStreamProjectionTwoIds,
        "Marten.Events.Projections.SingleStreamProjection`1",
        "Marten.Events.Projections.SingleStreamProjection`2"
    ];

    public static IReadOnlyList<ProjectionRegistration> Discover(
        DotNetProjectCompilation project,
        AdapterIdentity adapter)
    {
        var registrations = new List<ProjectionRegistration>();
        foreach (var tree in project.Compilation.SyntaxTrees.Where(_ => !DotNetGeneratedSource.IsGenerated(_)))
        {
            var semanticModel = project.Compilation.GetSemanticModel(tree);
            foreach (var invocation in tree.GetRoot().DescendantNodes().OfType<InvocationExpressionSyntax>())
            {
                if (semanticModel.GetSymbolInfo(invocation).Symbol is not IMethodSymbol method ||
                    !IsMarten(method) ||
                    method.TypeArguments.Length == 0)
                {
                    continue;
                }

                if (method.TypeArguments[0] is not INamedTypeSymbol type)
                {
                    continue;
                }

                var evidence = new Evidence
                {
                    Adapter = adapter,
                    Strength = EvidenceStrength.Configured,
                    Source = DotNetSource.Range(invocation.GetLocation(), project.SourceRoot),
                    Explanation = $"Marten projection registration through {method.Name}"
                };

                switch (method.Name)
                {
                    case "Snapshot":
                    case "LiveStreamAggregation":
                        registrations.Add(new(type, null, ProjectionKind.Snapshot, evidence));
                        break;
                    case "Add":
                        var shape = ShapeOf(type);
                        if (shape is not null)
                        {
                            registrations.Add(shape with { Evidence = evidence });
                        }
                        break;
                }
            }
        }

        var catalog = new DotNetArtifactCatalog(project.Compilation);
        foreach (var projection in catalog.Types.Select(ShapeOf).Where(_ => _ is not null).Cast<ProjectionRegistration>())
        {
            if (registrations.Exists(_ => SymbolEqualityComparer.Default.Equals(_.Projection, projection.Projection)))
            {
                continue;
            }

            registrations.Add(projection with
            {
                Evidence = DotNetSource.EvidenceFor(
                    projection.Projection!,
                    adapter,
                    EvidenceStrength.Conventional,
                    project.SourceRoot,
                    "The type derives from a Marten projection base")
            });
        }

        return
        [
            .. registrations
                .OrderBy(_ => DotNetSubjectIds.MetadataName(_.Model), StringComparer.Ordinal)
                .ThenBy(_ => _.Projection is null ? string.Empty : DotNetSubjectIds.MetadataName(_.Projection), StringComparer.Ordinal)
                .ThenBy(_ => _.Kind)
        ];
    }

    public static ProjectionRegistration? ShapeOf(INamedTypeSymbol projection)
    {
        var singleStream = BaseClosing(projection, _singleStreamProjectionTypes);
        if (singleStream?.TypeArguments[0] is INamedTypeSymbol singleStreamModel)
        {
            return new(singleStreamModel, projection, ProjectionKind.SingleStream, null!);
        }

        var multiStream = BaseClosing(projection, [WellKnownTypes.MartenMultiStreamProjection]);
        if (multiStream?.TypeArguments[0] is INamedTypeSymbol multiStreamModel)
        {
            return new(multiStreamModel, projection, ProjectionKind.MultiStream, null!);
        }

        if (DotNetSymbols.IsOrInheritsFrom(projection, WellKnownTypes.MartenEventProjection))
        {
            return new(projection, projection, ProjectionKind.Event, null!);
        }

        return null;
    }

    static INamedTypeSymbol? BaseClosing(INamedTypeSymbol type, IEnumerable<string> metadataNames)
    {
        var names = metadataNames.ToHashSet(StringComparer.Ordinal);
        for (var current = type.BaseType; current is not null; current = current.BaseType)
        {
            if (names.Contains(DotNetSubjectIds.MetadataName(current.OriginalDefinition)))
            {
                return current;
            }
        }

        return null;
    }

    static bool IsMarten(IMethodSymbol method)
    {
        var candidate = method.ReducedFrom ?? method;
        return candidate.ContainingNamespace.ToDisplayString().StartsWith("Marten", StringComparison.Ordinal);
    }
}
