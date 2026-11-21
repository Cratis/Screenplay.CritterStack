// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Cratis.CritterStack.Screenplay.for_CritterStackScreenplayGenerator;

public class when_generating_standalone_boundary_aggregate_markers : given.a_read_model_application
{
    protected override string ApplicationSource =>
        """
        namespace Other { public class BoundaryAggregateAttribute : System.Attribute; }
        namespace Aggregates
        {
            public record Inspect(System.Guid Id);
            public record InspectAgain(System.Guid Id);
            [JasperFx.Events.Aggregation.BoundaryAggregate]
            public class MarkerOnly { public decimal Balance { get; set; } }
            [Other.BoundaryAggregate]
            public class Unrelated { public decimal Balance { get; set; } }
            public static class MarkerHandler
            {
                public static void Handle(Inspect request, MarkerOnly aggregate, Unrelated other, Generated generated) { }
                public static void Handle(InspectAgain request, MarkerOnly aggregate) { }
            }
        }
        """;

    protected override string GeneratedSource =>
        """
        namespace Aggregates
        {
            [JasperFx.Events.Aggregation.BoundaryAggregate]
            public class Generated { public decimal Balance { get; set; } }
        }
        """;

    GenerationDiagnostic Diagnostic => Contribution.Diagnostics.Single(diagnostic => diagnostic.Code == WolverineDiagnosticCodes.DcbBoundaryOmitted);

    void Because() => Generate();

    [Fact] void should_keep_the_authored_input_compilable() => Project.Compilation.GetDiagnostics(TestContext.Current.CancellationToken).Where(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error).ShouldBeEmpty();
    [Fact] void should_report_only_the_genuine_authored_marker_once() => Contribution.Diagnostics.Count(diagnostic => diagnostic.Code == WolverineDiagnosticCodes.DcbBoundaryOmitted).ShouldEqual(1);
    [Fact] void should_name_the_unrecognized_boundary() => Diagnostic.Message.ShouldContain("standalone BoundaryAggregate marker on 'MarkerOnly'");
    [Fact] void should_locate_the_diagnostic_at_the_marker() => Diagnostic.Source.StartLine.ShouldEqual(6);
    [Fact] void should_locate_the_diagnostic_in_the_authored_file() => Diagnostic.Source.Path.ShouldEqual("Aggregates/Handlers.cs");
    [Fact] void should_classify_the_loss_as_structural() => Diagnostic.Severity.ShouldEqual(GenerationDiagnosticSeverity.Information);
    [Fact] void should_classify_the_boundary_as_unsupported() => Diagnostic.Outcome.ShouldEqual(GenerationDiagnosticOutcome.Unsupported);
    [Fact] void should_not_invent_aggregate_artifacts() => Contribution.Facts.OfType<ArtifactFact>().Where(fact => fact.Definition.Key.Kind == ArtifactKind.Aggregate).ShouldBeEmpty();
    [Fact] void should_not_invent_append_relationships() => Contribution.Facts.OfType<RelationshipFact>().Where(fact => fact.Definition.Key.Kind == RelationshipKind.Appends).ShouldBeEmpty();
}
