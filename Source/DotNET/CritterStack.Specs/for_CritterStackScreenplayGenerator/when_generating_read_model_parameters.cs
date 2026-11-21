// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Cratis.CritterStack.Screenplay.for_CritterStackScreenplayGenerator;

public class when_generating_read_model_parameters : given.a_read_model_application
{
    protected override string ApplicationSource =>
        """
        namespace Aggregates;

        public record Inspect(System.Guid Id);
        public record InspectLegacy(System.Guid Id);
        public record InspectDerived(System.Guid Id);
        public record InspectTwice(System.Guid Id);
        public record Adjust(System.Guid Id);
        public record Adjusted(System.Guid Id);
        public class Account { public System.Guid Id { get; set; } }
        public class ReadModelAttribute : System.Attribute;
        public class DerivedReadAttribute : Wolverine.Persistence.EventSourcing.ReadModelAttribute;

        public static class CurrentHandler
        {
            public static void Handle(Inspect request, [Wolverine.Persistence.EventSourcing.ReadModel] Account account) { }
        }
        public static class LegacyHandler
        {
            public static void Handle(InspectLegacy request, [Wolverine.Marten.ReadAggregate] Account account) { }
        }
        public static class DerivedHandler
        {
            public static void Handle(InspectDerived request, [DerivedRead] Account account) { }
        }
        public static class ReorderedHandler
        {
            public static void Handle([DerivedRead] Account account, InspectTwice request,
                [Wolverine.Marten.ReadAggregate] Account other) { }
        }
        public static class AdjustHandler
        {
            public static Adjusted Handle(Adjust request,
                [Wolverine.Persistence.EventSourcing.ReadModel] Account reference,
                [Wolverine.Marten.WriteAggregate] Account aggregate) => new(request.Id);
        }
        public static class SameNameHandler
        {
            public static void Handle(Inspect request, [ReadModel] Account account) { }
        }
        public static class GeneratedAttributeHandler
        {
            public static void Handle(Inspect request, [GeneratedRead] Account account) { }
        }
        public static class ReadOnlyAggregateHandler
        {
            public static InspectLegacy Handle(Inspect request,
                [Wolverine.Persistence.EventSourcing.ReadModel] Account account) => new(request.Id);
        }
        """;

    protected override string GeneratedSource =>
        """
        namespace Aggregates;
        public class GeneratedReadAttribute : Wolverine.Persistence.EventSourcing.ReadModelAttribute;
        public static class GeneratedHandler
        {
            public static void Handle(Inspect request,
                [Wolverine.Persistence.EventSourcing.ReadModel] Account account) { }
        }
        """;

    void Because() => Generate();

    [Fact]
    void should_keep_the_authored_input_compilable() =>
        Project.Compilation.GetDiagnostics(TestContext.Current.CancellationToken).Any(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error).ShouldBeFalse();

    [Fact]
    void should_recognize_current_legacy_and_inherited_read_attributes() =>
        ReadSubjects().ShouldContainOnly(["Current", "Legacy", "Derived", "Reordered", "ReadOnlyAggregate"]);

    [Fact]
    void should_admit_all_read_model_relationships_to_the_resolved_graph() =>
        Result.Graph.Relationships.Count(relationship => relationship.Key.Discriminator?.StartsWith("read-model:", StringComparison.Ordinal) == true).ShouldEqual(7);

    [Fact]
    void should_keep_two_reads_of_the_same_model_distinct() =>
        ReadOnlyRelationships().Count(relationship => relationship.Key.Source.Value.Contains("ReorderedHandler", StringComparison.Ordinal)).ShouldEqual(2);

    [Fact]
    void should_locate_read_evidence_at_the_attribute() =>
        ReadOnlyFacts().All(fact => fact.Evidence.Source?.Path == "Aggregates/Handlers.cs").ShouldBeTrue();

    [Fact]
    void should_exclude_same_named_and_generated_attributes() =>
        ReadSubjects().Any(subject => subject.Contains("Generated", StringComparison.Ordinal)).ShouldBeFalse();

    [Fact]
    void should_not_create_commands_for_read_only_handlers() =>
        Contribution.Facts.OfType<ArtifactFact>().Where(fact => fact.Definition.Key.Kind == ArtifactKind.Command)
            .Select(fact => fact.Definition.Name).ShouldContainOnly(["Adjust"]);

    [Fact]
    void should_keep_the_reference_read_separate_from_a_write_aggregate() =>
        Contribution.Facts.OfType<RelationshipFact>()
.Count(fact => fact.Definition.Key.Kind == RelationshipKind.Reads && fact.Subject == AdjustSubject()).ShouldEqual(2);

    [Fact]
    void should_not_promote_a_read_only_return_to_a_persisted_event() =>
        Contribution.Facts.OfType<ArtifactFact>().Where(fact => fact.Definition.Key.Kind == ArtifactKind.Event)
            .Select(fact => fact.Definition.Name).ShouldContainOnly(["Adjusted"]);

    IEnumerable<RelationshipFact> ReadOnlyFacts() => Contribution.Facts.OfType<RelationshipFact>()
        .Where(fact => fact.Definition.Key.Discriminator?.StartsWith("read-model:", StringComparison.Ordinal) == true);

    IEnumerable<RelationshipDefinition> ReadOnlyRelationships() => ReadOnlyFacts().Select(fact => fact.Definition);

    SubjectId AdjustSubject() => Contribution.Facts.OfType<ArtifactFact>()
.Single(fact => fact.Definition.Key.Kind == ArtifactKind.Command && fact.Definition.Name == "Adjust").Subject;

    string[] ReadSubjects()
    {
        var subjects = ReadOnlyRelationships().Select(relationship => relationship.Key.Source).ToHashSet();
        return [.. Contribution.Facts.OfType<ArtifactFact>()
.Where(fact => fact.Definition.Key.Kind == ArtifactKind.Reaction && subjects.Contains(fact.Subject)).Select(fact => fact.Definition.Name)];
    }
}
