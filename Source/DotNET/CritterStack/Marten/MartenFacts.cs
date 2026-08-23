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
        var registrations = MartenProjectionDiscovery.Discover(project, adapter);
        diagnostics.AddRange(MartenConfigurationDiscovery.Discover(project, registrations));
        diagnostics.AddRange(MartenEventSchemaConfigurationDiscovery.Discover(project));
        diagnostics.AddRange(MartenTenancyConfigurationDiscovery.Discover(project));

        foreach (var registration in registrations)
        {
            if (string.Equals(registration.Lifecycle, "Async", StringComparison.Ordinal) ||
                string.Equals(registration.Lifecycle, "Live", StringComparison.Ordinal))
            {
                diagnostics.Add(ProjectionLifecycleDiagnostic(project, registration));
            }

            var multiStreamConfiguration = MartenMultiStreamConfiguration.Empty;
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
                    multiStreamConfiguration = MartenMultiStreamConfigurationDiscovery.Discover(
                        project,
                        adapter,
                        registration.Projection!);
                    diagnostics.AddRange(multiStreamConfiguration.Diagnostics);
                    break;
                case ProjectionKind.Custom:
                    AddCustomProjectionFacts(project, registration, facts);
                    diagnostics.Add(CustomProjectionDiagnostic(project, registration));
                    continue;
            }

            AddAggregateProjectionFacts(project, options, adapter, registration, multiStreamConfiguration, facts);
        }

        return new(facts, diagnostics, documents);
    }

    static void AddCustomProjectionFacts(
        DotNetProjectCompilation project,
        ProjectionRegistration registration,
        List<GenerationFact> facts)
    {
        var projection = registration.Projection!;
        var subject = project.SubjectForType(projection);
        facts.Add(Artifact(
            $"marten:custom-projection:{subject.Value}",
            subject,
            ArtifactKind.Projection,
            projection.Name,
            SourceFileOf(projection, project),
            [],
            registration.Evidence));
    }

    static void AddAggregateProjectionFacts(
        DotNetProjectCompilation project,
        DotNetAdapterOptions options,
        AdapterIdentity adapter,
        ProjectionRegistration registration,
        MartenMultiStreamConfiguration multiStreamConfiguration,
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
            AddEventFacts(project, options, modelSubject, eventType, evidence, placementEvidence, facts);
            var eventSubject = project.SubjectForType(eventType);
            facts.Add(Relationship(
                $"marten:consumes:{projectionSubject.Value}:{eventSubject.Value}",
                projectionSubject,
                RelationshipKind.Consumes,
                eventSubject,
                evidence));
        }

        AddMultiStreamConfigurationFacts(
            project,
            options,
            modelSubject,
            projectionSubject,
            multiStreamConfiguration,
            facts);
    }

    static void AddMultiStreamConfigurationFacts(
        DotNetProjectCompilation project,
        DotNetAdapterOptions options,
        SubjectId modelSubject,
        SubjectId projectionSubject,
        MartenMultiStreamConfiguration configuration,
        List<GenerationFact> facts)
    {
        foreach (var identity in configuration.Identities)
        {
            var eventSubject = project.SubjectForType(identity.EventType);
            AddEventFacts(
                project,
                options,
                modelSubject,
                identity.EventType,
                identity.Evidence,
                identity.Evidence with
                {
                    Strength = EvidenceStrength.Heuristic,
                    Explanation = "The configured multi-stream event and project provide the default Screenplay placement"
                },
                facts,
                EvidenceId(identity.Evidence));
            var discriminator = identity.IsOneToMany
                ? $"marten:identities:{identity.TargetMember}"
                : $"marten:identity:{identity.TargetMember}";
            facts.Add(Relationship(
                $"marten:grouping:{projectionSubject.Value}:{eventSubject.Value}:{discriminator}:{EvidenceId(identity.Evidence)}",
                projectionSubject,
                RelationshipKind.Consumes,
                eventSubject,
                identity.Evidence,
                targetMember: identity.TargetMember,
                discriminator: discriminator,
                isCollection: identity.IsOneToMany));
        }

        foreach (var fanOut in configuration.FanOuts)
        {
            var parentSubject = project.SubjectForType(fanOut.ParentEventType);
            var childSubject = project.SubjectForType(fanOut.ChildEventType);
            var placementEvidence = fanOut.Evidence with
            {
                Strength = EvidenceStrength.Heuristic,
                Explanation = "The configured fan-out event and project provide the default Screenplay placement"
            };
            AddEventFacts(project, options, modelSubject, fanOut.ParentEventType, fanOut.Evidence, placementEvidence, facts, EvidenceId(fanOut.Evidence));
            AddEventFacts(project, options, modelSubject, fanOut.ChildEventType, fanOut.Evidence, placementEvidence, facts, EvidenceId(fanOut.Evidence));
            facts.Add(Relationship(
                $"marten:fan-out-consumes:{projectionSubject.Value}:{parentSubject.Value}:{childSubject.Value}:{EvidenceId(fanOut.Evidence)}",
                projectionSubject,
                RelationshipKind.Consumes,
                parentSubject,
                fanOut.Evidence,
                discriminator: $"marten:fan-out-source:{childSubject.Value}"));
            facts.Add(Relationship(
                $"marten:fan-out-child:{projectionSubject.Value}:{parentSubject.Value}:{childSubject.Value}:{EvidenceId(fanOut.Evidence)}",
                projectionSubject,
                RelationshipKind.Consumes,
                childSubject,
                fanOut.Evidence,
                sourceMember: fanOut.SourceMember,
                discriminator: $"marten:fan-out-child:{parentSubject.Value}:{fanOut.Mode}:{fanOut.SourceMember}",
                isCollection: true));
        }
    }

    static void AddEventFacts(
        DotNetProjectCompilation project,
        DotNetAdapterOptions options,
        SubjectId modelSubject,
        INamedTypeSymbol eventType,
        Evidence evidence,
        Evidence placementEvidence,
        List<GenerationFact> facts,
        string? idSuffix = null)
    {
        var eventSubject = project.SubjectForType(eventType);
        var suffix = idSuffix is null ? string.Empty : $":{idSuffix}";
        facts.Add(Artifact(
            $"marten:event:{eventSubject.Value}{suffix}",
            eventSubject,
            ArtifactKind.Event,
            eventType.Name,
            SourceFileOf(eventType, project),
            DotNetTypeShapes.PropertiesOf(eventType),
            evidence));
        facts.Add(Placement(
            $"marten:placement:event:{eventSubject.Value}:{modelSubject.Value}{suffix}",
            new ArtifactKey { Subject = eventSubject, Kind = ArtifactKind.Event },
            EventPlacementFor(project, options, eventType.Name),
            placementEvidence));
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
        Evidence evidence,
        string? sourceMember = null,
        string? targetMember = null,
        string? discriminator = null,
        bool isCollection = false) => new()
    {
        Id = new FactId { Value = id },
        Subject = source,
        Definition = new RelationshipDefinition
        {
            Key = new RelationshipKey
            {
                Kind = kind,
                Source = source,
                Target = target,
                Discriminator = discriminator
            },
            SourceMember = sourceMember,
            TargetMember = targetMember,
            IsCollection = isCollection
        },
        Evidence = evidence
    };

    static string EvidenceId(Evidence evidence) => evidence.Source is null
        ? "unknown"
        : $"{evidence.Source.StartLine}-{evidence.Source.StartColumn}";

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
        Message = $"Multi-stream projection '{registration.Projection!.Name}' is represented as an event reducer; exact authored grouping and fan-out declarations are retained as neutral evidence, but those semantics are not expressible in the current Screenplay language",
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

    static GenerationDiagnostic CustomProjectionDiagnostic(
        DotNetProjectCompilation project,
        ProjectionRegistration registration) => new()
    {
        Code = MartenDiagnosticCodes.CustomProcessingOmitted,
        Severity = GenerationDiagnosticSeverity.Warning,
        Message = $"Custom Marten projection '{registration.Projection!.Name}' is preserved as a neutral projection artifact, but its arbitrary processing consequences were not inferred",
        Source = registration.Evidence.Source,
        Subject = project.SubjectForType(registration.Projection)
    };
}
