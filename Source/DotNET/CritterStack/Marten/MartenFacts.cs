// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Cratis.Screenplay.Generation;
using Cratis.Screenplay.Generation.DotNet;
using Microsoft.CodeAnalysis;

namespace Cratis.CritterStack.Screenplay.Marten;

sealed record MartenDiscoveryResult(
    IReadOnlyList<GenerationFact> Facts,
    IReadOnlyList<GenerationDiagnostic> Diagnostics,
    IReadOnlyList<MartenDocumentUsage> Documents,
    IReadOnlyList<CritterStackPlacementIntent>? Placements = null);

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
        AdapterIdentity adapter,
        CritterStackSubjectResolver subjects)
    {
        if (project.Compilation.GetTypeByMetadataName(WellKnownTypes.MartenStoreOptions) is null &&
            project.Compilation.GetTypeByMetadataName(WellKnownTypes.MartenDocumentStore) is null)
        {
            return new([], [], []);
        }

        var facts = new List<GenerationFact>();
        var diagnostics = new List<GenerationDiagnostic>();
        var documents = new List<MartenDocumentUsage>();
        var placements = new List<CritterStackPlacementIntent>();
        var registrations = MartenProjectionDiscovery.Discover(project, adapter);
        var configuration = MartenConfigurationDiscovery.Discover(project, adapter, subjects, registrations);
        facts.AddRange(configuration.Facts);
        diagnostics.AddRange(configuration.Diagnostics);
        diagnostics.AddRange(MartenEventSchemaConfigurationDiscovery.Discover(project, subjects));
        diagnostics.AddRange(MartenTenancyConfigurationDiscovery.Discover(project, subjects));
        var sideEffectProjections = new HashSet<INamedTypeSymbol>(SymbolEqualityComparer.Default);

        foreach (var registration in registrations)
        {
            var sideEffectProjection = registration.Projection ?? registration.Model;
            if (registration.Kind != ProjectionKind.Custom && sideEffectProjections.Add(sideEffectProjection))
            {
                var sideEffects = MartenProjectionSideEffects.Discover(
                    project,
                    adapter,
                    subjects,
                    registration,
                    configuration.SideEffectsEnabled);
                facts.AddRange(sideEffects.Facts);
                diagnostics.AddRange(sideEffects.Diagnostics);
            }

            if (string.Equals(registration.Lifecycle, "Async", StringComparison.Ordinal) ||
                string.Equals(registration.Lifecycle, "Live", StringComparison.Ordinal))
            {
                diagnostics.Add(ProjectionLifecycleDiagnostic(project, subjects, registration));
            }

            var multiStreamConfiguration = MartenMultiStreamConfiguration.Empty;
            switch (registration.Kind)
            {
                case ProjectionKind.Event:
                    var eventProjection = MartenEventProjectionFacts.Discover(project, adapter, subjects, registration);
                    facts.AddRange(eventProjection.Facts);
                    diagnostics.AddRange(eventProjection.Diagnostics);
                    documents.AddRange(eventProjection.Documents);
                    continue;
                case ProjectionKind.MultiStream:
                    diagnostics.Add(MultiStreamDiagnostic(project, subjects, registration));
                    multiStreamConfiguration = MartenMultiStreamConfigurationDiscovery.Discover(
                        project,
                        adapter,
                        subjects,
                        registration.Projection!);
                    diagnostics.AddRange(multiStreamConfiguration.Diagnostics);
                    break;
                case ProjectionKind.Custom:
                    AddCustomProjectionFacts(project, subjects, registration, facts);
                    diagnostics.Add(CustomProjectionDiagnostic(project, subjects, registration));
                    continue;
            }

            AddAggregateProjectionFacts(project, options, adapter, subjects, registration, multiStreamConfiguration, facts, placements);
        }

        return new(facts, diagnostics, documents, placements);
    }

    static void AddCustomProjectionFacts(
        DotNetProjectCompilation project,
        CritterStackSubjectResolver subjects,
        ProjectionRegistration registration,
        List<GenerationFact> facts)
    {
        var projection = registration.Projection!;
        var subject = subjects.SubjectForType(project, projection);
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
        CritterStackSubjectResolver subjects,
        ProjectionRegistration registration,
        MartenMultiStreamConfiguration multiStreamConfiguration,
        List<GenerationFact> facts,
        List<CritterStackPlacementIntent> placements)
    {
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
        var file = SourceFileOf(model, project) ?? SourceFileOf(projection, project);
        var compatibilityPlacement = CritterStackSourcePlacement.CompatibilityPlacement(
            project,
            options,
            model.Name,
            model.Name,
            GenerationSliceKind.StateView);
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
        placements.Add(new(
            $"marten:placement:read-model:{modelSubject.Value}",
            new ArtifactKey { Subject = modelSubject, Kind = ArtifactKind.ReadModel },
            null,
            compatibilityPlacement,
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
        placements.Add(new(
            $"marten:placement:reducer:{projectionSubject.Value}",
            new ArtifactKey { Subject = projectionSubject, Kind = ArtifactKind.Reducer },
            reducerSourceOwner,
            compatibilityPlacement,
            placementEvidence));
        facts.Add(Relationship(
            $"marten:builds:{projectionSubject.Value}:{modelSubject.Value}",
            projectionSubject,
            RelationshipKind.Builds,
            modelSubject,
            registration.Evidence));

        foreach (var (eventType, evidence) in EvolutionEvents(projection, model, project, adapter))
        {
            AddEventFacts(project, options, subjects, modelSubject, eventType, evidence, placementEvidence, facts, placements);
            var eventSubject = subjects.SubjectForType(project, eventType);
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
            subjects,
            modelSubject,
            projectionSubject,
            multiStreamConfiguration,
            facts,
            placements);
    }

    static void AddMultiStreamConfigurationFacts(
        DotNetProjectCompilation project,
        DotNetAdapterOptions options,
        CritterStackSubjectResolver subjects,
        SubjectId modelSubject,
        SubjectId projectionSubject,
        MartenMultiStreamConfiguration configuration,
        List<GenerationFact> facts,
        List<CritterStackPlacementIntent> placements)
    {
        foreach (var identity in configuration.Identities)
        {
            var eventSubject = subjects.SubjectForType(project, identity.EventType);
            AddEventFacts(
                project,
                options,
                subjects,
                modelSubject,
                identity.EventType,
                identity.Evidence,
                identity.Evidence with
                {
                    Strength = EvidenceStrength.Heuristic,
                    Explanation = "The configured multi-stream event and project provide the default Screenplay placement"
                },
                facts,
                placements,
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
            var parentSubject = subjects.SubjectForType(project, fanOut.ParentEventType);
            var childSubject = subjects.SubjectForType(project, fanOut.ChildEventType);
            var placementEvidence = fanOut.Evidence with
            {
                Strength = EvidenceStrength.Heuristic,
                Explanation = "The configured fan-out event and project provide the default Screenplay placement"
            };
            AddEventFacts(project, options, subjects, modelSubject, fanOut.ParentEventType, fanOut.Evidence, placementEvidence, facts, placements, EvidenceId(fanOut.Evidence));
            AddEventFacts(project, options, subjects, modelSubject, fanOut.ChildEventType, fanOut.Evidence, placementEvidence, facts, placements, EvidenceId(fanOut.Evidence));
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
        CritterStackSubjectResolver subjects,
        SubjectId modelSubject,
        INamedTypeSymbol eventType,
        Evidence evidence,
        Evidence placementEvidence,
        List<GenerationFact> facts,
        List<CritterStackPlacementIntent> placements,
        string? idSuffix = null)
    {
        var eventSubject = subjects.SubjectForType(project, eventType);
        var suffix = idSuffix is null ? string.Empty : $":{idSuffix}";
        facts.Add(Artifact(
            $"marten:event:{eventSubject.Value}{suffix}",
            eventSubject,
            ArtifactKind.Event,
            eventType.Name,
            SourceFileOf(eventType, project),
            DotNetTypeShapes.PropertiesOf(eventType),
            evidence));
        placements.Add(new(
            $"marten:placement:event:{eventSubject.Value}:{modelSubject.Value}{suffix}",
            new ArtifactKey { Subject = eventSubject, Kind = ArtifactKind.Event },
            null,
            CritterStackSourcePlacement.CompatibilityPlacement(
                project,
                options,
                "Events",
                eventType.Name,
                GenerationSliceKind.StateView),
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
                CritterStackSource.EvidenceFor(method, adapter, project, EvidenceStrength.Conventional, $"Marten treats {method.Name} as an event evolution method"));
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

    static string? SourceFileOf(ISymbol symbol, DotNetProjectCompilation project) =>
        CritterStackSource.EvidenceFor(symbol, new AdapterIdentity { Id = "source", Version = "1" }, project, EvidenceStrength.Exact).Source?.Path;

    static GenerationDiagnostic MultiStreamDiagnostic(
        DotNetProjectCompilation project,
        CritterStackSubjectResolver subjects,
        ProjectionRegistration registration) => new()
        {
            Code = MartenDiagnosticCodes.MultiStreamGroupingOmitted,
            Severity = GenerationDiagnosticSeverity.Warning,
            Outcome = GenerationDiagnosticOutcome.Unsupported,
            Message = $"Multi-stream projection '{registration.Projection!.Name}' is represented as an event reducer; exact authored grouping and fan-out declarations are retained as neutral evidence, but those semantics are not expressible in the current Screenplay language",
            Source = registration.Evidence.Source,
            Subject = subjects.SubjectForType(project, registration.Projection)
        };

    static GenerationDiagnostic ProjectionLifecycleDiagnostic(
        DotNetProjectCompilation project,
        CritterStackSubjectResolver subjects,
        ProjectionRegistration registration) => new()
        {
            Code = MartenDiagnosticCodes.ProjectionLifecycleOmitted,
            Severity = GenerationDiagnosticSeverity.Warning,
            Outcome = GenerationDiagnosticOutcome.Unsupported,
            Message = $"Projection '{(registration.Projection ?? registration.Model).Name}' uses the {registration.Lifecycle} lifecycle, which is not expressible in the current Screenplay language",
            Source = registration.Evidence.Source,
            Subject = subjects.SubjectForType(project, registration.Projection ?? registration.Model)
        };

    static GenerationDiagnostic CustomProjectionDiagnostic(
        DotNetProjectCompilation project,
        CritterStackSubjectResolver subjects,
        ProjectionRegistration registration) => new()
        {
            Code = MartenDiagnosticCodes.CustomProcessingOmitted,
            Severity = GenerationDiagnosticSeverity.Warning,
            Outcome = GenerationDiagnosticOutcome.Unsupported,
            Message = $"Custom Marten projection '{registration.Projection!.Name}' is preserved as a neutral projection artifact, but its arbitrary processing consequences were not inferred",
            Source = registration.Evidence.Source,
            Subject = subjects.SubjectForType(project, registration.Projection)
        };
}
