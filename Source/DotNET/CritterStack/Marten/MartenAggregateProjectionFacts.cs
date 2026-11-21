// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Cratis.Screenplay.Generation;
using Cratis.Screenplay.Generation.DotNet;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Cratis.CritterStack.Screenplay.Marten;

/// <summary>
/// Accumulates everything the Marten projection facts produce while one project is discovered.
/// </summary>
/// <param name="project">The project being discovered.</param>
/// <param name="options">The adapter options.</param>
/// <param name="adapter">The adapter identity.</param>
/// <param name="subjects">The subject resolver.</param>
sealed class MartenCollectedFacts(
    DotNetProjectCompilation project,
    DotNetAdapterOptions options,
    AdapterIdentity adapter,
    CritterStackSubjectResolver subjects)
{
    public DotNetProjectCompilation Project { get; } = project;

    public DotNetAdapterOptions Options { get; } = options;

    public AdapterIdentity Adapter { get; } = adapter;

    public CritterStackSubjectResolver Subjects { get; } = subjects;

    public List<GenerationFact> Facts { get; } = [];

    public List<GenerationDiagnostic> Diagnostics { get; } = [];

    public List<MartenDocumentUsage> Documents { get; } = [];

    public List<CritterStackPlacementIntent> Placements { get; } = [];
}

/// <summary>
/// Describes one event the aggregate or multi-stream projection consumes.
/// </summary>
/// <param name="EventType">The event type.</param>
/// <param name="Evidence">The evidence for the event.</param>
/// <param name="PlacementEvidence">The evidence for the event's default placement.</param>
/// <param name="IdSuffix">An optional suffix that keeps repeated registrations of one event distinct.</param>
sealed record MartenEventSite(
    INamedTypeSymbol EventType,
    Evidence Evidence,
    Evidence PlacementEvidence,
    string? IdSuffix = null);

static class MartenAggregateProjectionFacts
{
    const string AggregationBase = "JasperFx.Events.Aggregation.JasperFxAggregationProjectionBase`4";
    const string AggregationSteps = "JasperFx.Events.Daemon.IAggregationSteps`2";
    static readonly string[] _eventWrapperTypes = ["JasperFx.Events.IEvent`1", "Marten.Events.IEvent`1"];
    static readonly string[] _evolutionMethodNames = ["Apply", "Create", "ShouldDelete"];
    static readonly string[] _dispatchMethodNames = ["Evolve", "EvolveAsync", "DetermineAction", "DetermineActionAsync"];
    static readonly HashSet<string> _infrastructureParameterTypes =
    [
        "System.Threading.CancellationToken",
        "JasperFx.Events.IEvent",
        "Marten.IQuerySession",
        "Marten.IDocumentOperations",
        "Marten.IDocumentSession"
    ];

    public static void Add(
        MartenCollectedFacts collected,
        ProjectionRegistration registration,
        MartenMultiStreamConfiguration multiStreamConfiguration)
    {
        var scope = ScopeFor(collected, registration);
        AddModelFacts(collected, registration, scope);
        AddReducerFacts(collected, registration, scope);

        foreach (var (eventType, evidence) in EvolutionEvents(scope.Projection, registration.Model, collected))
        {
            AddEventFacts(collected, scope.ModelSubject, new(eventType, evidence, scope.PlacementEvidence));
            var eventSubject = collected.Subjects.SubjectForType(collected.Project, eventType);
            collected.Facts.Add(new MartenRelationshipFact
            {
                Id = $"marten:consumes:{scope.ProjectionSubject.Value}:{eventSubject.Value}",
                Source = scope.ProjectionSubject,
                Kind = RelationshipKind.Consumes,
                Target = eventSubject,
                Evidence = evidence
            }.ToFact());
        }

        MartenMultiStreamFacts.Add(collected, scope.ModelSubject, scope.ProjectionSubject, multiStreamConfiguration);
    }

