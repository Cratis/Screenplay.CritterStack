// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Cratis.CritterStack.Screenplay.for_CritterStackScreenplayAdapter;

public class when_deriving_cross_project_source_placement : given.a_cross_project_source_placement_application
{
    AdapterContribution _ambiguous = null!;
    AdapterContribution _ambiguousAndUnrelated = null!;
    AdapterContribution _ambiguousRelocated = null!;
    AdapterContribution _ambiguousReversed = null!;
    AdapterContribution _contextCompatibility = null!;
    AdapterContribution _contribution = null!;
    SubjectId _customWolverinePolicy = null!;
    SubjectId _domainEvent = null!;
    SubjectId _domainMessage = null!;
    SubjectId _domainModel = null!;
    SubjectId _domainRequest = null!;
    IReadOnlyList<GenerationDiagnostic> _diagnosticOnlyMarten = null!;
    IReadOnlyList<GenerationDiagnostic> _diagnosticOnlyWolverine = null!;
    bool _diagnosticOnlyMartenMissingBlocked;
    bool _diagnosticOnlyWolverineAmbiguityBlocked;
    SubjectId _projection = null!;
    AdapterContribution _relocated = null!;
    AdapterContribution _reversed = null!;
    AdapterContribution _rootOnly = null!;
    AdapterContribution _legacy = null!;
    AdapterContribution _legacyCompatibility = null!;
    AdapterContribution _missingOwnerWithUnrelatedCollision = null!;
    IReadOnlyList<CritterStackPlacementIntent> _placementIntents = null!;
    AdapterContribution _withUnrelatedCollision = null!;
    AdapterContribution _withUnrelatedCollisionReversed = null!;

    void Because()
    {
        var projects = CreateProjects();
        _domainRequest = SubjectFor(projects.Domain, "Domain.Orders.SubmitOrder");
        _domainEvent = SubjectFor(projects.Domain, "Domain.Orders.OrderSubmitted");
        _domainMessage = SubjectFor(projects.Domain, "Domain.Orders.NotifyOrder");
        _domainModel = SubjectFor(projects.Domain, "Domain.Orders.OrderSummary");
        _projection = SubjectFor(projects.Application, "Application.Orders.Configuration.OrderSummaryProjection");
        _customWolverinePolicy = SubjectFor(projects.Application, "Application.Orders.Configuration.CustomWolverinePolicy");
        var context = new DotNetAnalysisContext([projects.Application, projects.Domain]);
        var subjects = new CritterStackSubjectResolver(context);
        var diagnosticScanner = projects.Application with
        {
            Name = "DiagnosticScanner",
            ProjectPath = "/workspace/DiagnosticScanner/DiagnosticScanner.csproj"
        };
        _diagnosticOnlyMarten = MartenTenancyConfigurationDiscovery.Discover(diagnosticScanner, subjects);
        _diagnosticOnlyWolverine = WolverineConventionAlterationDiscovery.Discover(diagnosticScanner, subjects);

        var missingDiagnosticSubjects = new CritterStackSubjectResolver(new([projects.Application]));
        var missingDiagnosticCheckpoint = missingDiagnosticSubjects.Checkpoint();
        _ = MartenTenancyConfigurationDiscovery.Discover(diagnosticScanner, missingDiagnosticSubjects);
        _diagnosticOnlyMartenMissingBlocked = missingDiagnosticSubjects.HasBlockingDiagnosticsSince(missingDiagnosticCheckpoint);

        var duplicateApplication = projects.Application with
        {
            Name = "ApplicationDuplicate",
            ProjectPath = "/workspace/ApplicationDuplicate/ApplicationDuplicate.csproj"
        };
        var ambiguousDiagnosticSubjects = new CritterStackSubjectResolver(new([projects.Application, duplicateApplication, projects.Domain]));
        var ambiguousDiagnosticCheckpoint = ambiguousDiagnosticSubjects.Checkpoint();
        _ = WolverineConventionAlterationDiscovery.Discover(diagnosticScanner, ambiguousDiagnosticSubjects);
        _diagnosticOnlyWolverineAmbiguityBlocked = ambiguousDiagnosticSubjects.HasBlockingDiagnosticsSince(ambiguousDiagnosticCheckpoint);
        var marten = Marten.MartenFacts.Discover(projects.Application, AdapterOptions, Adapter.Identity, subjects);
        var wolverine = Wolverine.WolverineFacts.Discover(projects.Application, AdapterOptions, Adapter.Identity, subjects);
        _placementIntents = [.. marten.Placements ?? [], .. wolverine.Placements ?? []];
        _contribution = Adapter.Analyze(context, AdapterOptions);
        _contextCompatibility = Adapter.AnalyzeCompatibility(context, AdapterOptions);
        _reversed = Adapter.Analyze(new([projects.Domain, projects.Application]), AdapterOptions);
        var unrelatedCollision = CreateMetadataNameCollisionProject();
        _withUnrelatedCollision = Adapter.Analyze(
            new([projects.Application, projects.Domain, unrelatedCollision]),
            AdapterOptions);
        _withUnrelatedCollisionReversed = Adapter.Analyze(
            new([unrelatedCollision, projects.Domain, projects.Application]),
            AdapterOptions);
        _missingOwnerWithUnrelatedCollision = Adapter.Analyze(
            new([projects.Application, unrelatedCollision]),
            AdapterOptions);

        var relocated = CreateProjects("/relocated");
        _relocated = Adapter.Analyze(new([relocated.Application, relocated.Domain]), AdapterOptions);
        _rootOnly = Adapter.Analyze(new([projects.Application]), AdapterOptions);
        var ambiguousContext = AmbiguousContext(projects, reverse: false);
        _ambiguous = Adapter.Analyze(ambiguousContext, AdapterOptions);
        _ambiguousAndUnrelated = Adapter.Analyze(
            new([.. ambiguousContext.Projects, CreateIndependentProject(projects)]),
            AdapterOptions);
        _ambiguousReversed = Adapter.Analyze(AmbiguousContext(projects, reverse: true), AdapterOptions);
        _ambiguousRelocated = Adapter.Analyze(AmbiguousContext(relocated, reverse: false), AdapterOptions);

        var legacyProjects = CreateProjects(includeSourceContexts: false);
        var legacyContext = new DotNetAnalysisContext([legacyProjects.Application, legacyProjects.Domain]);
        _legacy = Adapter.Analyze(legacyContext, AdapterOptions);
        _legacyCompatibility = Adapter.AnalyzeCompatibility(legacyContext, AdapterOptions);
    }

