// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Cratis.Screenplay.Generation;
using Cratis.Screenplay.Generation.DotNet;
using Microsoft.CodeAnalysis;

namespace Cratis.CritterStack.Screenplay.Marten;

sealed record MartenDiscoveryResult(
    IReadOnlyList<GenerationFact> Facts,
    IReadOnlyList<GenerationDiagnostic> Diagnostics,
    IReadOnlyList<MartenDocumentUsage> Documents);

static class MartenFacts
{
    static readonly string[] _eventWrapperTypes = ["JasperFx.Events.IEvent`1", "Marten.Events.IEvent`1"];
    static readonly string[] _evolutionMethodNames = ["Apply", "Create", "ShouldDelete"];
    static readonly HashSet<string> _infrastructureParameterTypes =
    [
        "System.Threading.CancellationToken",
        "JasperFx.Events.IEvent",
        "Marten.IQuerySession",
        "Marten.IDocumentOperations",
        "Marten.IDocumentSession"
    ];

    public static MartenDiscoveryResult Discover(
        DotNetProjectCompilation project,
        DotNetAdapterOptions options,
        AdapterIdentity adapter)
    {
        if (project.Compilation.GetTypeByMetadataName(WellKnownTypes.MartenStoreOptions) is null &&
            project.Compilation.GetTypeByMetadataName(WellKnownTypes.MartenDocumentStore) is null)
        {
            return new([], [], []);
        }

        var facts = new List<GenerationFact>();
        var diagnostics = new List<GenerationDiagnostic>();
        var documents = new List<MartenDocumentUsage>();

        foreach (var registration in MartenProjectionDiscovery.Discover(project, adapter))
        {
            if (string.Equals(registration.Lifecycle, "Async", StringComparison.Ordinal) ||
                string.Equals(registration.Lifecycle, "Live", StringComparison.Ordinal))
            {
                diagnostics.Add(ProjectionLifecycleDiagnostic(project, registration));
            }

            switch (registration.Kind)
            {
                case ProjectionKind.Event:
                    var eventProjection = MartenEventProjectionFacts.Discover(project, adapter, registration);
                    facts.AddRange(eventProjection.Facts);
                    diagnostics.AddRange(eventProjection.Diagnostics);
                    documents.AddRange(eventProjection.Documents);
                    continue;
                case ProjectionKind.MultiStream:
                    diagnostics.Add(MultiStreamDiagnostic(project, registration));
                    break;
            }

            AddAggregateProjectionFacts(project, options, adapter, registration, facts);
        }

        return new(facts, diagnostics, documents);
    }

    static void AddAggregateProjectionFacts(
        DotNetProjectCompilation project,
        DotNetAdapterOptions options,
        AdapterIdentity adapter,
        ProjectionRegistration registration,
        List<GenerationFact> facts)
    {
        var model = registration.Model;
        var modelSubject = project.SubjectForType(model);
        var projection = registration.Projection ?? model;
        var projectionSubject = new SubjectId
        {
            Value = $"{project.SubjectForType(projection).Value}#reducer"
        };
        var file = SourceFileOf(model, project) ?? SourceFileOf(projection, project);
        var placement = PlacementFor(project, options, model.Name);
        var placementEvidence = registration.Evidence with
        {
            Strength = EvidenceStrength.Heuristic,
            Explanation = "The project and aggregate name provide the default Screenplay placement"
        };

        facts.Add(Artifact(
            $"marten:read-model:{modelSubject.Value}",
            modelSubject,
            ArtifactKind.ReadModel,
            model.Name,
            file,
            DotNetTypeShapes.PropertiesOf(model),
            registration.Evidence));
        facts.Add(Artifact(
            $"marten:aggregate:{modelSubject.Value}",
            modelSubject,
            ArtifactKind.Aggregate,
            model.Name,
            file,
            DotNetTypeShapes.PropertiesOf(model),
            registration.Evidence));
        facts.Add(Placement(
            $"marten:placement:read-model:{modelSubject.Value}",
            new ArtifactKey { Subject = modelSubject, Kind = ArtifactKind.ReadModel },
            placement,
            placementEvidence));

        var reducerFile = SourceFileOf(projection, project) ?? file;
        facts.Add(Artifact(
            $"marten:reducer:{projectionSubject.Value}",
            projectionSubject,
            ArtifactKind.Reducer,
            registration.Projection?.Name ?? $"{model.Name}Snapshot",
            reducerFile,
            [],
            registration.Evidence));
        facts.Add(Placement(
            $"marten:placement:reducer:{projectionSubject.Value}",
            new ArtifactKey { Subject = projectionSubject, Kind = ArtifactKind.Reducer },
            placement,
            placementEvidence));
        facts.Add(Relationship(
            $"marten:builds:{projectionSubject.Value}:{modelSubject.Value}",
            projectionSubject,
            RelationshipKind.Builds,
            modelSubject,
            registration.Evidence));

        foreach (var (eventType, evidence) in EvolutionEvents(projection, model, project, adapter))
        {
            var eventSubject = project.SubjectForType(eventType);
            var eventFile = SourceFileOf(eventType, project);
            facts.Add(Artifact(
                $"marten:event:{eventSubject.Value}",
                eventSubject,
                ArtifactKind.Event,
                eventType.Name,
                eventFile,
                DotNetTypeShapes.PropertiesOf(eventType),
                evidence));
            facts.Add(Placement(
                $"marten:placement:event:{eventSubject.Value}:{modelSubject.Value}",
                new ArtifactKey { Subject = eventSubject, Kind = ArtifactKind.Event },
                EventPlacementFor(project, options, eventType.Name),
                placementEvidence));
            facts.Add(Relationship(
                $"marten:consumes:{projectionSubject.Value}:{eventSubject.Value}",
                projectionSubject,
                RelationshipKind.Consumes,
                eventSubject,
                evidence));
        }
    }