    public static void AddEventFacts(MartenCollectedFacts collected, SubjectId modelSubject, MartenEventSite site)
    {
        var (project, subjects) = (collected.Project, collected.Subjects);
        var eventSubject = subjects.SubjectForType(project, site.EventType);
        var suffix = site.IdSuffix is null ? string.Empty : $":{site.IdSuffix}";
        collected.Facts.Add(new MartenArtifactFact
        {
            Id = $"marten:event:{eventSubject.Value}{suffix}",
            Subject = eventSubject,
            Kind = ArtifactKind.Event,
            Name = site.EventType.Name,
            File = MartenSourceFiles.FileOf(site.EventType, project),
            Properties = DotNetTypeShapes.PropertiesOf(site.EventType),
            Evidence = site.Evidence
        }.ToFact());
        collected.Placements.Add(new(
            $"marten:placement:event:{eventSubject.Value}:{modelSubject.Value}{suffix}",
            new ArtifactKey { Subject = eventSubject, Kind = ArtifactKind.Event },
            null,
            CritterStackSourcePlacement.CompatibilityPlacement(
                project,
                collected.Options,
                "Events",
                site.EventType.Name,
                GenerationSliceKind.StateView),
            site.PlacementEvidence));
    }

    public static string EvidenceId(Evidence evidence) => evidence.Source is null
        ? "unknown"
        : $"{evidence.Source.StartLine}-{evidence.Source.StartColumn}";

    static AggregateScope ScopeFor(MartenCollectedFacts collected, ProjectionRegistration registration)
    {
        var (project, subjects) = (collected.Project, collected.Subjects);
        var model = registration.Model;
        var modelSubject = subjects.SubjectForType(project, model);
        var projection = registration.Projection ?? model;
        var projectionSubject = new SubjectId
        {
            Value = $"{subjects.SubjectForType(project, projection).Value}#reducer"
        };
        var reducerSourceOwner = registration.Projection is null
            ? modelSubject
            : subjects.SubjectForType(project, registration.Projection);
        var compatibilityPlacement = CritterStackSourcePlacement.CompatibilityPlacement(
            project,
            collected.Options,
            model.Name,
            model.Name,
            GenerationSliceKind.StateView);
        var placementEvidence = registration.Evidence with
        {
            Strength = EvidenceStrength.Heuristic,
            Explanation = "The project and aggregate name provide the default Screenplay placement"
        };

        return new(
            projection,
            modelSubject,
            projectionSubject,
            reducerSourceOwner,
            MartenSourceFiles.FileOf(model, project) ?? MartenSourceFiles.FileOf(projection, project),
            compatibilityPlacement,
            placementEvidence);
    }

    static void AddModelFacts(MartenCollectedFacts collected, ProjectionRegistration registration, AggregateScope scope)
    {
        var model = registration.Model;
        foreach (var (idPrefix, kind) in new[] { ("read-model", ArtifactKind.ReadModel), ("aggregate", ArtifactKind.Aggregate) })
        {
            collected.Facts.Add(new MartenArtifactFact
            {
                Id = $"marten:{idPrefix}:{scope.ModelSubject.Value}",
                Subject = scope.ModelSubject,
                Kind = kind,
                Name = model.Name,
                File = scope.File,
                Properties = DotNetTypeShapes.PropertiesOf(model),
                Evidence = registration.Evidence
            }.ToFact());
        }

        collected.Placements.Add(new(
            $"marten:placement:read-model:{scope.ModelSubject.Value}",
            new ArtifactKey { Subject = scope.ModelSubject, Kind = ArtifactKind.ReadModel },
            null,
            scope.CompatibilityPlacement,
            scope.PlacementEvidence));
    }

