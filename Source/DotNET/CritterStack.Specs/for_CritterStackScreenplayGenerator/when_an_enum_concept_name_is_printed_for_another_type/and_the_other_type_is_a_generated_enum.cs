// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Cratis.CritterStack.Screenplay.for_CritterStackScreenplayGenerator.when_an_enum_concept_name_is_printed_for_another_type;

public class and_the_other_type_is_a_generated_enum : given.an_application_printing_an_enum_name_for_another_type
{
    GeneratedScreenplayDefinition _result = null!;
    GeneratedScreenplayDefinition _direct = null!;

    void Establish() => UseApplication(
        """
        namespace Billing { public enum Status { Open, Closed } }
        namespace Ordering
        {
            public enum Priority { Low, High }
            public sealed record Ledger(int Id);
            public sealed record Bill(Billing.Status Status, Priority Priority);
            public sealed record Billed(Billing.Status Status);
            public static class BillAggregateHandler
            {
                public static Billed Handle(Bill command, Ledger ledger, Marten.IDocumentSession session)
                {
                    session.Store(new Ledger(1));
                    return new(command.Status);
                }
            }
            public sealed record Ship(Shipping.Status Status);
            public sealed record Shipped(Shipping.Status Status);
            public static class ShipAggregateHandler
            {
                public static Shipped Handle(Ship command, Ledger ledger, Marten.IDocumentSession session)
                {
                    session.Store(new Ledger(2));
                    return new(command.Status);
                }
            }
        }
        """,
        generated: "namespace Shipping { public enum Status { Packed, Sent } }");

    void Because()
    {
        _result = new CritterStackScreenplayGenerator().Generate([Project], new CritterStackScreenplayOptions { Domain = "Ordering" });
        _direct = new ScreenplayDefinitionGenerator().Generate([new CritterStackScreenplayAdapter().Analyze(new([Project]), new())], new ScreenplayGenerationOptions { Domain = "Ordering" });
    }

    [Fact] void should_compile_the_input() => Project.Compilation.GetDiagnostics().Where(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error).ShouldBeEmpty();
    [Fact] void should_succeed() => _result.IsSuccess.ShouldBeTrue();
    [Fact] void should_succeed_for_direct_adapter_consumers() => _direct.IsSuccess.ShouldBeTrue();
    [Fact] void should_not_declare_the_authored_enum() => _result.Source.ShouldNotContain("concept Status");
    [Fact] void should_not_declare_the_authored_enum_for_direct_adapter_consumers() => _direct.Source.ShouldNotContain("concept Status");
    [Fact] void should_keep_the_unrelated_enum_concept() => _result.Source.ShouldContain("concept Priority : Enum");
    [Fact] void should_keep_the_unrelated_enum_concept_for_direct_adapter_consumers() => _direct.Source.ShouldContain("concept Priority : Enum");
    [Fact] void should_keep_the_command_using_the_authored_enum() => _result.Source.ShouldContain("command Bill");
    [Fact] void should_keep_the_command_using_the_generated_enum() => _result.Source.ShouldContain("command Ship");
    [Fact] void should_keep_both_commands_for_direct_adapter_consumers() => (_direct.Source.Contains("command Bill") && _direct.Source.Contains("command Ship")).ShouldBeTrue();
    [Fact] void should_report_only_the_dropped_enum() => Conflicts(_result).Single().Subject!.Value.ShouldContain("Billing");
    [Fact] void should_report_only_the_dropped_enum_for_direct_adapter_consumers() => Conflicts(_direct).Single().Subject!.Value.ShouldContain("Billing");
    [Fact] void should_locate_the_conflict() => Conflicts(_result).All(diagnostic => diagnostic.Source?.Path == "Ordering/Application.cs" && diagnostic.Source.StartLine > 0).ShouldBeTrue();
    [Fact] void should_explain_that_a_different_type_uses_the_name() => Conflicts(_result).All(diagnostic => diagnostic.Message.Contains("a different type", StringComparison.Ordinal)).ShouldBeTrue();
    [Fact] void should_not_report_name_collisions_or_missing_concepts() => _result.Diagnostics.Concat(_direct.Diagnostics).Where(diagnostic => string.Equals(diagnostic.Code, "GEN0015", StringComparison.Ordinal) || string.Equals(diagnostic.Code, "GEN0016", StringComparison.Ordinal)).ShouldBeEmpty();

    static IEnumerable<GenerationDiagnostic> Conflicts(GeneratedScreenplayDefinition result) => result.Diagnostics.Where(diagnostic => diagnostic.Code == CritterStackDiagnosticCodes.EnumConceptNameConflict);
}