    [Fact] void should_resolve_the_domain_command_to_its_exact_owning_project() => Artifact(ArtifactKind.Command, "SubmitOrder").Key.Subject.ShouldEqual(_domainRequest);
    [Fact] void should_resolve_the_domain_event_to_its_exact_owning_project() => Artifact(ArtifactKind.Event, "OrderSubmitted").Key.Subject.ShouldEqual(_domainEvent);
    [Fact] void should_resolve_the_domain_message_to_its_exact_owning_project() => Artifact(ArtifactKind.Message, "NotifyOrder").Key.Subject.ShouldEqual(_domainMessage);
    [Fact] void should_resolve_the_domain_read_model_to_its_exact_owning_project() => Artifact(ArtifactKind.ReadModel, "OrderSummary").Key.Subject.ShouldEqual(_domainModel);
    [Fact] void should_resolve_the_true_owner_when_an_unrelated_assembly_has_the_same_metadata_names() => Artifact(_withUnrelatedCollision, ArtifactKind.Command, "SubmitOrder").Key.Subject.ShouldEqual(_domainRequest);
    [Fact] void should_not_report_missing_ownership_when_the_true_owner_and_an_unrelated_collision_are_present() => _withUnrelatedCollision.Diagnostics.Select(_ => _.Code).ShouldNotContain(DotNetSourceStructureDiagnosticCodes.MissingSourceMapping);
    [Fact] void should_report_missing_ownership_when_only_an_unrelated_same_name_assembly_is_present() => OwnershipDiagnostics(_missingOwnerWithUnrelatedCollision, "Domain.Orders.SubmitOrder").Single().Outcome.ShouldEqual(GenerationDiagnosticOutcome.Unknown);
    [Fact] void should_discard_all_project_facts_when_only_an_unrelated_same_name_assembly_is_present() => _missingOwnerWithUnrelatedCollision.Facts.ShouldBeEmpty();
    [Fact] void should_emit_no_placements_when_only_an_unrelated_same_name_assembly_is_present() => _missingOwnerWithUnrelatedCollision.Facts.OfType<ArtifactPlacementFact>().ShouldBeEmpty();
    [Fact] void should_keep_same_name_collision_results_deterministic_when_project_order_is_reversed() => FactSignatures(_withUnrelatedCollisionReversed).SequenceEqual(FactSignatures(_withUnrelatedCollision)).ShouldBeTrue();
    [Fact] void should_keep_same_name_collision_diagnostics_deterministic_when_project_order_is_reversed() => DiagnosticSignatures(_withUnrelatedCollisionReversed).SequenceEqual(DiagnosticSignatures(_withUnrelatedCollision)).ShouldBeTrue();
    [Fact] void should_use_exact_cross_project_relationship_subjects() => Relationships.ShouldContain(_ =>
        _.Kind == RelationshipKind.Produces &&
        _.Source == _domainRequest &&
        _.Target == _domainEvent);
    [Fact] void should_use_the_exact_domain_target_for_the_root_projection_relationship() => Relationships.ShouldContain(_ =>
        _.Kind == RelationshipKind.Builds &&
        _.Source.Value == $"{_projection.Value}#reducer" &&
        _.Target == _domainModel);
    [Fact] void should_keep_type_backed_placement_requests_self_owned() => new[]
    {
        PlacementIntents(ArtifactKind.Command, _domainRequest),
        PlacementIntents(ArtifactKind.Event, _domainEvent),
        PlacementIntents(ArtifactKind.ReadModel, _domainModel)
    }.SelectMany(_ => _).All(_ => _.SourceOwner is null).ShouldBeTrue();
    [Fact] void should_keep_the_exact_projection_owner_for_the_synthetic_reducer() => PlacementIntent(ArtifactKind.Reducer, new SubjectId { Value = $"{_projection.Value}#reducer" }).SourceOwner.ShouldEqual(_projection);
    [Fact] void should_keep_the_exact_containing_type_owner_for_the_query_method() => _placementIntents.Single(_ => _.Artifact.Kind == ArtifactKind.Query).SourceOwner.ShouldEqual(SubjectForContainingType("Application.Orders.Configuration.OrderEndpoints"));
    [Fact] void should_keep_the_exact_containing_type_owner_for_the_reaction_method() => _placementIntents.Single(_ => _.Artifact.Kind == ArtifactKind.Reaction).SourceOwner.ShouldEqual(SubjectForContainingType("Application.Orders.Configuration.NotificationHandler"));
    [Fact] void should_use_the_exact_domain_subject_for_diagnostic_only_marten_tenancy_discovery() => _diagnosticOnlyMarten.Single(_ => _.Code == MartenDiagnosticCodes.TenancyConfigurationOmitted).Subject.ShouldEqual(_domainModel);
    [Fact] void should_use_the_exact_application_subject_for_diagnostic_only_wolverine_convention_discovery() => _diagnosticOnlyWolverine.Single(_ => _.Code == WolverineDiagnosticCodes.ConventionAlterationOmitted).Subject.ShouldEqual(_customWolverinePolicy);
    [Fact] void should_block_when_a_diagnostic_only_marten_owner_is_missing() => _diagnosticOnlyMartenMissingBlocked.ShouldBeTrue();
    [Fact] void should_block_when_a_diagnostic_only_wolverine_owner_is_ambiguous() => _diagnosticOnlyWolverineAmbiguityBlocked.ShouldBeTrue();
    [Fact] void should_use_the_exact_domain_subject_for_the_wolverine_handler_chain_diagnostic() => _contribution.Diagnostics.Single(_ => _.Code == WolverineDiagnosticCodes.HandlerChainConfigurationOmitted && _.Message.Contains("SubmitOrder", StringComparison.Ordinal)).Subject.ShouldEqual(_domainRequest);
    [Fact] void should_not_report_a_missing_source_mapping_with_both_projects() => _contribution.Diagnostics.Select(_ => _.Code).ShouldNotContain(DotNetSourceStructureDiagnosticCodes.MissingSourceMapping);
    [Fact] void should_emit_cross_project_placements_with_both_projects() => _contribution.Facts.OfType<ArtifactPlacementFact>().ShouldNotBeEmpty();
    [Fact] void should_keep_exact_cross_project_subjects_with_explicit_compatibility_placement() => Artifact(_contextCompatibility, ArtifactKind.Command, "SubmitOrder").Key.Subject.ShouldEqual(_domainRequest);
    [Fact] void should_use_legacy_placement_with_context_owned_subjects() => Placement(_contextCompatibility, ArtifactKind.Command, _domainRequest).Module.ShouldEqual("Application");
    [Fact] void should_fail_closed_when_the_owning_project_is_missing() => _rootOnly.Diagnostics.Select(_ => _.Code).ShouldContain(DotNetSourceStructureDiagnosticCodes.MissingSourceMapping);
    [Fact] void should_collapse_duplicate_unresolved_message_uses() => OwnershipDiagnostics(_rootOnly, "Domain.Orders.NotifyOrder").Count.ShouldEqual(1);
    [Fact] void should_identify_the_unresolved_message_without_an_absolute_path() => OwnershipDiagnostics(_rootOnly, "Domain.Orders.NotifyOrder").Single().Source.ShouldBeNull();
    [Fact] void should_emit_no_project_facts_when_the_owning_project_is_missing() => _rootOnly.Facts.ShouldBeEmpty();
    [Fact] void should_not_leak_an_unplaced_wolverine_message_when_the_owning_project_is_missing() => _rootOnly.Facts.OfType<ArtifactFact>().Any(_ => _.Definition.Key.Kind == ArtifactKind.Message && _.Definition.Name == "NotifyOrder").ShouldBeFalse();
    [Fact] void should_not_leak_an_unplaced_wolverine_automation_when_the_owning_project_is_missing() => _rootOnly.Facts.OfType<ArtifactFact>().Any(_ => _.Definition.Key.Kind == ArtifactKind.Reaction && _.Definition.Name == "Notification").ShouldBeFalse();
    [Fact] void should_emit_no_placements_when_the_owning_project_is_missing() => _rootOnly.Facts.OfType<ArtifactPlacementFact>().ShouldBeEmpty();
    [Fact] void should_never_use_compatibility_when_the_owning_project_is_missing() => _rootOnly.Facts.OfType<ArtifactPlacementFact>().Any(_ => _.Evidence.Explanation?.Contains("usedCompatibility=true", StringComparison.Ordinal) == true).ShouldBeFalse();
    [Fact] void should_report_ambiguous_exact_source_ownership_as_blocking() => OwnershipDiagnostics(_ambiguous, "Domain.Orders.NotifyOrder").Single().Severity.ShouldEqual(GenerationDiagnosticSeverity.Error);
    [Fact] void should_treat_duplicate_projects_with_the_same_exact_assembly_identity_as_ambiguous() => OwnershipDiagnostics(_ambiguous, "Domain.Orders.SubmitOrder").Single().Outcome.ShouldEqual(GenerationDiagnosticOutcome.Conflict);
    [Fact] void should_report_ambiguous_exact_source_ownership_as_a_conflict() => OwnershipDiagnostics(_ambiguous, "Domain.Orders.NotifyOrder").Single().Outcome.ShouldEqual(GenerationDiagnosticOutcome.Conflict);
    [Fact] void should_collapse_duplicate_ambiguous_message_uses() => OwnershipDiagnostics(_ambiguous, "Domain.Orders.NotifyOrder").Count.ShouldEqual(1);
    [Fact] void should_discard_all_facts_for_the_project_with_ambiguous_ownership() => _ambiguous.Facts.ShouldBeEmpty();
    [Fact] void should_emit_no_placements_for_the_project_with_ambiguous_ownership() => _ambiguous.Facts.OfType<ArtifactPlacementFact>().ShouldBeEmpty();
    [Fact] void should_retain_unrelated_project_facts_when_another_project_has_ambiguous_ownership() => _ambiguousAndUnrelated.Facts.OfType<ArtifactFact>().Any(_ => _.Definition.Key.Kind == ArtifactKind.Reaction && _.Subject.Value.Contains("dotnet://Payments/", StringComparison.Ordinal)).ShouldBeTrue();
    [Fact] void should_retain_unrelated_project_placements_when_another_project_has_ambiguous_ownership() => _ambiguousAndUnrelated.Facts.OfType<ArtifactPlacementFact>().Any(_ => _.Artifact.Kind == ArtifactKind.Reaction && _.Artifact.Subject.Value.Contains("dotnet://Payments/", StringComparison.Ordinal)).ShouldBeTrue();
    [Fact] void should_keep_ambiguous_ownership_diagnostics_stable_when_project_order_is_reversed() => DiagnosticSignatures(_ambiguousReversed).SequenceEqual(DiagnosticSignatures(_ambiguous)).ShouldBeTrue();
    [Fact] void should_keep_ambiguous_ownership_diagnostics_stable_after_relocation() => DiagnosticSignatures(_ambiguousRelocated).SequenceEqual(DiagnosticSignatures(_ambiguous)).ShouldBeTrue();
    [Fact] void should_not_include_absolute_paths_in_ambiguous_ownership_diagnostics() => _ambiguous.Diagnostics.Where(_ => _.Code == DotNetSourceStructureDiagnosticCodes.MissingSourceMapping).All(_ => _.Source is null && !_.Message.Contains("/workspace", StringComparison.Ordinal)).ShouldBeTrue();
    [Fact] void should_be_deterministic_when_project_order_is_reversed() => FactSignatures(_reversed).SequenceEqual(FactSignatures(_contribution)).ShouldBeTrue();
    [Fact] void should_keep_diagnostics_deterministic_when_project_order_is_reversed() => DiagnosticSignatures(_reversed).SequenceEqual(DiagnosticSignatures(_contribution)).ShouldBeTrue();
    [Fact] void should_be_deterministic_after_physical_relocation() => FactSignatures(_relocated).SequenceEqual(FactSignatures(_contribution)).ShouldBeTrue();
    [Fact] void should_keep_diagnostics_deterministic_after_physical_relocation() => DiagnosticSignatures(_relocated).SequenceEqual(DiagnosticSignatures(_contribution)).ShouldBeTrue();
    [Fact] void should_preserve_legacy_no_context_facts() => FactSignatures(_legacy).SequenceEqual(FactSignatures(_legacyCompatibility)).ShouldBeTrue();
    [Fact] void should_preserve_legacy_no_context_diagnostics() => DiagnosticSignatures(_legacy).SequenceEqual(DiagnosticSignatures(_legacyCompatibility)).ShouldBeTrue();

