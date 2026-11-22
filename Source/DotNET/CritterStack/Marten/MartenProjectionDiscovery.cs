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

    public BackendOwnership Ownership { get; init; } = BackendOwnership.Legacy;
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
        AdapterIdentity adapter,
        MartenStoreIndex stores)
    {
        var registrations = new List<ProjectionRegistration>();
        foreach (var tree in project.Compilation.SyntaxTrees.Where(tree => project.AuthoredSyntaxTrees.Contains(tree) && !DotNetGeneratedSource.IsGenerated(tree)))
        {
            registrations.AddRange(RegistrationsIn(tree, project, adapter, stores));
        }

        AddConventionalRegistrations(registrations, project, adapter, stores);
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
        if (MartenProjectionKindDiscovery.IsOpaqueProjection(projection))
        {
            return null;
        }

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

        return IsDerivedEventProjection(projection)
            ? new(projection, projection, ProjectionKind.Event, null!)
            : null;
    }

    internal static ProjectionRegistration? RegistrationFrom(
        InvocationExpressionSyntax invocation,
        SemanticModel semanticModel,
        DotNetProjectCompilation project,
        AdapterIdentity adapter)
    {
        if (MartenRegistrationCall(invocation, semanticModel) is not var (method, type))
        {
            return null;
        }

        var evidence = new Evidence
        {
            Adapter = adapter,
            Strength = EvidenceStrength.Configured,
            Source = CritterStackSource.RangeForProject(invocation.GetLocation(), project),
            Explanation = $"Marten projection registration through {method.Name}"
        };

        return method.Name switch
        {
            "Snapshot" => SnapshotRegistration(type, evidence, LifecycleFrom(invocation, semanticModel)),
            "LiveStreamAggregation" => SnapshotRegistration(type, evidence, "Live"),
            "Add" or "AddProjectionWithServices" => AddedRegistration(type, evidence, invocation, semanticModel),
            _ => null
        };
    }

    static List<ProjectionRegistration> RegistrationsIn(
        SyntaxTree tree,
        DotNetProjectCompilation project,
        AdapterIdentity adapter,
        MartenStoreIndex stores)
    {
        var semanticModel = project.Compilation.GetSemanticModel(tree);
        return
        [
            .. tree.GetRoot().DescendantNodes().OfType<InvocationExpressionSyntax>()
                .Select(invocation => (Invocation: invocation, Registration: RegistrationFrom(invocation, semanticModel, project, adapter)))
                .Where(_ => _.Registration is not null)
                .Select(_ => _.Registration! with { Ownership = stores.ForOptions(project, _.Invocation) })
        ];
    }

    static bool IsDerivedEventProjection(INamedTypeSymbol projection) =>
        DotNetSubjectIds.MetadataName(projection) != WellKnownTypes.MartenEventProjection &&
        DotNetSymbols.IsOrInheritsFrom(projection, WellKnownTypes.MartenEventProjection);

    static (IMethodSymbol Method, INamedTypeSymbol Type)? MartenRegistrationCall(
        InvocationExpressionSyntax invocation,
        SemanticModel semanticModel)
    {
        if (semanticModel.GetSymbolInfo(invocation).Symbol is not IMethodSymbol method ||
            MartenProjectionKindDiscovery.IsComposite(method.ContainingType))
        {
            return null;
        }

        if (!IsMarten(method, invocation, semanticModel) ||
            ProjectionTypeFrom(method, invocation, semanticModel) is not { } type)
        {
            return null;
        }

        return (method, type);
    }

    static ProjectionRegistration SnapshotRegistration(
        INamedTypeSymbol type,
        Evidence evidence,
        string? lifecycle) => new(type, null, ProjectionKind.Snapshot, evidence)
        {
            Lifecycle = lifecycle
        };

    static ProjectionRegistration? AddedRegistration(
        INamedTypeSymbol type,
        Evidence evidence,
        InvocationExpressionSyntax invocation,
        SemanticModel semanticModel) => (ShapeOf(type) ?? CustomShapeOf(type)) is { } shape
            ? shape with { Evidence = evidence, Lifecycle = LifecycleFrom(invocation, semanticModel) }
            : null;

    static void AddConventionalRegistrations(
        List<ProjectionRegistration> registrations,
        DotNetProjectCompilation project,
        AdapterIdentity adapter,
        MartenStoreIndex stores)
    {
        var catalog = new DotNetArtifactCatalog(project.Compilation);
        foreach (var projection in catalog.Types.Select(ShapeOf).Where(_ => _ is not null).Cast<ProjectionRegistration>())
        {
            if (IsAlreadyRegistered(registrations, projection, project, stores) || !IsAuthored(projection.Projection!, project))
            {
                continue;
            }

            registrations.Add(projection with
            {
                Ownership = stores.HasExplicitScopes ? BackendOwnership.Unknown : BackendOwnership.Legacy,
                Evidence = CritterStackSource.EvidenceFor(
                    projection.Projection!,
                    adapter,
                    project,
                    EvidenceStrength.Conventional,
                    "The type derives from a Marten projection base")
            });
        }
    }

    static bool IsAlreadyRegistered(
        List<ProjectionRegistration> registrations,
        ProjectionRegistration projection,
        DotNetProjectCompilation project,
        MartenStoreIndex stores) =>
        registrations.Exists(_ => SymbolEqualityComparer.Default.Equals(_.Projection, projection.Projection)) ||
        stores.IsConfiguredProjection(project, projection.Projection!);

    static bool IsAuthored(INamedTypeSymbol type, DotNetProjectCompilation project) =>
        type.DeclaringSyntaxReferences.Any(reference => project.AuthoredSyntaxTrees.Contains(reference.SyntaxTree));

    static ProjectionRegistration? CustomShapeOf(INamedTypeSymbol projection) =>
        !MartenProjectionKindDiscovery.IsOpaqueProjection(projection) &&
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
            if (LifecycleName(argument.Expression, semanticModel) is { } name)
            {
                return name;
            }
        }

        return null;
    }

    static string? LifecycleName(ExpressionSyntax expression, SemanticModel semanticModel)
    {
        if (semanticModel.GetSymbolInfo(expression).Symbol is IFieldSymbol field &&
            _projectionLifecycleTypes.Contains(DotNetSubjectIds.MetadataName(field.ContainingType)))
        {
            return field.Name;
        }

        if (semanticModel.GetTypeInfo(expression).ConvertedType is not INamedTypeSymbol enumType ||
            !_projectionLifecycleTypes.Contains(DotNetSubjectIds.MetadataName(enumType)))
        {
            return null;
        }

        if (semanticModel.GetConstantValue(expression) is not { HasValue: true, Value: not null } constant)
        {
            return null;
        }

        return enumType.GetMembers()
            .OfType<IFieldSymbol>()
            .FirstOrDefault(_ => _.HasConstantValue && Equals(_.ConstantValue, constant.Value))?.Name;
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
