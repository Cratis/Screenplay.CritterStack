// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Cratis.CritterStack.Screenplay.for_CritterStackScreenplayGenerator;

public class when_fetched_identities_share_member_names : given.a_wolverine_fetch_many_application
{
    GeneratedScreenplayDefinition _result = null!;

    void Establish() => UseApplication(
        """
        using System;
        using System.Threading.Tasks;
        namespace Members;
        public class Account;
        public static class SourceIds { public static readonly Guid Id = Guid.NewGuid(); }
        public static class DestinationIds { public static readonly Guid Id = Guid.NewGuid(); }
        public record Transfer;
        public record Transferred;
        public record Parenthesized(Guid Id);
        public record ParenthesizedAppended;
        public record Spaced(Guid Id);
        public record SpacedAppended;
        public static class TransferHandler
        {
            public static async Task Handle(Transfer command, Marten.IDocumentSession session)
            {
                var streams = await session.Events.FetchManyForWriting<Account>([SourceIds.Id, DestinationIds.Id]);
                streams[0].AppendOne(new Transferred());
            }
        }
        public static class ParenthesizedHandler
        {
            public static async Task Handle(Parenthesized command, Marten.IDocumentSession session)
            {
                var streams = await session.Events.FetchManyForWriting<Account>([command.Id, (command).Id]);
                streams[0].AppendOne(new ParenthesizedAppended());
            }
        }
        public static class SpacedHandler
        {
            public static async Task Handle(Spaced command, Marten.IDocumentSession session)
            {
                var streams = await session.Events.FetchManyForWriting<Account>([command.Id, command . Id]);
                streams[0].AppendOne(new SpacedAppended());
            }
        }
        """);

    void Because() => _result = new CritterStackScreenplayGenerator().Generate([Project], new CritterStackScreenplayOptions { Domain = "Members" });

    [Fact] void should_compile_the_input() => Project.Compilation.GetDiagnostics().Where(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error).ShouldBeEmpty();
    [Fact] void should_keep_appends_through_same_named_fields_of_different_types() => Relationships("Transfer", RelationshipKind.Appends).Count.ShouldEqual(1);
    [Fact] void should_keep_the_event_appended_through_same_named_fields() => _result.Source.ShouldContain("event Transferred");
    [Fact] void should_treat_a_parenthesized_receiver_as_the_same_identity() => Relationships("Parenthesized", RelationshipKind.Appends).ShouldBeEmpty();
    [Fact] void should_treat_differently_spaced_references_as_the_same_identity() => Relationships("Spaced", RelationshipKind.Appends).ShouldBeEmpty();
    [Fact] void should_report_the_duplicate_loss_only_for_repeated_properties() => _result.Diagnostics.Where(diagnostic => diagnostic.Code == WolverineDiagnosticCodes.MultipleStreamMetadataOmitted && diagnostic.Message.Contains("repeated authored identity expressions are not proven distinct", StringComparison.Ordinal)).Select(diagnostic => diagnostic.Subject!.Value.Split('.')[^1]).Distinct().ShouldContainOnly(["Parenthesized", "Spaced"]);

    IReadOnlyList<ResolvedRelationship> Relationships(string command, RelationshipKind kind) => [.. _result.Graph.Relationships.Where(relationship => relationship.Key.Kind == kind && relationship.Key.Source.Value.EndsWith($".{command}", StringComparison.Ordinal))];
}
