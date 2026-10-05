// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Cratis.CritterStack.Screenplay.for_CritterStackScreenplayGenerator;

public class when_enum_concept_names_collide : Specification
{
    DotNetProjectCompilation _project = null!;
    GeneratedScreenplayDefinition _result = null!;
    GeneratedScreenplayDefinition _direct = null!;

    void Establish()
    {
        var tree = CSharpSyntaxTree.ParseText(
            """
            namespace Wolverine { public sealed class WolverineOptions; }
            namespace Marten
            {
                public sealed class StoreOptions;
                public interface IDocumentSession { void Store<T>(T document); }
            }
            namespace Billing { public enum Stage { Open, Closed } }
            namespace Shipping { public enum Stage { Pending, Sent } }
            namespace Ordering
            {
                public class Order { public enum Status { Draft, Placed } }
                public class Invoice { public enum Status { Due, Paid } }
                public enum Priority { Low, High }
                public sealed record Ledger(int Id);
                public sealed record PlaceOrder(Order.Status Status, Priority Priority);
                public sealed record SendInvoice(Invoice.Status Status);
                public sealed record Bill(Billing.Stage Stage);
                public sealed record Ship(Shipping.Stage Stage);
                public sealed record OrderPlaced(Order.Status Status);
                public static class PlaceOrderAggregateHandler
                {
                    public static OrderPlaced Handle(PlaceOrder command, Ledger ledger, Marten.IDocumentSession session)
                    {
                        session.Store(new Ledger(1));
                        return new(command.Status);
                    }
                }
                public sealed record InvoiceSent(Invoice.Status Status);
                public static class SendInvoiceAggregateHandler
                {
                    public static InvoiceSent Handle(SendInvoice command, Ledger ledger, Marten.IDocumentSession session)
                    {
                        session.Store(new Ledger(2));
                        return new(command.Status);
                    }
                }
                public sealed record Billed(Billing.Stage Stage);
                public static class BillAggregateHandler
                {
                    public static Billed Handle(Bill command, Ledger ledger, Marten.IDocumentSession session)
                    {
                        session.Store(new Ledger(3));
                        return new(command.Stage);
                    }
                }
                public sealed record Shipped(Shipping.Stage Stage);
                public static class ShipAggregateHandler
                {
                    public static Shipped Handle(Ship command, Ledger ledger, Marten.IDocumentSession session)
                    {
                        session.Store(new Ledger(4));
                        return new(command.Stage);
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

    void Because()
    {
        _result = new CritterStackScreenplayGenerator().Generate([_project], new CritterStackScreenplayOptions { Domain = "Ordering" });
        _direct = new ScreenplayDefinitionGenerator().Generate([new CritterStackScreenplayAdapter().Analyze(new([_project]), new())], new ScreenplayGenerationOptions { Domain = "Ordering" });
    }

    [Fact] void should_compile_the_input() => _project.Compilation.GetDiagnostics().Where(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error).ShouldBeEmpty();
    [Fact] void should_succeed() => _result.IsSuccess.ShouldBeTrue();
    [Fact] void should_succeed_for_direct_adapter_consumers() => _direct.IsSuccess.ShouldBeTrue();
    [Fact] void should_keep_the_command_using_a_nested_enum() => _result.Source.ShouldContain("command PlaceOrder");
    [Fact] void should_keep_the_command_using_the_other_nested_enum() => _result.Source.ShouldContain("command SendInvoice");
    [Fact] void should_keep_the_command_using_a_namespaced_enum() => _result.Source.ShouldContain("command Bill");
    [Fact] void should_keep_the_command_using_the_other_namespaced_enum() => _result.Source.ShouldContain("command Ship");
    [Fact] void should_not_report_conflicting_concept_names() => _result.Diagnostics.Where(diagnostic => diagnostic.Code == GenerationDiagnosticCodes.ConflictingConceptName).ShouldBeEmpty();
    [Fact] void should_not_report_missing_concepts() => _result.Diagnostics.Where(diagnostic => diagnostic.Code == "GEN0016").ShouldBeEmpty();
    [Fact] void should_not_report_conflicting_concept_names_for_direct_adapter_consumers() => _direct.Diagnostics.Where(diagnostic => diagnostic.Code == GenerationDiagnosticCodes.ConflictingConceptName).ShouldBeEmpty();
    [Fact] void should_not_report_missing_concepts_for_direct_adapter_consumers() => _direct.Diagnostics.Where(diagnostic => diagnostic.Code == "GEN0016").ShouldBeEmpty();
    [Fact] void should_not_declare_the_colliding_nested_enums() => _result.Source.ShouldNotContain("concept Status");
    [Fact] void should_not_declare_the_colliding_namespaced_enums() => _result.Source.ShouldNotContain("concept Stage");
    [Fact] void should_keep_the_unique_enum_concept() => _result.Source.ShouldContain("concept Priority : Enum");
    [Fact] void should_report_each_colliding_enum() => Collisions.Select(diagnostic => diagnostic.Subject!.Value).Distinct().Count().ShouldEqual(4);
    [Fact] void should_locate_each_collision() => Collisions.All(diagnostic => diagnostic.Source?.Path == "Ordering/Application.cs" && diagnostic.Source.StartLine > 0).ShouldBeTrue();
    [Fact] void should_report_collisions_as_conflicts() => Collisions.All(diagnostic => diagnostic.Outcome == GenerationDiagnosticOutcome.Conflict && diagnostic.Severity == GenerationDiagnosticSeverity.Warning).ShouldBeTrue();

    IEnumerable<GenerationDiagnostic> Collisions => _result.Diagnostics.Where(diagnostic => diagnostic.Code == CritterStackDiagnosticCodes.EnumConceptNameConflict);
}