    static void AddReducerFacts(MartenCollectedFacts collected, ProjectionRegistration registration, AggregateScope scope)
    {
        collected.Facts.Add(new MartenArtifactFact
        {
            Id = $"marten:reducer:{scope.ProjectionSubject.Value}",
            Subject = scope.ProjectionSubject,
            Kind = ArtifactKind.Reducer,
            Name = registration.Projection?.Name ?? $"{registration.Model.Name}Snapshot",
            File = MartenSourceFiles.FileOf(scope.Projection, collected.Project) ?? scope.File,
            Evidence = registration.Evidence
        }.ToFact());
        collected.Placements.Add(new(
            $"marten:placement:reducer:{scope.ProjectionSubject.Value}",
            new ArtifactKey { Subject = scope.ProjectionSubject, Kind = ArtifactKind.Reducer },
            scope.ReducerSourceOwner,
            scope.CompatibilityPlacement,
            scope.PlacementEvidence));
        collected.Facts.Add(new MartenRelationshipFact
        {
            Id = $"marten:builds:{scope.ProjectionSubject.Value}:{scope.ModelSubject.Value}",
            Source = scope.ProjectionSubject,
            Kind = RelationshipKind.Builds,
            Target = scope.ModelSubject,
            Evidence = registration.Evidence
        }.ToFact());
    }

    static IEnumerable<(INamedTypeSymbol EventType, Evidence Evidence)> EvolutionEvents(
        INamedTypeSymbol projection,
        INamedTypeSymbol model,
        MartenCollectedFacts collected)
    {
        foreach (var method in projection.GetMembers().OfType<IMethodSymbol>())
        {
            var conventional = _evolutionMethodNames.Contains(method.Name, StringComparer.Ordinal);
            if (!conventional && !IsDispatchOverride(method))
            {
                continue;
            }

            var evidence = CritterStackSource.EvidenceFor(method, collected.Adapter, collected.Project, EvidenceStrength.Conventional, $"Marten treats {method.Name} as an event evolution method");
            if (!conventional && evidence.Source is null)
            {
                continue;
            }

            var eventType = conventional ? EventTypeFrom(method, model) : DispatchEventTypeFrom(method);
            if (eventType is not null)
            {
                yield return (eventType, evidence);
            }
            else if (!conventional)
            {
                collected.Diagnostics.Add(new()
                {
                    Code = MartenDiagnosticCodes.CustomProcessingOmitted,
                    Severity = GenerationDiagnosticSeverity.Information,
                    Outcome = GenerationDiagnosticOutcome.Unsupported,
                    Message = $"Marten evolution override '{method.Name}' processes untyped events; no event type or processing consequences were inferred",
                    Source = evidence.Source,
                    Subject = collected.Subjects.SubjectForType(collected.Project, projection)
                });
            }
        }

        foreach (var registration in DeleteEvents(projection, collected))
        {
            yield return registration;
        }
    }

    static bool IsDispatchOverride(IMethodSymbol method)
    {
        if (!method.IsOverride || !_dispatchMethodNames.Contains(method.Name, StringComparer.Ordinal))
        {
            return false;
        }

        for (var current = method.OverriddenMethod; current is not null; current = current.OverriddenMethod)
        {
            if (DotNetSubjectIds.MetadataName(current.ContainingType.OriginalDefinition) == AggregationBase)
            {
                return method.Parameters.Length == current.Parameters.Length && method.Parameters
                    .Select((parameter, index) => parameter.RefKind == current.Parameters[index].RefKind &&
                        SymbolEqualityComparer.Default.Equals(parameter.Type, current.Parameters[index].Type))
                    .All(_ => _);
            }
        }

        return false;
    }

    static INamedTypeSymbol? DispatchEventTypeFrom(IMethodSymbol method) => method.Parameters
        .Select(_ => _.Type).OfType<INamedTypeSymbol>()
        .Where(_ => _eventWrapperTypes.Contains(DotNetSubjectIds.MetadataName(_.OriginalDefinition), StringComparer.Ordinal))
        .Select(_ => _.TypeArguments[0]).OfType<INamedTypeSymbol>().FirstOrDefault();

