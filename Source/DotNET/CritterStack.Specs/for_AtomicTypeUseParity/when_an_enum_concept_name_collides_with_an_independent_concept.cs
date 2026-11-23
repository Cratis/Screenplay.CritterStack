// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Cratis.CritterStack.Screenplay.for_AtomicTypeUseParity;

/// <summary>
/// Exercises published concept-name yielding and characterizes the remaining diagnostic-outcome parity gap.
/// </summary>
public class when_an_enum_concept_name_collides_with_an_independent_concept : Specification
{
    GeneratedScreenplayDefinition _compatibility = null!;
    GeneratedScreenplayDefinition _neutral = null!;
    GeneratedScreenplayDefinition _reversed = null!;
    GeneratedScreenplayDefinition _withoutVogen = null!;
    GeneratedScreenplayDefinition _neutralWithoutVogen = null!;
    GeneratedScreenplayDefinition _yielding = null!;
    GeneratedScreenplayDefinition _yieldingReversed = null!;
    GeneratedScreenplayDefinition _preservingOutcome = null!;

    void Because()
    {
        var project = CreateProject();
        var context = new DotNetAnalysisContext([project]);
        _compatibility = new CritterStackScreenplayGenerator().Generate([project], new CritterStackScreenplayOptions { Domain = "Ordering" });
        var enumeration = EnumContribution(context, project);
        var vogen = new VogenConceptScreenplayAdapter().Analyze(context, new DotNetAdapterOptions());
        var options = new ScreenplayGenerationOptions { Domain = "Ordering" };
        _neutral = new ScreenplayDefinitionGenerator().Generate([enumeration, vogen], options);
        _reversed = new ScreenplayDefinitionGenerator().Generate([vogen, enumeration], options);
        _withoutVogen = new CritterStackScreenplayGenerator([new CritterStackScreenplayAdapter()]).Generate([project], new CritterStackScreenplayOptions { Domain = "Ordering" });
        _neutralWithoutVogen = new ScreenplayDefinitionGenerator().Generate([enumeration], options);
        var yielding = EnumContribution(context, project, GenerationDiagnosticOutcome.Unsupported);
        _yielding = new ScreenplayDefinitionGenerator().Generate([yielding, vogen], options);
        _yieldingReversed = new ScreenplayDefinitionGenerator().Generate([vogen, yielding], options);
        var preservingOutcome = EnumContribution(context, project, GenerationDiagnosticOutcome.Conflict);
        _preservingOutcome = new ScreenplayDefinitionGenerator().Generate([preservingOutcome, vogen], options);
    }

