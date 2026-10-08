// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Cratis.CritterStack.Screenplay.for_CritterStackScreenplayGenerator;

public class when_fetched_identities_cannot_be_compared_semantically : given.a_wolverine_fetch_many_application
{
    GeneratedScreenplayDefinition _result = null!;

    void Establish() => UseApplication(
        """
        using System;
        using System.Threading.Tasks;
        namespace Elements;
        public class Account;
        public record RepeatElement(Guid[] Ids);
        public record RepeatedElementAppended;
        public record DistinctElements(Guid[] Ids);
        public record DistinctElementsAppended;
        public record Computed(Guid Id);
        public record ComputedAppended;
        public record SingleComputed;
        public record SingleComputedAppended;
        public static class RepeatElementHandler
        {
            public static async Task Handle(RepeatElement command, Marten.IDocumentSession session)
            {
                var streams = await session.Events.FetchManyForWriting<Account>([command.Ids[0], command.Ids[0]]);
                streams[0].AppendOne(new RepeatedElementAppended());
            }
        }
        public static class DistinctElementsHandler
        {
            public static async Task Handle(DistinctElements command, Marten.IDocumentSession session)
            {
                var streams = await session.Events.FetchManyForWriting<Account>([command.Ids[0], command.Ids[1]]);
                streams[1].AppendOne(new DistinctElementsAppended());
            }
        }
        public static class ComputedHandler
        {
            public static async Task Handle(Computed command, Marten.IDocumentSession session)
            {
                var streams = await session.Events.FetchManyForWriting<Account>([Compute(), command.Id]);
                streams[1].AppendOne(new ComputedAppended());
            }

            static Guid Compute() => Guid.NewGuid();
        }
        public static class SingleComputedHandler
        {
            public static async Task Handle(SingleComputed command, Marten.IDocumentSession session)
            {
                var streams = await session.Events.FetchManyForWriting<Account>([Compute()]);
                streams[0].AppendOne(new SingleComputedAppended());
            }

            static Guid Compute() => Guid.NewGuid();
        }
        """);

    void Because() => _result = new CritterStackScreenplayGenerator().Generate([Project], new CritterStackScreenplayOptions { Domain = "Elements" });

    [Fact] void should_compile_the_input() => Project.Compilation.GetDiagnostics().Where(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error).ShouldBeEmpty();
    [Fact] void should_keep_reads_for_a_repeated_array_element() => Relationships("RepeatElement", RelationshipKind.Reads).Count.ShouldEqual(2);
    [Fact] void should_not_append_through_a_repeated_array_element() => Relationships("RepeatElement", RelationshipKind.Appends).ShouldBeEmpty();
    [Fact] void should_not_produce_through_a_repeated_array_element() => Relationships("RepeatElement", RelationshipKind.Produces).ShouldBeEmpty();
    [Fact] void should_keep_appends_through_distinct_array_elements() => Relationships("DistinctElements", RelationshipKind.Appends).Count.ShouldEqual(1);
    [Fact] void should_keep_reads_for_a_computed_identity() => Relationships("Computed", RelationshipKind.Reads).Count.ShouldEqual(2);
    [Fact] void should_not_append_through_a_computed_identity() => Relationships("Computed", RelationshipKind.Appends).ShouldBeEmpty();
    [Fact] void should_not_produce_through_a_computed_identity() => Relationships("Computed", RelationshipKind.Produces).ShouldBeEmpty();
    [Fact] void should_keep_appends_through_a_single_computed_identity() => Relationships("SingleComputed", RelationshipKind.Appends).Count.ShouldEqual(1);
    [Fact] void should_not_invent_events_for_unproven_batches() => _result.Graph.Artifacts.Any(artifact => artifact.Key.Kind == ArtifactKind.Event && (string.Equals(artifact.Variants[0].Definition.Name, "RepeatedElementAppended", StringComparison.Ordinal) || string.Equals(artifact.Variants[0].Definition.Name, "ComputedAppended", StringComparison.Ordinal))).ShouldBeFalse();
    [Fact] void should_report_the_repeated_loss_for_the_repeated_array_element() => LossesContaining("repeated authored identity expressions are not proven distinct").ShouldContainOnly(["RepeatElement"]);
    [Fact] void should_report_the_uncomparable_loss_for_the_computed_identity() => LossesContaining("authored identity expressions that cannot be compared semantically are not proven distinct").ShouldContainOnly(["Computed"]);
    [Fact] void should_locate_each_loss() => _result.Diagnostics.Where(diagnostic => diagnostic.Code == WolverineDiagnosticCodes.MultipleStreamMetadataOmitted && diagnostic.Message.Contains("not proven distinct", StringComparison.Ordinal)).All(diagnostic => diagnostic.Source?.Path == "Batches/Handlers.cs" && diagnostic.Source.StartLine > 0).ShouldBeTrue();

    IEnumerable<string> LossesContaining(string text) => _result.Diagnostics.Where(diagnostic => diagnostic.Code == WolverineDiagnosticCodes.MultipleStreamMetadataOmitted && diagnostic.Message.Contains(text, StringComparison.Ordinal)).Select(diagnostic => diagnostic.Subject!.Value.Split('.')[^1]).Distinct();
    IReadOnlyList<ResolvedRelationship> Relationships(string command, RelationshipKind kind) => [.. _result.Graph.Relationships.Where(relationship => relationship.Key.Kind == kind && relationship.Key.Source.Value.EndsWith($".{command}", StringComparison.Ordinal))];
}
