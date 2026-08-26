// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Cratis.CritterStack.Screenplay.for_CritterStackScreenplayAdapter;

public class when_deriving_placement_with_invalid_evidence_strength : given.a_shared_source_placement_application
{
    DerivationResult _exactAndUnknown = null!;
    DerivationResult _noPartialPlacement = null!;
    DerivationResult _reversedExactAndUnknown = null!;
    DerivationResult _undefined = null!;
    DerivationResult _unknown = null!;

    void Because()
    {
        var context = new DotNetAnalysisContext([Project]);
        var baseline = Adapter.Analyze(context, AdapterOptions);
        var command = baseline.Facts
            .OfType<ArtifactFact>()
            .Single(_ => _.Definition.Key.Kind == ArtifactKind.Command && _.Definition.Name == "SubmitOrder");
        var query = baseline.Facts
            .OfType<ArtifactFact>()
            .Single(_ => _.Definition.Key.Kind == ArtifactKind.Query && _.Definition.Name == "GetOrder");
        var placement = CritterStackSourcePlacement.CompatibilityPlacement(
            Project,
            AdapterOptions,
            "Order",
            "SubmitOrder",
            GenerationSliceKind.StateChange);
        var exact = Intent("test:placement:exact", command, placement, EvidenceStrength.Exact);
        var unknown = Intent("test:placement:unknown", command, placement, EvidenceStrength.Unknown);
        var undefined = Intent("test:placement:undefined", command, placement, (EvidenceStrength)int.MaxValue);

        _unknown = Derive(context, [unknown]);
        _undefined = Derive(context, [undefined]);
        _exactAndUnknown = Derive(context, [exact, unknown]);
        _reversedExactAndUnknown = Derive(context, [unknown, exact]);
        _noPartialPlacement = Derive(
            context,
            [
                exact,
                Intent(
                    "test:placement:query:undefined",
                    query,
                    placement with { Slice = "GetOrder", SliceKind = GenerationSliceKind.StateView },
                    (EvidenceStrength)int.MaxValue)
            ]);
    }

    [Fact] void should_report_unknown_evidence_strength_as_an_error() => Diagnostic(_unknown).ShouldEqual((GenerationDiagnosticCodes.UnsupportedEvidenceStrength, GenerationDiagnosticSeverity.Error, GenerationDiagnosticOutcome.Unknown));
    [Fact] void should_emit_no_placement_for_unknown_evidence_strength() => _unknown.Facts.ShouldBeEmpty();
    [Fact] void should_report_undefined_evidence_strength_as_an_error() => Diagnostic(_undefined).ShouldEqual((GenerationDiagnosticCodes.UnsupportedEvidenceStrength, GenerationDiagnosticSeverity.Error, GenerationDiagnosticOutcome.Unsupported));
    [Fact] void should_emit_no_placement_for_undefined_evidence_strength() => _undefined.Facts.ShouldBeEmpty();
    [Fact] void should_not_allow_unknown_evidence_to_outrank_exact_evidence() => _exactAndUnknown.Facts.ShouldBeEmpty();
    [Fact] void should_report_exact_plus_unknown_evidence_instead_of_discarding_the_unknown_intent() => Diagnostic(_exactAndUnknown).ShouldEqual((GenerationDiagnosticCodes.UnsupportedEvidenceStrength, GenerationDiagnosticSeverity.Error, GenerationDiagnosticOutcome.Unknown));
    [Fact] void should_keep_invalid_evidence_diagnostics_stable_when_intent_order_is_reversed() => DiagnosticSignatures(_reversedExactAndUnknown).SequenceEqual(DiagnosticSignatures(_exactAndUnknown)).ShouldBeTrue();
    [Fact] void should_emit_no_placement_when_intent_order_is_reversed() => _reversedExactAndUnknown.Facts.ShouldBeEmpty();
    [Fact] void should_atomically_discard_valid_placements_when_another_artifact_has_invalid_evidence() => _noPartialPlacement.Facts.ShouldBeEmpty();
    [Fact] void should_report_the_invalid_artifact_in_a_mixed_batch() => _noPartialPlacement.Diagnostics.Single().Subject.ShouldEqual(_noPartialPlacement.InvalidSubject);

    static DerivationResult Derive(
        DotNetAnalysisContext context,
        IReadOnlyList<CritterStackPlacementIntent> intents)
    {
        var diagnostics = new List<GenerationDiagnostic>();
        var facts = CritterStackSourcePlacement.Derive(context, new(), intents, diagnostics);
        var invalidSubject = intents.SingleOrDefault(_ => _.Evidence.Strength == EvidenceStrength.Unknown || !Enum.IsDefined(_.Evidence.Strength))?.Artifact.Subject;
        return new(facts, diagnostics, invalidSubject);
    }

    static CritterStackPlacementIntent Intent(
        string id,
        ArtifactFact artifact,
        ArtifactPlacement placement,
        EvidenceStrength strength) => new(
            id,
            artifact.Definition.Key,
            null,
            placement,
            artifact.Evidence with { Strength = strength });

    static (string Code, GenerationDiagnosticSeverity Severity, GenerationDiagnosticOutcome Outcome) Diagnostic(DerivationResult result)
    {
        var diagnostic = result.Diagnostics.Single();
        return (diagnostic.Code, diagnostic.Severity, diagnostic.Outcome!.Value);
    }

    static IReadOnlyList<string> DiagnosticSignatures(DerivationResult result) =>
    [
        .. result.Diagnostics.Select(_ => $"{_.Code}|{_.Severity}|{_.Outcome}|{_.Subject?.Value}|{_.Source?.Path}|{_.Message}")
    ];

    sealed record DerivationResult(
        IReadOnlyList<GenerationFact> Facts,
        IReadOnlyList<GenerationDiagnostic> Diagnostics,
        SubjectId? InvalidSubject);
}
