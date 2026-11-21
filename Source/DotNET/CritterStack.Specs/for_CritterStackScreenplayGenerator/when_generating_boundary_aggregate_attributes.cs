// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Cratis.CritterStack.Screenplay.for_CritterStackScreenplayGenerator;

public class when_generating_boundary_aggregate_attributes : given.a_read_model_application
{
    protected override string ApplicationSource =>
        """
        namespace Wolverine.Persistence.EventSourcing
        {
            public class DcbModelAttribute : System.Attribute;
        }
        namespace JasperFx.Events.Tags
        {
            public sealed class EventTagQuery
            {
                public static EventTagQuery For<TTag>(TTag value) => new();
            }
        }
        namespace Aggregates
        {
            public record Change(System.Guid Id);
            public record Inspect(System.Guid Id);
            [JasperFx.Events.Aggregation.BoundaryAggregate]
            public class Boundary { public decimal Balance { get; set; } }
            [JasperFx.Events.Aggregation.BoundaryAggregate]
            public class MarkerOnly { public decimal Balance { get; set; } }
            public static class ChangeHandler
            {
                public static JasperFx.Events.Tags.EventTagQuery Before(Change request) =>
                    JasperFx.Events.Tags.EventTagQuery.For(request.Id);
                public static void Handle(Change request,
                    [Wolverine.Persistence.EventSourcing.DcbModel] Boundary aggregate) { }
            }
            public static class MarkerHandler
            {
                public static void Handle(Inspect request, MarkerOnly aggregate) { }
            }
        }
        """;

    void Because() => Generate();

    [Fact]
    void should_keep_the_authored_input_compilable() =>
        Project.Compilation.GetDiagnostics(TestContext.Current.CancellationToken).Any(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error).ShouldBeFalse();

    [Fact]
    void should_retain_only_the_existing_dcb_aggregate_recognition() =>
        Contribution.Facts.OfType<ArtifactFact>().Where(fact => fact.Definition.Key.Kind == ArtifactKind.Aggregate)
            .Select(fact => fact.Definition.Name).ShouldContainOnly(["Boundary"]);

    [Fact]
    void should_report_the_unrepresentable_boundary_concurrency() =>
        Contribution.Diagnostics.Any(diagnostic => diagnostic.Code == WolverineDiagnosticCodes.DcbBoundaryOmitted).ShouldBeTrue();

    [Fact]
    void should_not_invent_a_single_stream_identifier_for_an_identityless_boundary() =>
        Contribution.Facts.OfType<ArtifactFact>().Where(fact => fact.Definition.Key.Kind == ArtifactKind.Aggregate)
            .SelectMany(fact => fact.Definition.Properties).Any(property => property.IsIdentifier).ShouldBeFalse();

    [Fact]
    void should_not_treat_the_marker_as_a_write_operation() =>
        Contribution.Facts.OfType<RelationshipFact>().Any(fact => fact.Definition.Key.Kind == RelationshipKind.Appends).ShouldBeFalse();
}
