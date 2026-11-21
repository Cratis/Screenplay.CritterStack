// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Cratis.CritterStack.Screenplay.for_CritterStackScreenplayGenerator;

public class when_generating_natural_key_aggregates : given.a_read_model_application
{
    protected override string ApplicationSource =>
        """
        namespace Aggregates;

        public record ChangeZ(System.Guid Id, string Zulu);
        public record ChangeA(System.Guid Id);
        public record Inspect(System.Guid Id);
        public record ChangeFake(System.Guid Id);
        public record Changed;
        public class NaturalKeyAttribute : System.Attribute;
        public partial class ZAccount
        {
            public System.Guid Id { get; set; }
            [JasperFx.Events.Aggregation.NaturalKey]
            public string Zulu { get; set; } = string.Empty;
            [JasperFx.Events.Aggregation.NaturalKey]
            public string Alpha { get; set; } = string.Empty;
        }
        public class AOrder
        {
            public System.Guid Id { get; set; }
            [JasperFx.Events.Aggregation.NaturalKey]
            public string Number { get; set; } = string.Empty;
        }
        public class Fake
        {
            public System.Guid Id { get; set; }
            [NaturalKey] public string Code { get; set; } = string.Empty;
        }
        public class Unrecognised
        {
            [JasperFx.Events.Aggregation.NaturalKey]
            public string Code { get; set; } = string.Empty;
        }
        public static class ZHandler
        {
            public static Changed Handle(ChangeZ request, [Wolverine.Marten.WriteAggregate] ZAccount aggregate) => new();
        }
        public static class AHandler
        {
            public static Changed Handle(ChangeA request, [Wolverine.Marten.WriteAggregate] AOrder aggregate) => new();
        }
        public static class InspectHandler
        {
            public static void Handle(Inspect request, [Wolverine.Marten.ReadAggregate] ZAccount aggregate) { }
        }
        public static class FakeHandler
        {
            public static Changed Handle(ChangeFake request, [Wolverine.Marten.WriteAggregate] Fake aggregate) => new();
        }
        """;

    protected override string GeneratedSource =>
        """
        namespace Aggregates;
        public partial class ZAccount
        {
            [JasperFx.Events.Aggregation.NaturalKey]
            public string GeneratedKey { get; set; } = string.Empty;
        }
        """;

    void Because() => Generate();

    [Fact] void should_keep_the_authored_input_compilable() =>
        Project.Compilation.GetDiagnostics(TestContext.Current.CancellationToken).Any(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error).ShouldBeFalse();

    [Fact] void should_report_once_per_annotated_property_not_per_handler() => NaturalKeyDiagnostics().Count().ShouldEqual(3);

    [Fact] void should_order_by_aggregate_then_property_declaration() =>
        NaturalKeyDiagnostics().Select(diagnostic => diagnostic.Source!.StartLine).ToArray().ShouldEqual([20, 12, 14]);

    [Fact] void should_locate_diagnostics_at_the_property_attributes() =>
        NaturalKeyDiagnostics().Select(diagnostic => diagnostic.Source!.StartColumn).Distinct().ShouldContainOnly([6]);

    [Fact] void should_classify_omitted_natural_key_identity_as_information() =>
        NaturalKeyDiagnostics().All(diagnostic => diagnostic.Severity == GenerationDiagnosticSeverity.Information).ShouldBeTrue();

    [Fact] void should_ignore_same_named_generated_and_unrecognised_keys() =>
        NaturalKeyDiagnostics().Any(diagnostic => diagnostic.Message.Contains("Code", StringComparison.Ordinal)).ShouldBeFalse();

    [Fact] void should_retain_the_existing_surrogate_command_identifier() =>
        ChangeZ().Definition.Properties.Single(property => property.Name == "id").IsIdentifier.ShouldBeTrue();

    [Fact] void should_not_guess_a_natural_key_identifier() =>
        ChangeZ().Definition.Properties.Single(property => property.Name == "zulu").IsIdentifier.ShouldBeFalse();

    IEnumerable<GenerationDiagnostic> NaturalKeyDiagnostics() => Contribution.Diagnostics
        .Where(diagnostic => diagnostic.Code == WolverineDiagnosticCodes.NaturalKeyIdentityOmitted);

    ArtifactFact ChangeZ() => Contribution.Facts.OfType<ArtifactFact>()
        .Where(fact => fact.Definition.Key.Kind == ArtifactKind.Command)
        .Single(fact => fact.Definition.Name == "ChangeZ");
}
