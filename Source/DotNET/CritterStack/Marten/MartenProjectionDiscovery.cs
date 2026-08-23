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
    Event,
    Custom
}

sealed record ProjectionRegistration(
    INamedTypeSymbol Model,
    INamedTypeSymbol? Projection,
    ProjectionKind Kind,
    Evidence Evidence)
{
    public string? Lifecycle { get; init; }
}

static class MartenProjectionDiscovery
{
    static readonly string[] _singleStreamProjectionTypes =
    [
        WellKnownTypes.MartenSingleStreamProjectionOneId,
        WellKnownTypes.MartenSingleStreamProjectionTwoIds,
        "Marten.Events.Projections.SingleStreamProjection`1",
        "Marten.Events.Projections.SingleStreamProjection`2"
    ];
    static readonly HashSet<string> _projectionLifecycleTypes =
    [
        WellKnownTypes.JasperFxProjectionLifecycle,
        WellKnownTypes.JasperFxSnapshotLifecycle,
        WellKnownTypes.MartenProjectionLifecycle,
        WellKnownTypes.MartenSnapshotLifecycle
    ];

    internal static IReadOnlySet<string> ProjectionLifecycleTypes => _projectionLifecycleTypes;

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
                    !IsMarten(method, invocation, semanticModel))
                {
                    continue;
                }

                var type = ProjectionTypeFrom(method, invocation, semanticModel);
                if (type is null)
                {
                    continue;
                }

                var evidence = new Evidence
                {
                    Adapter = adapter,
                    Strength = EvidenceStrength.Configured,
                    Source = CritterStackSource.RangeForProject(invocation.GetLocation(), project),
                    Explanation = $"Marten projection registration through {method.Name}"
                };

                switch (method.Name)
                {
                    case "Snapshot":
                    case "LiveStreamAggregation":
                        registrations.Add(new(type, null, ProjectionKind.Snapshot, evidence)
                        {
                            Lifecycle = string.Equals(method.Name, "LiveStreamAggregation", StringComparison.Ordinal)
                                ? "Live"
                                : LifecycleFrom(invocation, semanticModel)
                        });
                        break;
                    case "Add":
                    case "AddProjectionWithServices":
                        var shape = ShapeOf(type) ?? CustomShapeOf(type);
                        if (shape is not null)
                        {
                            registrations.Add(shape with
                            {
                                Evidence = evidence,
                                Lifecycle = LifecycleFrom(invocation, semanticModel)
                            });
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
                Evidence = CritterStackSource.EvidenceFor(
                    projection.Projection!,
                    adapter,
                    project,
                    EvidenceStrength.Conventional,
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

        if (DotNetSubjectIds.MetadataName(projection) != WellKnownTypes.MartenEventProjection &&
            DotNetSymbols.IsOrInheritsFrom(projection, WellKnownTypes.MartenEventProjection))
        {
            return new(projection, projection, ProjectionKind.Event, null!);
        }

        return null;
    }

    static ProjectionRegistration? CustomShapeOf(INamedTypeSymbol projection) =>
        DotNetSubjectIds.MetadataName(projection) != WellKnownTypes.MartenProjection &&
        projection.AllInterfaces.Any(_ => DotNetSubjectIds.MetadataName(_.OriginalDefinition) == WellKnownTypes.MartenProjection)
            ? new(projection, projection, ProjectionKind.Custom, null!)
            : null;

    static INamedTypeSymbol? ProjectionTypeFrom(
        IMethodSymbol method,
        InvocationExpressionSyntax invocation,
        SemanticModel semanticModel)
    {
        if (method.TypeArguments.FirstOrDefault() is INamedTypeSymbol typeArgument)
        {
            return typeArgument;
        }

        return invocation.ArgumentList.Arguments
            .Select(_ => semanticModel.GetTypeInfo(_.Expression).Type)
            .OfType<INamedTypeSymbol>()
            .FirstOrDefault(_ => ShapeOf(_) is not null || CustomShapeOf(_) is not null);
    }

    static string? LifecycleFrom(InvocationExpressionSyntax invocation, SemanticModel semanticModel)
    {
        foreach (var argument in invocation.ArgumentList.Arguments)
        {
            if (semanticModel.GetSymbolInfo(argument.Expression).Symbol is IFieldSymbol field &&
                _projectionLifecycleTypes.Contains(DotNetSubjectIds.MetadataName(field.ContainingType)))
            {
                return field.Name;
            }

            if (semanticModel.GetTypeInfo(argument.Expression).ConvertedType is not INamedTypeSymbol enumType ||
                !_projectionLifecycleTypes.Contains(DotNetSubjectIds.MetadataName(enumType)) ||
                semanticModel.GetConstantValue(argument.Expression) is not { HasValue: true, Value: not null } constant)
            {
                continue;
            }

            var member = enumType.GetMembers()
                .OfType<IFieldSymbol>()
                .FirstOrDefault(_ => _.HasConstantValue && Equals(_.ConstantValue, constant.Value));
            if (member is not null)
            {
                return member.Name;
            }
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

    static bool IsMarten(
        IMethodSymbol method,
        InvocationExpressionSyntax invocation,
        SemanticModel semanticModel)
    {
        var candidate = method.ReducedFrom ?? method;
        if (candidate.ContainingNamespace.ToDisplayString().StartsWith("Marten", StringComparison.Ordinal))
        {
            return true;
        }

        return invocation.Expression is MemberAccessExpressionSyntax memberAccess &&
               semanticModel.GetTypeInfo(memberAccess.Expression).Type is INamedTypeSymbol receiver &&
               DotNetSubjectIds.MetadataName(receiver.OriginalDefinition) == WellKnownTypes.MartenProjectionOptions;
    }
}