    [Fact] void should_keep_the_compatibility_candidate_verified() => _compatibility.IsSuccess.ShouldBeTrue();
    [Fact] void should_keep_the_independent_concept_in_compatibility() => _compatibility.Source.ShouldContain("concept Status : Uuid");
    [Fact] void should_keep_the_enum_using_command_in_compatibility() => _compatibility.Source.ShouldContain("command PlaceOrder");
    [Fact] void should_omit_only_the_enum_concept_in_compatibility() => _compatibility.Source.ShouldNotContain("concept Status : Enum");
    [Fact] void should_report_the_located_compatibility_omission_once() => _compatibility.Diagnostics.Single(_ => _.Code == CritterStackDiagnosticCodes.EnumConceptNameConflict).Source.Path.ShouldEqual("Ordering/Application.cs");
    [Fact] void should_characterize_the_neutral_conflict() => _neutral.Diagnostics.Count(_ => _.Code == GenerationDiagnosticCodes.ConflictingConceptName).ShouldEqual(1);
    [Fact] void should_characterize_the_unverified_neutral_candidate() => _neutral.IsSuccess.ShouldBeFalse();
    [Fact] void should_characterize_the_absent_neutral_enum_omission() => _neutral.Diagnostics.Any(_ => _.Code == CritterStackDiagnosticCodes.EnumConceptNameConflict).ShouldBeFalse();
    [Fact] void should_not_resolve_the_gap_by_reversing_contributions() => _reversed.Diagnostics.Count(_ => _.Code == GenerationDiagnosticCodes.ConflictingConceptName).ShouldEqual(1);
    [Fact] void should_keep_the_enum_in_compatibility_when_vogen_is_not_registered() => _withoutVogen.Source.ShouldContain("concept Status : Enum");
    [Fact] void should_verify_the_neutral_facts_when_no_concept_competes() => _neutralWithoutVogen.IsSuccess.ShouldBeTrue();
    [Fact] void should_keep_the_neutral_enum_when_no_concept_competes() => _neutralWithoutVogen.Source.ShouldContain("concept Status : Enum");
    [Fact] void should_verify_the_published_yielding_candidate() => _yielding.IsSuccess.ShouldBeTrue();
    [Fact] void should_keep_the_independent_concept_after_yielding() => _yielding.Source.ShouldContain("concept Status : Uuid");
    [Fact] void should_omit_the_yielding_enum_concept() => _yielding.Source.ShouldNotContain("concept Status : Enum");
    [Fact] void should_keep_the_enum_using_command_after_yielding() => _yielding.Source.ShouldContain("command PlaceOrder");
    [Fact] void should_keep_the_same_yielding_source_in_reverse_order() => _yieldingReversed.Source.ShouldEqual(_yielding.Source);
    [Fact] void should_report_one_yielding_warning() => _yielding.Diagnostics.Count(_ => _.Code == CritterStackDiagnosticCodes.EnumConceptNameConflict).ShouldEqual(1);
    [Fact] void should_preserve_the_omission_source() => Omission(_yielding).Source.ShouldEqual(Omission(_compatibility).Source);
    [Fact] void should_preserve_the_omission_message() => Omission(_yielding).Message.ShouldEqual(Omission(_compatibility).Message);
    [Fact] void should_preserve_the_omission_subject() => Omission(_yielding).Subject.ShouldEqual(Omission(_compatibility).Subject);
    [Fact] void should_preserve_the_omission_severity() => Omission(_yielding).Severity.ShouldEqual(Omission(_compatibility).Severity);
    [Fact] void should_characterize_the_compatibility_conflict_outcome() => Omission(_compatibility).Outcome.ShouldEqual(GenerationDiagnosticOutcome.Conflict);
    [Fact] void should_characterize_the_required_yielding_outcome() => Omission(_yielding).Outcome.ShouldEqual(GenerationDiagnosticOutcome.Unsupported);
    [Fact] void should_not_claim_diagnostic_parity() => Omission(_yielding).Outcome.ShouldNotEqual(Omission(_compatibility).Outcome);
    [Fact] void should_not_yield_with_the_unchanged_compatibility_outcome() => _preservingOutcome.Diagnostics.Count(_ => _.Code == GenerationDiagnosticCodes.ConflictingConceptName).ShouldEqual(1);
    [Fact] void should_not_verify_with_the_unchanged_compatibility_outcome() => _preservingOutcome.IsSuccess.ShouldBeFalse();

    static GenerationDiagnostic Omission(GeneratedScreenplayDefinition result) => result.Diagnostics.Single(_ => _.Code == CritterStackDiagnosticCodes.EnumConceptNameConflict);

