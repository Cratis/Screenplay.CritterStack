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
    public static MartenDiscoveryResult Discover(
        DotNetProjectCompilation project,
        DotNetAdapterOptions options,
        AdapterIdentity adapter,
        CritterStackSubjectResolver subjects,
        MartenStoreIndex stores) => Discover(new(project, options, adapter, subjects), stores);

    public static MartenDiscoveryResult Discover(MartenCollectedFacts collected, MartenStoreIndex stores)
    {
        var (project, adapter, subjects) = (collected.Project, collected.Adapter, collected.Subjects);
        if (project.Compilation.GetTypeByMetadataName(WellKnownTypes.MartenStoreOptions) is null &&
            project.Compilation.GetTypeByMetadataName(WellKnownTypes.MartenDocumentStore) is null)
        {
            return new([], [], []);
        }

        var registrations = MartenProjectionDiscovery.Discover(project, adapter, stores);
        var configuration = MartenConfigurationDiscovery.Discover(project, adapter, subjects, registrations);
        collected.Facts.AddRange(configuration.Facts);
        collected.Diagnostics.AddRange(configuration.Diagnostics);
        collected.Diagnostics.AddRange(MartenEventSchemaConfigurationDiscovery.Discover(project, subjects));
        collected.Diagnostics.AddRange(MartenTenancyConfigurationDiscovery.Discover(project, subjects));
        collected.Diagnostics.AddRange(MartenProjectionKindDiscovery.Discover(project));
        HashSet<INamedTypeSymbol> sideEffectProjections = [with(SymbolEqualityComparer.Default)];

        foreach (var registration in registrations)
        {
            AddProjectionRegistration(collected, registration, sideEffectProjections, configuration.SideEffectsEnabled);
        }

        return new(collected.Facts, collected.Diagnostics, collected.Documents, collected.Placements);
    }

    static void AddProjectionRegistration(
        MartenCollectedFacts collected,
        ProjectionRegistration registration,
        HashSet<INamedTypeSymbol> sideEffectProjections,
        bool sideEffectsEnabled)
    {
        if (!registration.Ownership.CanEmit)
        {
            collected.Diagnostics.Add(registration.Ownership.Diagnostic(
                collected.Subjects.SubjectForType(collected.Project, registration.Projection ?? registration.Model),
                registration.Evidence.Source,
                "Marten projection ownership cannot be proved"));
            return;
        }

        var sideEffectProjection = registration.Projection ?? registration.Model;
        if (registration.Kind != ProjectionKind.Custom && sideEffectProjections.Add(sideEffectProjection))
        {
            AddSideEffectFacts(collected, registration, sideEffectsEnabled);
        }

        if (HasUnmodeledLifecycle(registration))
        {
            collected.Diagnostics.Add(ProjectionLifecycleDiagnostic(collected.Project, collected.Subjects, registration));
        }

        AddRegistrationFacts(collected.WithOwnership(registration.Ownership), registration);
    }

    static void AddSideEffectFacts(MartenCollectedFacts collected, ProjectionRegistration registration, bool sideEffectsEnabled)
    {
        var sideEffects = MartenProjectionSideEffects.Discover(collected, registration, sideEffectsEnabled);
        collected.Facts.AddRange(sideEffects.Facts);
        collected.Diagnostics.AddRange(sideEffects.Diagnostics);
    }

    static bool HasUnmodeledLifecycle(ProjectionRegistration registration) =>
        string.Equals(registration.Lifecycle, "Async", StringComparison.Ordinal) ||
        string.Equals(registration.Lifecycle, "Live", StringComparison.Ordinal);

    static void AddRegistrationFacts(MartenCollectedFacts collected, ProjectionRegistration registration)
    {
        var (project, adapter, subjects) = (collected.Project, collected.Adapter, collected.Subjects);
        var multiStreamConfiguration = MartenMultiStreamConfiguration.Empty;
        switch (registration.Kind)
        {
            case ProjectionKind.Event:
                var eventProjection = MartenEventProjectionFacts.Discover(project, adapter, subjects, registration);
                collected.Facts.AddRange(eventProjection.Facts);
                collected.Diagnostics.AddRange(eventProjection.Diagnostics);
                collected.Documents.AddRange(eventProjection.Documents);
                return;
            case ProjectionKind.MultiStream:
                collected.Diagnostics.Add(MultiStreamDiagnostic(project, subjects, registration));
                multiStreamConfiguration = MartenMultiStreamConfigurationDiscovery.Discover(
                    project,
                    adapter,
                    subjects,
                    registration.Projection!);
                collected.Diagnostics.AddRange(multiStreamConfiguration.Diagnostics);
                break;
            case ProjectionKind.Custom:
                AddCustomProjectionFacts(project, subjects, registration, collected.Facts);
                collected.Diagnostics.Add(CustomProjectionDiagnostic(project, subjects, registration));
                return;
        }

        MartenAggregateProjectionFacts.Add(collected, registration, multiStreamConfiguration);
    }

    static void AddCustomProjectionFacts(
        DotNetProjectCompilation project,
        CritterStackSubjectResolver subjects,
        ProjectionRegistration registration,
        List<GenerationFact> facts)
    {
        var projection = registration.Projection!;
        var subject = subjects.SubjectForType(project, projection);
        facts.Add(new MartenArtifactFact
        {
            Id = $"marten:custom-projection:{subject.Value}",
            Subject = subject,
            Kind = ArtifactKind.Projection,
            Name = projection.Name,
            File = MartenSourceFiles.FileOf(projection, project),
            Evidence = registration.Evidence
        }.ToFact(registration.Ownership));
    }

    static GenerationDiagnostic MultiStreamDiagnostic(
        DotNetProjectCompilation project,
        CritterStackSubjectResolver subjects,
        ProjectionRegistration registration) => OmittedProjectionDiagnostic(
            MartenDiagnosticCodes.MultiStreamGroupingOmitted,
            $"Multi-stream projection '{registration.Projection!.Name}' is represented as an event reducer; exact authored grouping and fan-out declarations are retained as neutral evidence, but those semantics are not expressible in the current Screenplay language",
            registration,
            subjects.SubjectForType(project, registration.Projection));

    static GenerationDiagnostic ProjectionLifecycleDiagnostic(
        DotNetProjectCompilation project,
        CritterStackSubjectResolver subjects,
        ProjectionRegistration registration) => OmittedProjectionDiagnostic(
            MartenDiagnosticCodes.ProjectionLifecycleOmitted,
            $"Projection '{(registration.Projection ?? registration.Model).Name}' uses the {registration.Lifecycle} lifecycle, which is not expressible in the current Screenplay language",
            registration,
            subjects.SubjectForType(project, registration.Projection ?? registration.Model));

    static GenerationDiagnostic CustomProjectionDiagnostic(
        DotNetProjectCompilation project,
        CritterStackSubjectResolver subjects,
        ProjectionRegistration registration) => OmittedProjectionDiagnostic(
            MartenDiagnosticCodes.CustomProcessingOmitted,
            $"Custom Marten projection '{registration.Projection!.Name}' is preserved as a neutral projection artifact, but its arbitrary processing consequences were not inferred",
            registration,
            subjects.SubjectForType(project, registration.Projection));

    static GenerationDiagnostic OmittedProjectionDiagnostic(
        string code,
        string message,
        ProjectionRegistration registration,
        SubjectId subject) => new()
        {
            Code = code,
            Severity = GenerationDiagnosticSeverity.Warning,
            Outcome = GenerationDiagnosticOutcome.Unsupported,
            Message = message,
            Source = registration.Evidence.Source,
            Subject = subject
        };
}
