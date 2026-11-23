// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Cratis.CritterStack.Screenplay.for_AtomicTypeUseParity;

/// <summary>
/// Preserves omission parity when an independent unbound type would capture an enum name.
/// </summary>
public class when_an_enum_name_captures_an_independent_unbound_type : Specification
{
    GeneratedScreenplayDefinition _compatibility = null!;
    GeneratedScreenplayDefinition _neutral = null!;
    GeneratedScreenplayDefinition _reversed = null!;
    GeneratedScreenplayDefinition _withoutMembers = null!;
    ArtifactMemberTypeUseFact _externalType = null!;

    void Because()
    {
        var project = CreateProject();
        var context = new DotNetAnalysisContext([project]);
        _compatibility = new CritterStackScreenplayGenerator().Generate([project], new CritterStackScreenplayOptions { Domain = "Ordering" });
        var enumeration = EnumContribution(context, project, _compatibility.Diagnostics.Single(_ => _.Code == CritterStackDiagnosticCodes.EnumConceptNameConflict));
        var members = CommandContribution(context, project);
        _externalType = members.Facts.OfType<ArtifactMemberTypeUseFact>().Single(_ => _.Definition.Member.Name == "externalStatus");
        var options = new ScreenplayGenerationOptions { Domain = "Ordering" };
        var generator = new ScreenplayDefinitionGenerator();
        _neutral = generator.Generate([enumeration, members], options);
        _reversed = generator.Generate([members, enumeration], options);
        _withoutMembers = generator.Generate([enumeration], options);
    }

    [Fact] void should_verify_the_compatibility_candidate() => _compatibility.IsSuccess.ShouldBeTrue();
    [Fact] void should_omit_the_capturing_enum_in_compatibility() => _compatibility.Source.ShouldNotContain("concept Status : Enum");
    [Fact] void should_keep_the_command_and_plain_external_type_in_compatibility() => _compatibility.Source.ShouldContain("externalStatus Status");
    [Fact] void should_preserve_the_located_conflict_warning() => _compatibility.Diagnostics.Single(_ => _.Code == CritterStackDiagnosticCodes.EnumConceptNameConflict).Source.Path.ShouldEqual("Ordering/Application.cs");
    [Fact] void should_preserve_the_conflict_outcome() => _compatibility.Diagnostics.Single(_ => _.Code == CritterStackDiagnosticCodes.EnumConceptNameConflict).Outcome.ShouldEqual(GenerationDiagnosticOutcome.Conflict);
    [Fact] void should_keep_the_metadata_type_unbound() => _externalType.Definition.Type.ObservedTypeSubject.ShouldBeNull();
    [Fact] void should_keep_the_metadata_type_name() => _externalType.Definition.Type.Name.ShouldEqual("Status");
    [Fact] void should_verify_the_neutral_candidate() => _neutral.IsSuccess.ShouldBeTrue();
    [Fact] void should_omit_the_capturing_enum_in_neutral_composition() => _neutral.Source.ShouldNotContain("concept Status : Enum");
    [Fact] void should_keep_the_external_property_without_a_capturing_concept() => _neutral.Source.ShouldContain("externalStatus Status");
    [Fact] void should_preserve_the_compatibility_omission_outcome() => _neutral.Diagnostics.Single(_ => _.Code == CritterStackDiagnosticCodes.EnumConceptNameConflict).Outcome.ShouldEqual(GenerationDiagnosticOutcome.Conflict);
    [Fact] void should_preserve_the_compatibility_omission_location() => _neutral.Diagnostics.Single(_ => _.Code == CritterStackDiagnosticCodes.EnumConceptNameConflict).Source.ShouldEqual(_compatibility.Diagnostics.Single(_ => _.Code == CritterStackDiagnosticCodes.EnumConceptNameConflict).Source);
    [Fact] void should_omit_the_same_enum_in_either_contribution_order() => _reversed.Source.ShouldEqual(_neutral.Source);
    [Fact] void should_keep_the_enum_when_no_independent_members_compete() => _withoutMembers.Source.ShouldContain("concept Status : Enum");

    static DotNetProjectCompilation CreateProject()
    {
        var references = ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")).Split(Path.PathSeparator)
            .Select(path => MetadataReference.CreateFromFile(path)).ToArray();
        var metadata = CSharpCompilation.Create(
            "External",
            [CSharpSyntaxTree.ParseText("namespace External; public sealed class Status;")],
            references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
        using var stream = new MemoryStream();
        metadata.Emit(stream).Success.ShouldBeTrue();
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
                public enum Status { Draft, Placed }
                public sealed record Ledger(int Id);
                public sealed record PlaceOrder(Status Status, External.Status ExternalStatus);
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
            [.. references, MetadataReference.CreateFromImage(stream.ToArray())],
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

    static AdapterContribution EnumContribution(DotNetAnalysisContext context, DotNetProjectCompilation project, GenerationDiagnostic diagnostic)
    {
        var adapter = new AdapterIdentity { Id = "enum-vector", Version = "1.0.0" };
        var enumeration = project.Compilation.GetTypeByMetadataName("Ordering.Status")!;
        var subject = context.SubjectForType(enumeration)!;
        var evidence = CritterStackSource.EvidenceFor(enumeration, adapter, project, EvidenceStrength.Exact, "Authored enum declaration");
        var declarationId = new FactId { Value = "enum-vector:concept" };
        return new AdapterContribution
        {
            Adapter = adapter,
            Facts =
            [
                new ArtifactDeclarationFact
                {
                    Id = declarationId,
                    Subject = subject,
                    Evidence = evidence,
                    Definition = new ArtifactDeclarationDefinition
                    {
                        Artifact = new ArtifactKey { Subject = subject, Kind = ArtifactKind.Concept },
                        Name = enumeration.Name,
                        File = evidence.Source?.Path,
                        NameConflictYield = new ConceptNameYieldDefinition { Diagnostic = diagnostic with { Facts = [declarationId] } }
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
                }
            ]
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
