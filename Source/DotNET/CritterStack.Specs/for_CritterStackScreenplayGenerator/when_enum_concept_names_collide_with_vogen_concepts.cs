// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Cratis.CritterStack.Screenplay.for_CritterStackScreenplayGenerator;

public class when_enum_concept_names_collide_with_vogen_concepts : Specification
{
    DotNetProjectCompilation _project = null!;
    GeneratedScreenplayDefinition _result = null!;

    void Establish()
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
                public sealed record Track(Tracking.Status Status);
                public sealed record OrderPlaced(Status Status);
                public static class PlaceOrderAggregateHandler
                {
                    public static OrderPlaced Handle(PlaceOrder command, Ledger ledger, Marten.IDocumentSession session)
                    {
                        session.Store(new Ledger(1));
                        return new(command.Status);
                    }
                }
                public sealed record Tracked(Tracking.Status Status);
                public static class TrackAggregateHandler
                {
                    public static Tracked Handle(Track command, Ledger ledger, Marten.IDocumentSession session)
                    {
                        session.Store(new Ledger(2));
                        return new(command.Status);
                    }
                }
            }
            """,
            path: "/workspace/Ordering/Application.cs");
        _project = new DotNetProjectCompilation
        {
            Name = "Ordering",
            ProjectPath = "/workspace/Ordering/Ordering.csproj",
            SourceRoot = "/workspace",
            Compilation = CSharpCompilation.Create("Ordering", [tree], ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")!).Split(Path.PathSeparator).Select(path => MetadataReference.CreateFromFile(path)), new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary)),
            AuthoredSyntaxTrees = new HashSet<SyntaxTree> { tree }
        };
    }

    void Because() => _result = new CritterStackScreenplayGenerator().Generate([_project], new CritterStackScreenplayOptions { Domain = "Ordering" });

    [Fact] void should_compile_the_input() => _project.Compilation.GetDiagnostics().Where(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error).ShouldBeEmpty();
    [Fact] void should_succeed() => _result.IsSuccess.ShouldBeTrue();
    [Fact] void should_keep_the_vogen_concept() => _result.Source.ShouldContain("concept Status : Uuid");
    [Fact] void should_not_declare_the_enum_concept() => _result.Source.ShouldNotContain("concept Status : Enum");
    [Fact] void should_keep_the_command_using_the_enum() => _result.Source.ShouldContain("command PlaceOrder");
    [Fact] void should_keep_the_command_using_the_vogen_concept() => _result.Source.ShouldContain("command Track");
    [Fact] void should_not_report_conflicting_concept_names() => _result.Diagnostics.Where(diagnostic => diagnostic.Code == GenerationDiagnosticCodes.ConflictingConceptName).ShouldBeEmpty();
    [Fact] void should_not_report_missing_concepts() => _result.Diagnostics.Where(diagnostic => diagnostic.Code == "GEN0016").ShouldBeEmpty();
    [Fact] void should_report_the_enum_as_the_dropped_side() => _result.Diagnostics.Single(diagnostic => diagnostic.Code == CritterStackDiagnosticCodes.EnumConceptNameConflict).Subject!.Value.ShouldNotContain("Tracking");
    [Fact] void should_keep_vogen_provenance_for_the_concept() => _result.Graph.Artifacts.Single(artifact => artifact.Key.Kind == ArtifactKind.Concept && artifact.Variants[0].Definition.Name == "Status").Variants.Single().Evidence.Single().Adapter.Id.ShouldEqual("vogen");
}
