// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Cratis.CritterStack.Screenplay.for_AtomicTypeUseParity;

/// <summary>
/// Characterizes the released neutral type-use fallback gap before enabling atomic emission.
/// Exact observation of an intentionally undeclared enum adds a diagnostic that the compatibility path does not emit.
/// </summary>
public class when_a_member_uses_an_omitted_flags_enum : given.an_enum_application
{
    GeneratedScreenplayDefinition _compatibility = null!;
    ResolvedApplicationGraph _neutral = null!;
    IReadOnlyList<GenerationFact> _facts = null!;

    void Establish() => Project = CreateProject(declaration: "[System.Flags] public enum Priority { First = 1, Second = 2 }");

    void Because()
    {
        _compatibility = Generate(Project);
        var adapter = new AdapterIdentity { Id = "flags-vector", Version = "1.0.0" };
        var command = Project.Compilation.GetTypeByMetadataName("Ordering.PlaceOrder.PlaceOrder")!;
        var context = new DotNetAnalysisContext([Project]);
        var key = new ArtifactKey { Subject = context.SubjectForType(command)!, Kind = ArtifactKind.Command };
        var evidence = new Evidence { Adapter = adapter, Strength = EvidenceStrength.Exact };
        _facts =
        [
            new ArtifactDeclarationFact
            {
                Id = new FactId { Value = "flags-vector:command" },
                Subject = key.Subject,
                Evidence = evidence,
                Definition = new ArtifactDeclarationDefinition { Artifact = key, Name = command.Name }
            },
            .. DotNetTypeUseFacts.Emit(command, key, context, evidence)
        ];
        _neutral = new ScreenplayDefinitionGenerator().Generate(
            [new AdapterContribution { Adapter = adapter, Facts = _facts }],
            new ScreenplayGenerationOptions { Domain = "Ordering" }).Graph;
    }

    [Fact] void should_keep_the_compatibility_candidate_compiler_verified() => _compatibility.IsSuccess.ShouldBeTrue();
    [Fact] void should_keep_the_compatibility_flags_warning() => _compatibility.Diagnostics.Count(_ => _.Code == CritterStackDiagnosticCodes.FlagsEnumOmitted).ShouldEqual(1);
    [Fact] void should_keep_the_compatibility_property_without_a_concept() => _compatibility.Graph.Artifacts.Single(_ => _.Key.Kind == ArtifactKind.Command).Variants.Single().Definition.Properties.Single().Type.Subject.ShouldBeNull();
    [Fact] void should_observe_the_exact_enum_subject_in_the_neutral_facts() => _facts.OfType<ArtifactMemberTypeUseFact>().Single().Definition.Type.ObservedTypeSubject.ShouldEqual(new DotNetAnalysisContext([Project]).SubjectForType(Project.Compilation.GetTypeByMetadataName("Ordering.Priority")!));
    [Fact] void should_not_invent_a_concept_for_the_omitted_flags_enum() => _neutral.Artifacts.Any(_ => _.Key.Kind == ArtifactKind.Concept).ShouldBeFalse();
    [Fact] void should_characterize_the_neutral_omission_of_the_command() => _neutral.Artifacts.Any(_ => _.Key.Kind == ArtifactKind.Command).ShouldBeFalse();
    [Fact] void should_characterize_the_released_neutral_missing_target_diagnostic() => _neutral.Diagnostics.Count(_ => _.Code == GenerationDiagnosticCodes.MissingTypeUseTarget).ShouldEqual(1);
    [Fact] void should_characterize_the_absence_of_that_diagnostic_in_compatibility() => _compatibility.Diagnostics.Any(_ => _.Code == GenerationDiagnosticCodes.MissingTypeUseTarget).ShouldBeFalse();
}