    static IEnumerable<(INamedTypeSymbol EventType, Evidence Evidence)> DeleteEvents(
        INamedTypeSymbol projection,
        MartenCollectedFacts collected)
    {
        foreach (var constructor in projection.InstanceConstructors.SelectMany(_ => _.DeclaringSyntaxReferences)
                     .Select(_ => _.GetSyntax()).OfType<ConstructorDeclarationSyntax>()
                     .Where(_ => collected.Project.AuthoredSyntaxTrees.Contains(_.SyntaxTree) && !DotNetGeneratedSource.IsGenerated(_.SyntaxTree)))
        {
            var semanticModel = collected.Project.Compilation.GetSemanticModel(constructor.SyntaxTree);
            foreach (var invocation in constructor.DescendantNodes().OfType<InvocationExpressionSyntax>())
            {
                if (DeleteEventType(invocation, semanticModel) is not { } eventType)
                {
                    continue;
                }

                yield return (eventType, new Evidence
                {
                    Adapter = collected.Adapter,
                    Strength = EvidenceStrength.Configured,
                    Source = CritterStackSource.RangeForProject(invocation.GetLocation(), collected.Project),
                    Explanation = "Marten registers this event type to delete the aggregate"
                });
            }
        }
    }

    static INamedTypeSymbol? DeleteEventType(InvocationExpressionSyntax invocation, SemanticModel semanticModel)
    {
        if (!IsSelfRegistration(invocation, semanticModel) ||
            semanticModel.GetEnclosingSymbol(invocation.SpanStart) is not IMethodSymbol { MethodKind: MethodKind.Constructor } ||
            semanticModel.GetSymbolInfo(invocation).Symbol is not IMethodSymbol { Name: "DeleteEvent", Parameters.Length: 0, TypeArguments.Length: 1 } method ||
            !IsAggregationStep(method))
        {
            return null;
        }

        return method.TypeArguments[0] is INamedTypeSymbol { TypeKind: not TypeKind.Error } eventType ? eventType : null;
    }

    static bool IsAggregationStep(IMethodSymbol method) => DotNetSubjectIds.MetadataName(method.ContainingType.OriginalDefinition) is AggregationBase or AggregationSteps;

    static bool IsSelfRegistration(InvocationExpressionSyntax invocation, SemanticModel semanticModel) => invocation.Expression switch
    {
        GenericNameSyntax => true,
        MemberAccessExpressionSyntax { Expression: ThisExpressionSyntax or BaseExpressionSyntax } => true,
        MemberAccessExpressionSyntax { Expression: InvocationExpressionSyntax receiver } =>
            semanticModel.GetSymbolInfo(receiver).Symbol is IMethodSymbol { Name: "DeleteEvent" or "TransformsEvent", Parameters.Length: 0, TypeArguments.Length: 1 } method &&
            IsAggregationStep(method) && IsSelfRegistration(receiver, semanticModel),
        _ => false
    };

    static INamedTypeSymbol? EventTypeFrom(IMethodSymbol method, INamedTypeSymbol model)
    {
        foreach (var parameter in method.Parameters)
        {
            if (parameter.Type is not INamedTypeSymbol type || SymbolEqualityComparer.Default.Equals(type, model))
            {
                continue;
            }

            if (type.IsGenericType && _eventWrapperTypes.Contains(DotNetSubjectIds.MetadataName(type.OriginalDefinition), StringComparer.Ordinal))
            {
                return type.TypeArguments[0] as INamedTypeSymbol;
            }

            var metadataName = DotNetSubjectIds.MetadataName(type.OriginalDefinition);
            if (IsInfrastructureParameter(metadataName))
            {
                continue;
            }

            return type;
        }

        return null;
    }

    static bool IsInfrastructureParameter(string metadataName) => _infrastructureParameterTypes.Contains(metadataName);

    sealed record AggregateScope(
        INamedTypeSymbol Projection,
        SubjectId ModelSubject,
        SubjectId ProjectionSubject,
        SubjectId ReducerSourceOwner,
        string? File,
        ArtifactPlacement CompatibilityPlacement,
        Evidence PlacementEvidence);
}
