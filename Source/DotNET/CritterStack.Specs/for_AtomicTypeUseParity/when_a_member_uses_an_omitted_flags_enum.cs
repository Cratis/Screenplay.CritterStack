// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Cratis.CritterStack.Screenplay.for_AtomicTypeUseParity;

/// <summary>
/// Preserves the compatibility fallback for an intentionally omitted enum through neutral omission facts.
/// </summary>
public class when_a_member_uses_an_omitted_flags_enum : given.an_enum_application
{
    GeneratedScreenplayDefinition _compatibility = null!;
    GeneratedScreenplayDefinition _neutral = null!;
    IReadOnlyList<GenerationFact> _facts = null!;

    void Establish() => Project = CreateProject(declaration: "[System.Flags] public enum Priority { First = 1, Second = 2 }");

    void Because()
    {
        _compatibility = Generate(Project);
        var adapter = new AdapterIdentity { Id = "flags-vector", Version = "1.0.0" };
        var command = Project.Compilation.GetTypeByMetadataName("Ordering.PlaceOrder.PlaceOrder");
        var context = new DotNetAnalysisContext([Project]);
        var key = new ArtifactKey { Subject = context.SubjectForType(command), Kind = ArtifactKind.Command };
        var evidence = new Evidence { Adapter = adapter, Strength = EvidenceStrength.Exact };
        var typeUses = DotNetTypeUseFacts.Emit(command, key, context, evidence).ToArray();
        var memberType = typeUses.OfType<ArtifactMemberTypeUseFact>().Single();
        var enumeration = Project.Compilation.GetTypeByMetadataName("Ordering.Priority");
        var enumEvidence = CritterStackSource.EvidenceFor(enumeration, adapter, Project, EvidenceStrength.Exact, "Authored flags enum");
        _facts =
        [
            new ArtifactDeclarationFact
            {
                Id = new FactId { Value = "flags-vector:command" },
                Subject = key.Subject,
                Evidence = evidence,
                Definition = new ArtifactDeclarationDefinition { Artifact = key, Name = command.Name }
            },
            new ArtifactPlacementFact
            {
                Id = new FactId { Value = "flags-vector:command-placement" },
                Subject = key.Subject,
                Artifact = key,
                Evidence = evidence,
                Placement = new ArtifactPlacement
                {
                    Module = "Ordering",
                    Features = ["PlaceOrder"],
                    Slice = "PlaceOrder",
                    SliceKind = GenerationSliceKind.StateChange
                }
            },
            .. typeUses,
            new TypeUseTargetOmittedFact
            {
                Id = new FactId { Value = "flags-vector:omitted-priority" },
                Subject = memberType.Subject,
                Evidence = enumEvidence,
                Definition = new TypeUseTargetOmittedDefinition
                {
                    Member = memberType.Definition.Member,
                    Diagnostic = new GenerationDiagnostic
                    {
                        Code = CritterStackDiagnosticCodes.FlagsEnumOmitted,
                        Severity = GenerationDiagnosticSeverity.Warning,
                        Outcome = GenerationDiagnosticOutcome.Unsupported,
                        Subject = context.SubjectForType(enumeration),
                        Source = enumEvidence.Source,
                        Message = "Flags enum 'Priority' permits combinations that cannot be represented by an enumeration concept; it is not declared as a concept and its uses keep the plain type name without a concept reference"
                    }
                }
            }
        ];
        _neutral = new ScreenplayDefinitionGenerator().Generate(
            [new AdapterContribution { Adapter = adapter, Facts = _facts }],
            new ScreenplayGenerationOptions { Domain = "Ordering" });
    }

    [Fact] void should_keep_the_compatibility_candidate_compiler_verified() => _compatibility.IsSuccess.ShouldBeTrue();
    [Fact] void should_keep_the_compatibility_flags_warning() => _compatibility.Diagnostics.Count(_ => _.Code == CritterStackDiagnosticCodes.FlagsEnumOmitted).ShouldEqual(1);
    [Fact] void should_keep_the_compatibility_property_without_a_concept() => _compatibility.Graph.Artifacts.Single(_ => _.Key.Kind == ArtifactKind.Command).Variants.Single().Definition.Properties.Single().Type.Subject.ShouldBeNull();
    [Fact] void should_observe_the_exact_enum_subject_in_the_neutral_facts() => _facts.OfType<ArtifactMemberTypeUseFact>().Single().Definition.Type.ObservedTypeSubject.ShouldEqual(new DotNetAnalysisContext([Project]).SubjectForType(Project.Compilation.GetTypeByMetadataName("Ordering.Priority")));
    [Fact] void should_keep_the_neutral_candidate_compiler_verified() => _neutral.IsSuccess.ShouldBeTrue();
    [Fact] void should_not_invent_a_concept_for_the_omitted_flags_enum() => _neutral.Graph.Artifacts.Any(_ => _.Key.Kind == ArtifactKind.Concept).ShouldBeFalse();
    [Fact] void should_keep_the_neutral_command_and_its_unbound_property() => _neutral.Graph.Artifacts.Single(_ => _.Key.Kind == ArtifactKind.Command).Variants.Single().Definition.Properties.Single().Type.Subject.ShouldBeNull();
    [Fact] void should_preserve_the_neutral_property_type_name() => _neutral.Graph.Artifacts.Single(_ => _.Key.Kind == ArtifactKind.Command).Variants.Single().Definition.Properties.Single().Type.Name.ShouldEqual("Priority");
    [Fact] void should_report_exactly_the_original_flags_warning() => _neutral.Diagnostics.Single(_ => _.Code == CritterStackDiagnosticCodes.FlagsEnumOmitted).ShouldEqual(_compatibility.Diagnostics.Single(_ => _.Code == CritterStackDiagnosticCodes.FlagsEnumOmitted));
    [Fact] void should_lower_the_neutral_command() => _neutral.Source.ShouldContain("command PlaceOrder");
    [Fact] void should_not_report_a_missing_target() => _neutral.Diagnostics.Any(_ => _.Code == GenerationDiagnosticCodes.MissingTypeUseTarget).ShouldBeFalse();
}
