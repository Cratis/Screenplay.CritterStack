// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Cratis.CritterStack.Screenplay.for_AtomicTypeUseParity;

/// <summary>
/// Characterizes diagnostic-message parity when the same enum yields for different independently contributed reasons.
/// </summary>
public class when_one_yield_marker_meets_different_conflict_reasons : Specification
{
    GeneratedScreenplayDefinition _legacyConflict = null!;
    GeneratedScreenplayDefinition _legacyCapture = null!;
    GeneratedScreenplayDefinition _neutralConflict = null!;
    GeneratedScreenplayDefinition _neutralCapture = null!;

    void Because()
    {
        var adapter = new AdapterIdentity { Id = "enum-vector", Version = "1.0.0" };
        var subject = new SubjectId { Value = "dotnet://Ordering/Ordering.Status" };
        var evidence = new Evidence
        {
            Adapter = adapter,
            Strength = EvidenceStrength.Exact,
            Source = new SourceRange { Path = "Ordering/Status.cs", StartLine = 1, StartColumn = 1, EndLine = 1, EndColumn = 38 }
        };
        var enumeration = LegacyEnumeration(adapter, subject, evidence);
        var competitor = CompetingConcept();
        var legacyMembers = CapturingMembers(false);
        var options = new ScreenplayGenerationOptions { Domain = "Ordering" };
        var generator = new ScreenplayDefinitionGenerator();
        _legacyConflict = generator.Generate(EnumConceptFacts.WithoutConflictingNames([enumeration, competitor]), options);
        _legacyCapture = generator.Generate(EnumConceptFacts.WithoutCapturedNames([enumeration, legacyMembers]), options);
        var diagnostic = Omission(_legacyConflict);
        var yielding = YieldingEnumeration(adapter, subject, evidence, diagnostic);
        _neutralConflict = generator.Generate([yielding, competitor], options);
        _neutralCapture = generator.Generate([yielding, CapturingMembers(true)], options);
    }

    [Fact] void should_verify_the_legacy_concept_conflict() => _legacyConflict.IsSuccess.ShouldBeTrue();
    [Fact] void should_verify_the_legacy_unbound_name_capture() => _legacyCapture.IsSuccess.ShouldBeTrue();
    [Fact] void should_verify_the_neutral_concept_conflict() => _neutralConflict.Diagnostics.Where(_ => _.Severity == GenerationDiagnosticSeverity.Error).Select(_ => $"{_.Code}: {_.Message}").ShouldBeEmpty();
    [Fact] void should_verify_the_neutral_unbound_name_capture() => _neutralCapture.Diagnostics.Where(_ => _.Severity == GenerationDiagnosticSeverity.Error).Select(_ => $"{_.Code}: {_.Message}").ShouldBeEmpty();
    [Fact] void should_keep_the_concept_conflict_source() => _neutralConflict.Source.ShouldEqual(_legacyConflict.Source);
    [Fact] void should_keep_the_unbound_name_capture_source() => _neutralCapture.Source.ShouldEqual(_legacyCapture.Source);
    [Fact] void should_characterize_the_distinct_compatibility_messages() => Omission(_legacyCapture).Message.ShouldNotEqual(Omission(_legacyConflict).Message);
    [Fact] void should_keep_the_configured_message_for_a_concept_conflict() => Omission(_neutralConflict).Message.ShouldEqual(Omission(_legacyConflict).Message);
    [Fact] void should_characterize_the_same_configured_message_for_name_capture() => Omission(_neutralCapture).Message.ShouldEqual(Omission(_neutralConflict).Message);
    [Fact] void should_characterize_the_name_capture_message_parity_gap() => Omission(_neutralCapture).Message.ShouldNotEqual(Omission(_legacyCapture).Message);
    [Fact] void should_keep_the_conflict_outcome_for_both_reasons() => Omission(_neutralCapture).Outcome.ShouldEqual(GenerationDiagnosticOutcome.Conflict);
    [Fact] void should_keep_the_diagnostic_location_for_both_reasons() => Omission(_neutralCapture).Source.ShouldEqual(Omission(_legacyCapture).Source);

    static AdapterContribution LegacyEnumeration(AdapterIdentity adapter, SubjectId subject, Evidence evidence) => new()
    {
        Adapter = adapter,
        Facts =
        [
            new ArtifactFact
            {
                Id = new FactId { Value = $"{adapter.Id}:concept:{subject.Value}" },
                Subject = subject,
                Evidence = evidence,
                Definition = new ArtifactDefinition { Key = new ArtifactKey { Subject = subject, Kind = ArtifactKind.Concept }, Name = "Status" }
            },
            Representation(adapter, subject, evidence)
        ]
    };

