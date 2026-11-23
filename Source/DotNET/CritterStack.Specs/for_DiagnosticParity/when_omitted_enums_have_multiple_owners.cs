// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Cratis.CritterStack.Screenplay.for_DiagnosticParity;

/// <summary>
/// Every non-conflict enum omission keeps one original warning when independently observed twice.
/// Name-conflict yielding is characterized separately in for_AtomicTypeUseParity.
/// </summary>
public class when_omitted_enums_have_multiple_owners : given.an_enum_application
{
    readonly List<(GeneratedScreenplayDefinition Compatibility, GeneratedScreenplayDefinition Forward, GeneratedScreenplayDefinition Reverse)> _vectors = [];

    void Because()
    {
        foreach (var declaration in new[]
        {
            "[System.Flags] public enum Priority { First = 1, Second = 2 }",
            "public enum Priority { }",
            "public enum Priority { First, first }",
            "public enum Priority { First = 1, Second = 1 }",
            "public enum Priority : long { First = 2147483648 }"
        })
        {
            var project = CreateProject(declaration: declaration);
            var compatibility = Generate(project);
            var first = Contribution(project, compatibility, "first-enum-owner");
            var second = Contribution(project, compatibility, "second-enum-owner");
            _vectors.Add((compatibility, GenerateNeutral(first, second), GenerateNeutral(second, first)));
        }
    }

    [Fact] void should_exercise_all_non_conflict_omission_paths() => _vectors.Count.ShouldEqual(5);
    [Fact] void should_keep_every_compatibility_candidate_verified() => _vectors.TrueForAll(vector => vector.Compatibility.IsSuccess).ShouldBeTrue();
    [Fact] void should_keep_every_neutral_candidate_verified() => _vectors.TrueForAll(vector => vector.Forward.IsSuccess && vector.Reverse.IsSuccess).ShouldBeTrue();

    [Fact]
    void should_preserve_each_warning_once_with_its_full_payload()
    {
        foreach (var vector in _vectors)
        {
            vector.Compatibility.Diagnostics.Count(diagnostic => diagnostic.Code.StartsWith("CRITTERSTACK", StringComparison.Ordinal)).ShouldEqual(1);
            EnumDiagnostics(vector.Forward).ShouldEqual(EnumDiagnostics(vector.Compatibility));
            EnumDiagnostics(vector.Reverse).ShouldEqual(EnumDiagnostics(vector.Compatibility));
        }
    }

    [Fact]
    void should_preserve_the_unbound_member_and_not_restore_the_omitted_enum()
    {
        foreach (var vector in _vectors)
        {
            vector.Forward.Source.ShouldContain("priority Priority");
            vector.Forward.Graph.Artifacts.Any(artifact => artifact.Key.Kind == ArtifactKind.Concept).ShouldBeFalse();
            vector.Reverse.Source.ShouldEqual(vector.Forward.Source);
        }
    }

    static string[] EnumDiagnostics(GeneratedScreenplayDefinition result) => given.diagnostic_parity.Meaning(
        result.Diagnostics.Where(diagnostic => diagnostic.Code.StartsWith("CRITTERSTACK", StringComparison.Ordinal)));

    static GeneratedScreenplayDefinition GenerateNeutral(params AdapterContribution[] contributions) =>
        new ScreenplayDefinitionGenerator().Generate(contributions, new ScreenplayGenerationOptions { Domain = "Ordering" });

    static AdapterContribution Contribution(DotNetProjectCompilation project, GeneratedScreenplayDefinition compatibility, string owner)
    {
        var adapter = given.diagnostic_parity.Identity(owner);
        var context = new DotNetAnalysisContext([project]);
        var command = project.Compilation.GetTypeByMetadataName("Ordering.PlaceOrder.PlaceOrder");
        var key = new ArtifactKey { Subject = context.SubjectForType(command), Kind = ArtifactKind.Command };
        var evidence = given.diagnostic_parity.Evidence(adapter);
        var members = DotNetTypeUseFacts.Emit(command, key, context, evidence).ToArray();
        var member = members.OfType<ArtifactMemberTypeUseFact>().Single();
        var diagnostic = compatibility.Diagnostics.Single(item => item.Code.StartsWith("CRITTERSTACK", StringComparison.Ordinal));
        return new AdapterContribution
        {
            Adapter = adapter,
            Facts =
            [
                new ArtifactDeclarationFact
                {
                    Id = new FactId { Value = $"{owner}:command" }, Subject = key.Subject, Evidence = evidence,
                    Definition = new ArtifactDeclarationDefinition { Artifact = key, Name = command.Name }
                },
                new ArtifactPlacementFact
                {
                    Id = new FactId { Value = $"{owner}:placement" }, Subject = key.Subject, Artifact = key, Evidence = evidence,
                    Placement = new ArtifactPlacement { Module = "Ordering", Features = ["PlaceOrder"], Slice = "PlaceOrder", SliceKind = GenerationSliceKind.StateChange }
                },
                .. members,
                new TypeUseTargetOmittedFact
                {
                    Id = new FactId { Value = $"{owner}:omission" }, Subject = key.Subject,
                    Evidence = given.diagnostic_parity.Evidence(adapter, diagnostic.Source),
                    Definition = new TypeUseTargetOmittedDefinition { Member = member.Definition.Member, Diagnostic = diagnostic }
                }
            ]
        };
    }
}
