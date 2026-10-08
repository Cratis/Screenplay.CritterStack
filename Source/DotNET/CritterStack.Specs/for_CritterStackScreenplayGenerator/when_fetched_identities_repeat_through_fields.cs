// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Cratis.CritterStack.Screenplay.for_CritterStackScreenplayGenerator;

public class when_fetched_identities_repeat_through_fields : given.a_wolverine_fetch_many_application
{
    GeneratedScreenplayDefinition _result = null!;

    void Establish() => UseApplication(
        """
        using System;
        using System.Threading.Tasks;
        namespace Repeats;
        public class Account;
        public struct Holder { public Guid Value; }
        public record ShareEmpty;
        public record EmptyAppended;
        public record ShareInstance;
        public record InstanceAppended;
        public record DistinctFields(Holder First, Holder Second);
        public record DistinctAppended;
        public static class ShareEmptyHandler
        {
            public static async Task Handle(ShareEmpty command, Marten.IDocumentSession session)
            {
                var streams = await session.Events.FetchManyForWriting<Account>([Guid.Empty, Guid.Empty]);
                streams[0].AppendOne(new EmptyAppended());
            }
        }
        public class ShareInstanceHandler
        {
            readonly Guid _id = Guid.NewGuid();

            public async Task Handle(ShareInstance command, Marten.IDocumentSession session)
            {
                var streams = await session.Events.FetchManyForWriting<Account>([_id, _id]);
                streams[1].AppendOne(new InstanceAppended());
            }
        }
        public static class DistinctFieldsHandler
        {
            public static async Task Handle(DistinctFields command, Marten.IDocumentSession session)
            {
                var streams = await session.Events.FetchManyForWriting<Account>([command.First.Value, command.Second.Value]);
                streams[0].AppendOne(new DistinctAppended());
            }
        }
        """);

    void Because() => _result = new CritterStackScreenplayGenerator().Generate([Project], new CritterStackScreenplayOptions { Domain = "Repeats" });

    [Fact] void should_compile_the_input() => Project.Compilation.GetDiagnostics().Where(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error).ShouldBeEmpty();
    [Fact] void should_keep_reads_for_a_repeated_static_field() => Relationships("ShareEmpty", RelationshipKind.Reads).Count.ShouldEqual(2);
    [Fact] void should_not_append_through_a_repeated_static_field() => Relationships("ShareEmpty", RelationshipKind.Appends).ShouldBeEmpty();
    [Fact] void should_not_produce_through_a_repeated_static_field() => Relationships("ShareEmpty", RelationshipKind.Produces).ShouldBeEmpty();
    [Fact] void should_keep_reads_for_a_repeated_instance_field() => Relationships("ShareInstance", RelationshipKind.Reads).Count.ShouldEqual(2);
    [Fact] void should_not_append_through_a_repeated_instance_field() => Relationships("ShareInstance", RelationshipKind.Appends).ShouldBeEmpty();
    [Fact] void should_not_produce_through_a_repeated_instance_field() => Relationships("ShareInstance", RelationshipKind.Produces).ShouldBeEmpty();
    [Fact] void should_not_invent_events_for_repeated_fields() => _result.Graph.Artifacts.Any(artifact => artifact.Key.Kind == ArtifactKind.Event && (string.Equals(artifact.Variants[0].Definition.Name, "EmptyAppended", StringComparison.Ordinal) || string.Equals(artifact.Variants[0].Definition.Name, "InstanceAppended", StringComparison.Ordinal))).ShouldBeFalse();
    [Fact] void should_report_the_duplicate_loss_for_both_handlers() => _result.Diagnostics.Where(diagnostic => diagnostic.Code == WolverineDiagnosticCodes.MultipleStreamMetadataOmitted && diagnostic.Message.Contains("repeated authored identity expressions are not proven distinct", StringComparison.Ordinal)).Select(diagnostic => diagnostic.Subject!.Value.Split('.')[^1]).Distinct().ShouldContainOnly(["ShareEmpty", "ShareInstance"]);
    [Fact] void should_keep_appends_through_fields_of_distinct_receivers() => Relationships("DistinctFields", RelationshipKind.Appends).Count.ShouldEqual(1);

    IReadOnlyList<ResolvedRelationship> Relationships(string command, RelationshipKind kind) => [.. _result.Graph.Relationships.Where(relationship => relationship.Key.Kind == kind && relationship.Key.Source.Value.EndsWith($".{command}", StringComparison.Ordinal))];
}