    IReadOnlyList<RelationshipKey> Relationships =>
    [
        .. _contribution.Facts
            .OfType<RelationshipFact>()
            .Select(_ => _.Definition.Key)
    ];

    ResolvedArtifact Artifact(ArtifactKind kind, string name) => Artifact(_contribution, kind, name);

    static ResolvedArtifact Artifact(AdapterContribution contribution, ArtifactKind kind, string name) => new GenerationResolver()
        .Resolve([contribution])
        .Artifacts
        .Single(_ => _.Key.Kind == kind && _.Variants.Any(variant => variant.Definition.Name == name));

    static ArtifactPlacement Placement(AdapterContribution contribution, ArtifactKind kind, SubjectId subject) =>
        contribution.Facts
            .OfType<ArtifactPlacementFact>()
            .Single(_ => _.Artifact == new ArtifactKey { Kind = kind, Subject = subject })
            .Placement;

    CritterStackPlacementIntent PlacementIntent(ArtifactKind kind, SubjectId subject) =>
        PlacementIntents(kind, subject).Single();

    IEnumerable<CritterStackPlacementIntent> PlacementIntents(ArtifactKind kind, SubjectId subject) =>
        _placementIntents.Where(_ => _.Artifact == new ArtifactKey { Kind = kind, Subject = subject });