    static DotNetProjectCompilation CreateProject()
    {
        var tree = CSharpSyntaxTree.ParseText(
            """
            namespace Vogen { public sealed class ValueObjectAttribute<T> : System.Attribute; }
            namespace Wolverine { public sealed class WolverineOptions; }
            namespace Marten
            {
                public sealed class StoreOptions;
                public interface IDocumentSession { void Store<T>(T document); }
            }
            namespace Tracking { [Vogen.ValueObject<System.Guid>] public partial struct Status; }
            namespace Ordering
            {
                public enum Status { Draft, Placed }
                public sealed record Ledger(int Id);
                public sealed record PlaceOrder(Status Status);
                public sealed record OrderPlaced(Status Status);
                public static class PlaceOrderAggregateHandler
                {
                    public static OrderPlaced Handle(PlaceOrder command, Ledger ledger, Marten.IDocumentSession session)
                    {
                        session.Store(new Ledger(1));
                        return new(command.Status);
                    }
                }
            }
            """,
            path: "/workspace/Ordering/Application.cs");
        var compilation = CSharpCompilation.Create(
            "Ordering",
            [tree],
            ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")).Split(Path.PathSeparator).Select(path => MetadataReference.CreateFromFile(path)),
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
        compilation.GetDiagnostics().Where(_ => _.Severity == DiagnosticSeverity.Error).ShouldBeEmpty();
        return new DotNetProjectCompilation
        {
            Name = "Ordering",
            ProjectPath = "/workspace/Ordering/Ordering.csproj",
            SourceRoot = "/workspace",
            Compilation = compilation,
            AuthoredSyntaxTrees = new HashSet<SyntaxTree> { tree }
        };
    }

    static AdapterContribution EnumContribution(DotNetAnalysisContext context, DotNetProjectCompilation project, GenerationDiagnosticOutcome? yieldOutcome = null)
    {
        var adapter = new AdapterIdentity { Id = "enum-vector", Version = "1.0.0" };
        var enumeration = project.Compilation.GetTypeByMetadataName("Ordering.Status")!;
        var subject = context.SubjectForType(enumeration)!;
        var evidence = CritterStackSource.EvidenceFor(enumeration, adapter, project, EvidenceStrength.Exact, "Authored enum declaration");
        var command = project.Compilation.GetTypeByMetadataName("Ordering.PlaceOrder")!;
        var key = new ArtifactKey { Subject = context.SubjectForType(command)!, Kind = ArtifactKind.Command };
        var commandEvidence = CritterStackSource.EvidenceFor(command, adapter, project, EvidenceStrength.Exact, "Authored command declaration");
        return new AdapterContribution
        {
            Adapter = adapter,
            Facts =
            [
                new ArtifactDeclarationFact
                {
                    Id = new FactId { Value = "enum-vector:concept" },
                    Subject = subject,
                    Evidence = evidence,
                    Definition = new ArtifactDeclarationDefinition
                    {
                        Artifact = new ArtifactKey { Subject = subject, Kind = ArtifactKind.Concept },
                        Name = enumeration.Name,
                        File = evidence.Source?.Path,
                        NameConflictYield = YieldMarker(yieldOutcome, subject, evidence)
                    }
                },
                new ConceptRepresentationFact
                {
                    Id = new FactId { Value = "enum-vector:representation" },
                    Subject = subject,
                    Evidence = evidence,
                    Definition = new ConceptRepresentationDefinition
                    {
                        Concept = subject,
                        Kind = ConceptRepresentationKind.Enumeration,
                        EnumerationValues = ["Draft", "Placed"]
                    }
                },
                new ArtifactDeclarationFact
                {
                    Id = new FactId { Value = "enum-vector:command" },
                    Subject = key.Subject,
                    Evidence = commandEvidence,
                    Definition = new ArtifactDeclarationDefinition { Artifact = key, Name = command.Name, File = commandEvidence.Source?.Path }
                },
                new ArtifactPlacementFact
                {
                    Id = new FactId { Value = "enum-vector:command-placement" },
                    Subject = key.Subject,
                    Artifact = key,
                    Evidence = commandEvidence,
                    Placement = new ArtifactPlacement { Module = "Ordering", Features = ["PlaceOrder"], Slice = "PlaceOrder", SliceKind = GenerationSliceKind.StateChange }
                },
                .. DotNetTypeUseFacts.Emit(command, key, context, commandEvidence)
            ]
        };
    }

    static ConceptNameYieldDefinition? YieldMarker(GenerationDiagnosticOutcome? outcome, SubjectId subject, Evidence evidence) => outcome is null ? null : new()
    {
        Diagnostic = new GenerationDiagnostic
        {
            Code = CritterStackDiagnosticCodes.EnumConceptNameConflict,
            Severity = GenerationDiagnosticSeverity.Warning,
            Outcome = outcome.Value,
            Subject = subject,
            Source = evidence.Source,
            Facts = [new FactId { Value = "enum-vector:concept" }],
            Message = "Enum 'Status' shares its Screenplay concept name with 1 other source subject; it is not declared as a concept and its uses keep the plain type name without a concept reference"
        }
    };
}
