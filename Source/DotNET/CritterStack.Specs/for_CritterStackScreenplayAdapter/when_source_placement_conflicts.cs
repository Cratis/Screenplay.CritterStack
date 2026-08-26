// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Cratis.CritterStack.Screenplay.for_CritterStackScreenplayAdapter;

public class when_source_placement_conflicts : given.a_shared_source_placement_application
{
    AdapterContribution _conflictingPartial = null!;
    IReadOnlyList<GenerationDiagnostic> _conflictingOwnerDiagnostics = null!;
    IReadOnlyList<GenerationFact> _conflictingOwnerFacts = null!;
    IReadOnlyList<GenerationDiagnostic> _reversedConflictingOwnerDiagnostics = null!;
    IReadOnlyList<GenerationFact> _reversedConflictingOwnerFacts = null!;
    IReadOnlyList<GenerationDiagnostic> _conflictingRequestDiagnostics = null!;
    IReadOnlyList<GenerationFact> _conflictingRequestFacts = null!;
    IReadOnlyList<GenerationDiagnostic> _reversedConflictingRequestDiagnostics = null!;
    IReadOnlyList<GenerationFact> _reversedConflictingRequestFacts = null!;
    IReadOnlyList<GenerationDiagnostic> _strongerEvidenceDiagnostics = null!;
    IReadOnlyList<GenerationFact> _strongerEvidenceFacts = null!;
    AdapterContribution _duplicateSourceSubjects = null!;
    AdapterContribution _invalidRoot = null!;
    IReadOnlyList<GenerationDiagnostic> _missingOwnerDiagnostics = null!;
    IReadOnlyList<GenerationFact> _missingOwnerFacts = null!;
    SubjectId _missingOwner = null!;