    SubjectId SubjectForContainingType(string metadataName)
    {
        var projects = CreateProjects();
        return SubjectFor(projects.Application, metadataName);
    }

    static SubjectId SubjectFor(DotNetProjectCompilation project, string metadataName) =>
        project.SubjectForType(project.Compilation.GetTypeByMetadataName(metadataName)!);

    static DotNetAnalysisContext AmbiguousContext(ProjectPair projects, bool reverse)
    {
        var duplicateDomain = projects.Domain with
        {
            Name = "DomainDuplicate",
            ProjectPath = projects.Domain.ProjectPath?.Replace("Domain.csproj", "DomainDuplicate.csproj", StringComparison.Ordinal)
        };
        return reverse
            ? new([duplicateDomain, projects.Domain, projects.Application])
            : new([projects.Application, projects.Domain, duplicateDomain]);
    }

    static IReadOnlyList<GenerationDiagnostic> OwnershipDiagnostics(AdapterContribution contribution, string typeName) =>
    [
        .. contribution.Diagnostics.Where(_ =>
            _.Code == DotNetSourceStructureDiagnosticCodes.MissingSourceMapping &&
            _.Message.Contains(typeName, StringComparison.Ordinal))
    ];

    static IReadOnlyList<string> FactSignatures(AdapterContribution contribution) =>
    [
        .. contribution.Facts.Select(_ => $"{_.GetType().FullName}|{System.Text.Json.JsonSerializer.Serialize(_, _.GetType())}")
    ];

    static IReadOnlyList<string> DiagnosticSignatures(AdapterContribution contribution) =>
    [
        .. contribution.Diagnostics.Select(_ => $"{_.Code}|{_.Severity}|{_.Outcome}|{_.Subject?.Value}|{_.Source?.Path}|{_.Source?.StartLine}|{_.Source?.StartColumn}|{_.Message}")
    ];
}