    static IEnumerable<(INamedTypeSymbol EventType, Evidence Evidence)> EvolutionEvents(
        INamedTypeSymbol projection,
        INamedTypeSymbol model,
        DotNetProjectCompilation project,
        AdapterIdentity adapter)
    {
        foreach (var method in projection.GetMembers().OfType<IMethodSymbol>()
                     .Where(_ => _evolutionMethodNames.Contains(_.Name, StringComparer.Ordinal)))
        {
            var eventType = EventTypeFrom(method, model);
            if (eventType is null)
            {
                continue;
            }

            yield return (
                eventType,
                DotNetSource.EvidenceFor(
                    method,
                    adapter,
                    EvidenceStrength.Conventional,
                    project.SourceRoot,
                    $"Marten treats {method.Name} as an event evolution method"));
        }
    }

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

    static ArtifactFact Artifact(
        string id,
        SubjectId subject,
        ArtifactKind kind,
        string name,
        string? file,
        IReadOnlyList<PropertyDefinition> properties,
        Evidence evidence) => new()
    {
        Id = new FactId { Value = id },
        Subject = subject,
        Definition = new ArtifactDefinition
        {
            Key = new ArtifactKey { Subject = subject, Kind = kind },
            Name = name,
            File = file,
            Properties = properties
        },
        Evidence = evidence
    };

    static ArtifactPlacementFact Placement(
        string id,
        ArtifactKey artifact,
        ArtifactPlacement placement,
        Evidence evidence) => new()
    {
        Id = new FactId { Value = id },
        Subject = artifact.Subject,
        Artifact = artifact,
        Placement = placement,
        Evidence = evidence
    };

    static RelationshipFact Relationship(
        string id,
        SubjectId source,
        RelationshipKind kind,
        SubjectId target,
        Evidence evidence) => new()
    {
        Id = new FactId { Value = id },
        Subject = source,
        Definition = new RelationshipDefinition
        {
            Key = new RelationshipKey
            {
                Kind = kind,
                Source = source,
                Target = target
            }
        },
        Evidence = evidence
    };

    static ArtifactPlacement PlacementFor(
        DotNetProjectCompilation project,
        DotNetAdapterOptions options,
        string modelName) => new()
    {
        Module = ScreenplayNames.Declaration(options.Module ?? project.Name),
        Features = [modelName],
        Slice = modelName,
        SliceKind = GenerationSliceKind.StateView
    };

    static ArtifactPlacement EventPlacementFor(
        DotNetProjectCompilation project,
        DotNetAdapterOptions options,
        string eventName) => new()
    {
        Module = ScreenplayNames.Declaration(options.Module ?? project.Name),
        Features = ["Events"],
        Slice = eventName,
        SliceKind = GenerationSliceKind.StateView
    };

    static string? SourceFileOf(ISymbol symbol, DotNetProjectCompilation project) =>
        DotNetSource.EvidenceFor(symbol, new AdapterIdentity { Id = "source", Version = "1" }, EvidenceStrength.Exact, project.SourceRoot).Source?.Path;

    static GenerationDiagnostic MultiStreamDiagnostic(
        DotNetProjectCompilation project,
        ProjectionRegistration registration) => new()
    {
        Code = MartenDiagnosticCodes.MultiStreamGroupingOmitted,
        Severity = GenerationDiagnosticSeverity.Warning,
        Message = $"Multi-stream projection '{registration.Projection!.Name}' is represented as an event reducer, but its grouping and fan-out semantics are not expressible in the current Screenplay language",
        Source = registration.Evidence.Source,
        Subject = project.SubjectForType(registration.Projection)
    };

    static GenerationDiagnostic ProjectionLifecycleDiagnostic(
        DotNetProjectCompilation project,
        ProjectionRegistration registration) => new()
    {
        Code = MartenDiagnosticCodes.ProjectionLifecycleOmitted,
        Severity = GenerationDiagnosticSeverity.Warning,
        Message = $"Projection '{(registration.Projection ?? registration.Model).Name}' uses the {registration.Lifecycle} lifecycle, which is not expressible in the current Screenplay language",
        Source = registration.Evidence.Source,
        Subject = project.SubjectForType(registration.Projection ?? registration.Model)
    };
}