    void Because()
    {
        var context = new DotNetAnalysisContext([Project]);
        var baseline = Adapter.Analyze(context, AdapterOptions);
        var command = baseline.Facts
            .OfType<ArtifactFact>()
            .Single(_ => _.Definition.Key.Kind == ArtifactKind.Command && _.Definition.Name == "SubmitOrder");
        var compatibilityPlacement = CritterStackSourcePlacement.CompatibilityPlacement(
            Project,
            AdapterOptions,
            "SubmitOrder",
            "SubmitOrder",
            GenerationSliceKind.StateChange);
        var intents = new[]
        {
            new CritterStackPlacementIntent(
                "test:placement:state-change",
                command.Definition.Key,
                null,
                compatibilityPlacement,
                command.Evidence),
            new CritterStackPlacementIntent(
                "test:placement:automation",
                command.Definition.Key,
                null,
                compatibilityPlacement with { SliceKind = GenerationSliceKind.Automation },
                command.Evidence)
        };
        var conflictingRequestDiagnostics = new List<GenerationDiagnostic>();
        _conflictingRequestFacts = CritterStackSourcePlacement.Derive(
            context,
            AdapterOptions,
            intents,
            conflictingRequestDiagnostics);
        _conflictingRequestDiagnostics = conflictingRequestDiagnostics;
        var reversedConflictingRequestDiagnostics = new List<GenerationDiagnostic>();
        _reversedConflictingRequestFacts = CritterStackSourcePlacement.Derive(
            context,
            AdapterOptions,
            intents.AsEnumerable().Reverse(),
            reversedConflictingRequestDiagnostics);
        _reversedConflictingRequestDiagnostics = reversedConflictingRequestDiagnostics;

        var strongerEvidenceDiagnostics = new List<GenerationDiagnostic>();
        _strongerEvidenceFacts = CritterStackSourcePlacement.Derive(
            context,
            AdapterOptions,
            [
                new CritterStackPlacementIntent(
                    "test:marten-like:placement:state-view",
                    command.Definition.Key,
                    null,
                    compatibilityPlacement with { SliceKind = GenerationSliceKind.StateView },
                    command.Evidence with { Strength = EvidenceStrength.Heuristic }),
                new CritterStackPlacementIntent(
                    "test:wolverine-like:placement:state-change",
                    command.Definition.Key,
                    null,
                    compatibilityPlacement,
                    command.Evidence with { Strength = EvidenceStrength.Exact })
            ],
            strongerEvidenceDiagnostics);
        _strongerEvidenceDiagnostics = strongerEvidenceDiagnostics;

        var query = baseline.Facts
            .OfType<ArtifactFact>()
            .Single(_ => _.Definition.Key.Kind == ArtifactKind.Query && _.Definition.Name == "GetOrder");
        var modelOwner = Project.SubjectForType(Project.Compilation.GetTypeByMetadataName("Application.Orders.Summary.OrderSummary")!);
        var endpointOwner = Project.SubjectForType(Project.Compilation.GetTypeByMetadataName("Application.Orders.Summary.OrderEndpoints")!);
        var queryCompatibilityPlacement = CritterStackSourcePlacement.CompatibilityPlacement(
            Project,
            AdapterOptions,
            "OrderSummary",
            "GetOrder",
            GenerationSliceKind.StateView);
        var validCommandIntent = new CritterStackPlacementIntent(
            "test:placement:valid-command",
            command.Definition.Key,
            null,
            compatibilityPlacement,
            command.Evidence);
        CritterStackPlacementIntent[] conflictingOwnerIntents =
        [
            validCommandIntent,
            new CritterStackPlacementIntent(
                "test:placement:query:model-owner",
                query.Definition.Key,
                modelOwner,
                queryCompatibilityPlacement,
                query.Evidence),
            new CritterStackPlacementIntent(
                "test:placement:query:endpoint-owner",
                query.Definition.Key,
                endpointOwner,
                queryCompatibilityPlacement,
                query.Evidence)
        ];
        var conflictingOwnerDiagnostics = new List<GenerationDiagnostic>();
        _conflictingOwnerFacts = CritterStackSourcePlacement.Derive(
            context,
            AdapterOptions,
            conflictingOwnerIntents,
            conflictingOwnerDiagnostics);
        _conflictingOwnerDiagnostics = conflictingOwnerDiagnostics;
        var reversedConflictingOwnerDiagnostics = new List<GenerationDiagnostic>();
        _reversedConflictingOwnerFacts = CritterStackSourcePlacement.Derive(
            context,
            AdapterOptions,
            conflictingOwnerIntents.AsEnumerable().Reverse(),
            reversedConflictingOwnerDiagnostics);
        _reversedConflictingOwnerDiagnostics = reversedConflictingOwnerDiagnostics;

        _missingOwner = new SubjectId { Value = $"{query.Subject.Value}#missing-owner" };
        var missingOwnerDiagnostics = new List<GenerationDiagnostic>();
        _missingOwnerFacts = CritterStackSourcePlacement.Derive(
            context,
            AdapterOptions,
            [
                validCommandIntent,
                new CritterStackPlacementIntent(
                    "test:placement:query:missing-owner",
                    query.Definition.Key,
                    _missingOwner,
                    queryCompatibilityPlacement,
                    query.Evidence)
            ],
            missingOwnerDiagnostics);
        _missingOwnerDiagnostics = missingOwnerDiagnostics;

        var partialProject = CreateProject(conflictingPartial: true);
        _conflictingPartial = Adapter.Analyze(new([partialProject]), AdapterOptions);
        _duplicateSourceSubjects = Adapter.Analyze(new([Project, Project]), AdapterOptions);
        _invalidRoot = Adapter.Analyze(
            context,
            AdapterOptions with { FeatureRoot = "../Source" });
    }

