// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Cratis.CritterStack.Screenplay.for_AtomicTypeUseParity;

/// <summary>
/// Preserves plain source types through explicit concept-only binding with unbound fallback.
/// </summary>
public class when_a_member_uses_an_unowned_source_type : Specification
{
    GeneratedScreenplayDefinition _compatibility = null!;
    GeneratedScreenplayDefinition _neutral = null!;
    GeneratedScreenplayDefinition _withPolicy = null!;
    ArtifactMemberTypeUseFact _typeUse = null!;
    SubjectId _details = null!;

    void Because()
    {
        var project = CreateProject();
        var context = new DotNetAnalysisContext([project]);
        _compatibility = new CritterStackScreenplayGenerator().Generate([project], new CritterStackScreenplayOptions { Domain = "Ordering" });
        _details = context.SubjectForType(project.Compilation.GetTypeByMetadataName("Ordering.Details")!)!;
        var contribution = CommandContribution(context, project);
        _typeUse = contribution.Facts.OfType<ArtifactMemberTypeUseFact>().Single();
        var generator = new ScreenplayDefinitionGenerator();
        var options = new ScreenplayGenerationOptions { Domain = "Ordering" };
        _neutral = generator.Generate([contribution], options);
        var policy = new TypeUseBindingPolicyFact
        {
            Id = new FactId { Value = "command-vector:concept-only-binding" },
            Subject = _typeUse.Subject,
            Evidence = _typeUse.Evidence,
            Definition = new()
            {
                TypeUse = _typeUse.Id,
                Policy = TypeUseBindingPolicyKind.ConceptOnlyWithUnboundFallback
            }
        };
        _withPolicy = generator.Generate([contribution with { Facts = [.. contribution.Facts, policy] }], options);
    }

    [Fact] void should_verify_the_compatibility_candidate() => _compatibility.IsSuccess.ShouldBeTrue();
    [Fact] void should_keep_the_plain_source_type_in_compatibility() => _compatibility.Source.ShouldContain("details Details");
    [Fact] void should_not_invent_a_concept_for_the_plain_source_type() => _compatibility.Source.ShouldNotContain("concept Details");
    [Fact] void should_not_report_a_missing_target_in_compatibility() => _compatibility.Diagnostics.Any(_ => _.Code == GenerationDiagnosticCodes.MissingTypeUseTarget).ShouldBeFalse();
    [Fact] void should_observe_the_exact_source_type_subject() => _typeUse.Definition.Type.ObservedTypeSubject.ShouldEqual(_details);
    [Fact] void should_preserve_the_source_type_name() => _typeUse.Definition.Type.Name.ShouldEqual("Details");
    [Fact] void should_characterize_the_neutral_missing_target() => _neutral.Diagnostics.Count(_ => _.Code == GenerationDiagnosticCodes.MissingTypeUseTarget).ShouldEqual(1);
    [Fact] void should_characterize_the_strict_neutral_generation_failure() => _neutral.IsSuccess.ShouldBeFalse();
    [Fact] void should_verify_with_the_opted_in_fallback_policy() => _withPolicy.IsSuccess.ShouldBeTrue();
    [Fact] void should_preserve_the_plain_source_type_with_the_policy() => _withPolicy.Source.ShouldContain("details Details");
    [Fact] void should_not_report_a_missing_target_with_the_policy() => _withPolicy.Diagnostics.Any(_ => _.Code == GenerationDiagnosticCodes.MissingTypeUseTarget).ShouldBeFalse();
    [Fact] void should_not_invent_a_concept_with_the_policy() => _withPolicy.Source.ShouldNotContain("concept Details");

    static DotNetProjectCompilation CreateProject()
    {
        var tree = CSharpSyntaxTree.ParseText(
            """
            namespace Wolverine { public sealed class WolverineOptions; }
            namespace Marten
            {
                public sealed class StoreOptions;
                public interface IDocumentSession { void Store<T>(T document); }
            }
            namespace Ordering
            {
                public sealed record Details(string Description);
                public sealed record Ledger(int Id);
                public sealed record PlaceOrder(Details Details);
                public sealed record OrderPlaced(int Value);
                public static class PlaceOrderAggregateHandler
                {
                    public static OrderPlaced Handle(PlaceOrder command, Ledger ledger, Marten.IDocumentSession session)
                    {
                        session.Store(new Ledger(1));
                        return new(1);
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

    static AdapterContribution CommandContribution(DotNetAnalysisContext context, DotNetProjectCompilation project)
    {
        var adapter = new AdapterIdentity { Id = "command-vector", Version = "1.0.0" };
        var command = project.Compilation.GetTypeByMetadataName("Ordering.PlaceOrder")!;
        var key = new ArtifactKey { Subject = context.SubjectForType(command)!, Kind = ArtifactKind.Command };
        var evidence = CritterStackSource.EvidenceFor(command, adapter, project, EvidenceStrength.Exact, "Authored command declaration");
        return new AdapterContribution
        {
            Adapter = adapter,
            Facts =
            [
                new ArtifactDeclarationFact
                {
                    Id = new FactId { Value = "command-vector:command" },
                    Subject = key.Subject,
                    Evidence = evidence,
                    Definition = new ArtifactDeclarationDefinition { Artifact = key, Name = command.Name, File = evidence.Source?.Path }
                },
                new ArtifactPlacementFact
                {
                    Id = new FactId { Value = "command-vector:placement" },
                    Subject = key.Subject,
                    Artifact = key,
                    Evidence = evidence,
                    Placement = new ArtifactPlacement { Module = "Ordering", Features = ["PlaceOrder"], Slice = "PlaceOrder", SliceKind = GenerationSliceKind.StateChange }
                },
                .. DotNetTypeUseFacts.Emit(command, key, context, evidence)
            ]
        };
    }
}