    static AdapterContribution YieldingEnumeration(AdapterIdentity adapter, SubjectId subject, Evidence evidence, GenerationDiagnostic diagnostic) => new()
    {
        Adapter = adapter,
        Facts =
        [
            new ArtifactDeclarationFact
            {
                Id = new FactId { Value = $"{adapter.Id}:concept:{subject.Value}" },
                Subject = subject,
                Evidence = evidence,
                Definition = new ArtifactDeclarationDefinition
                {
                    Artifact = new ArtifactKey { Subject = subject, Kind = ArtifactKind.Concept },
                    Name = "Status",
                    NameConflictYield = new ConceptNameYieldDefinition { Diagnostic = diagnostic with { Facts = [new FactId { Value = $"{adapter.Id}:concept:{subject.Value}" }] } }
                }
            },
            Representation(adapter, subject, evidence)
        ]
    };

    static ConceptRepresentationFact Representation(AdapterIdentity adapter, SubjectId subject, Evidence evidence) => new()
    {
        Id = new FactId { Value = $"{adapter.Id}:concept-representation:{subject.Value}" },
        Subject = subject,
        Evidence = evidence,
        Definition = new ConceptRepresentationDefinition { Concept = subject, Kind = ConceptRepresentationKind.Enumeration, EnumerationValues = ["Draft", "Placed"] }
    };

    static AdapterContribution CompetingConcept()
    {
        var adapter = new AdapterIdentity { Id = "independent-concept", Version = "1.0.0" };
        var subject = new SubjectId { Value = "dotnet://Tracking/Tracking.Status" };
        var evidence = new Evidence { Adapter = adapter, Strength = EvidenceStrength.Exact };
        return new AdapterContribution
        {
            Adapter = adapter,
            Facts =
            [
                new ArtifactFact
                {
                    Id = new FactId { Value = "independent-concept:declaration" },
                    Subject = subject,
                    Evidence = evidence,
                    Definition = new ArtifactDefinition { Key = new ArtifactKey { Subject = subject, Kind = ArtifactKind.Concept }, Name = "Status" }
                },
                new ConceptRepresentationFact
                {
                    Id = new FactId { Value = "independent-concept:representation" },
                    Subject = subject,
                    Evidence = evidence,
                    Definition = new ConceptRepresentationDefinition { Concept = subject, Kind = ConceptRepresentationKind.Primitive, Primitive = GenerationPrimitiveKind.Uuid }
                }
            ]
        };
    }

    static AdapterContribution CapturingMembers(bool granular)
    {
        var adapter = new AdapterIdentity { Id = "independent-command", Version = "1.0.0" };
        var subject = new SubjectId { Value = "dotnet://Ordering/Ordering.PlaceOrder" };
        var key = new ArtifactKey { Subject = subject, Kind = ArtifactKind.Command };
        var member = new ArtifactMemberKey { Artifact = key, Name = "externalStatus" };
        var evidence = new Evidence { Adapter = adapter, Strength = EvidenceStrength.Exact };
        var id = new FactId { Value = "independent-command:declaration" };
        return new AdapterContribution
        {
            Adapter = adapter,
            Facts =
            [
                .. granular ? new GenerationFact[]
                {
                    new ArtifactDeclarationFact { Id = id, Subject = subject, Evidence = evidence, Definition = new ArtifactDeclarationDefinition { Artifact = key, Name = "PlaceOrder" } },
                    new ArtifactMemberDeclarationFact
                    {
                        Id = new FactId { Value = "independent-command:member" }, Subject = subject, Evidence = evidence,
                        Definition = new ArtifactMemberDeclarationDefinition { Member = member, DeclarationOrder = 0 }
                    },
                    new ArtifactMemberTypeUseFact
                    {
                        Id = new FactId { Value = "independent-command:type" }, Subject = subject, Evidence = evidence,
                        Definition = new ArtifactMemberTypeUseDefinition { Member = member, Type = new TypeUseDefinition { Name = "Status", Shape = [TypeUseShapeKind.Named] } }
                    }
                } :
                [
                    new ArtifactFact
                    {
                        Id = id, Subject = subject, Evidence = evidence,
                        Definition = new ArtifactDefinition { Key = key, Name = "PlaceOrder", Properties = [new PropertyDefinition { Name = "externalStatus", Type = new TypeReferenceDefinition { Name = "Status" } }] }
                    }
                ],
                new ArtifactPlacementFact
                {
                    Id = new FactId { Value = "independent-command:placement" }, Subject = subject, Artifact = key, Evidence = evidence,
                    Placement = new ArtifactPlacement { Module = "Ordering", Features = ["PlaceOrder"], Slice = "PlaceOrder", SliceKind = GenerationSliceKind.StateChange }
                }
            ]
        };
    }

    static GenerationDiagnostic Omission(GeneratedScreenplayDefinition result) => result.Diagnostics.Single(_ => _.Code == CritterStackDiagnosticCodes.EnumConceptNameConflict);
}