    [Fact] void should_fail_closed_for_conflicting_requests() => new[] { _conflictingRequestDiagnostics, _reversedConflictingRequestDiagnostics }.All(_ => _.Select(diagnostic => diagnostic.Code).SequenceEqual([DotNetSourceStructureDiagnosticCodes.ConflictingPlacementRequests])).ShouldBeTrue();
    [Fact] void should_emit_no_placement_for_conflicting_requests_in_either_order() => new[] { _conflictingRequestFacts, _reversedConflictingRequestFacts }.All(_ => _.Count == 0).ShouldBeTrue();
    [Fact] void should_report_the_same_request_conflict_in_reversed_order() => DiagnosticSignatures(_reversedConflictingRequestDiagnostics).SequenceEqual(DiagnosticSignatures(_conflictingRequestDiagnostics)).ShouldBeTrue();
    [Fact] void should_discard_strictly_weaker_placement_evidence_without_a_conflict() => _strongerEvidenceDiagnostics.ShouldBeEmpty();
    [Fact] void should_place_only_the_stronger_state_change_role() => _strongerEvidenceFacts.OfType<ArtifactPlacementFact>().Single().Placement.SliceKind.ShouldEqual(GenerationSliceKind.StateChange);
    [Fact] void should_emit_one_placement_for_the_stronger_role() => _strongerEvidenceFacts.Count.ShouldEqual(1);
    [Fact] void should_use_the_stronger_intent_as_the_fact_representative() => _strongerEvidenceFacts.Single().Id.Value.ShouldEqual("test:wolverine-like:placement:state-change");
    [Fact] void should_fail_closed_for_conflicting_exact_owners() => _conflictingOwnerDiagnostics.Select(_ => _.Code).SequenceEqual([DotNetSourceStructureDiagnosticCodes.ConflictingPlacementRequests]).ShouldBeTrue();
    [Fact] void should_atomically_discard_valid_placements_when_an_owner_conflicts() => _conflictingOwnerFacts.ShouldBeEmpty();
    [Fact] void should_report_the_same_owner_conflict_in_reversed_order() => DiagnosticSignatures(_reversedConflictingOwnerDiagnostics).SequenceEqual(DiagnosticSignatures(_conflictingOwnerDiagnostics)).ShouldBeTrue();
    [Fact] void should_atomically_discard_valid_placements_in_reversed_order() => _reversedConflictingOwnerFacts.ShouldBeEmpty();
    [Fact] void should_report_a_missing_owner_with_a_stable_source_mapping_diagnostic() => _missingOwnerDiagnostics.Single().Code.ShouldEqual(DotNetSourceStructureDiagnosticCodes.MissingSourceMapping);
    [Fact] void should_locate_the_missing_owner_diagnostic() => _missingOwnerDiagnostics.Single().Source.ShouldNotBeNull();
    [Fact] void should_identify_the_missing_owner_subject() => _missingOwnerDiagnostics.Single().Subject.ShouldEqual(_missingOwner);
    [Fact] void should_atomically_discard_valid_placements_when_an_owner_mapping_is_missing() => _missingOwnerFacts.ShouldBeEmpty();
    [Fact] void should_report_conflicting_partial_declarations() => _conflictingPartial.Diagnostics.Any(_ => _.Code == DotNetSourceStructureDiagnosticCodes.ConflictingStructure && _.Subject?.Value.EndsWith("/Application.Orders.Submit.SubmitOrder", StringComparison.Ordinal) == true).ShouldBeTrue();
    [Fact] void should_emit_no_placements_when_any_partial_declaration_conflicts() => _conflictingPartial.Facts.OfType<ArtifactPlacementFact>().ShouldBeEmpty();
    [Fact] void should_report_duplicate_source_subjects_without_throwing() => _duplicateSourceSubjects.Diagnostics.Select(_ => _.Code).ShouldContain(DotNetSourceStructureDiagnosticCodes.DuplicateSourceSubject);
    [Fact] void should_emit_no_placements_for_an_incomplete_source_snapshot() => _duplicateSourceSubjects.Facts.OfType<ArtifactPlacementFact>().ShouldBeEmpty();
    [Fact] void should_reject_a_traversing_feature_root() => _invalidRoot.Diagnostics.Select(_ => _.Code).ShouldContain(DotNetSourceStructureDiagnosticCodes.InvalidPath);
    [Fact] void should_not_use_compatibility_for_a_non_dotnetsp0004_diagnostic() => _invalidRoot.Facts.OfType<ArtifactPlacementFact>().ShouldBeEmpty();

    static IReadOnlyList<string> DiagnosticSignatures(IEnumerable<GenerationDiagnostic> diagnostics) =>
    [
        .. diagnostics.Select(_ => $"{_.Code}|{_.Outcome}|{_.Subject?.Value}|{_.Source?.Path}|{_.Message}")
    ];
}
