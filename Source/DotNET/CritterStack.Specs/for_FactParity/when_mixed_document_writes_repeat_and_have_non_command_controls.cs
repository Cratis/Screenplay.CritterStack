// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Cratis.CritterStack.Screenplay.for_FactParity;

/// <summary>Repeated writes and deletes keep their sites without granting a command to unrelated methods.</summary>
public class when_mixed_document_writes_repeat_and_have_non_command_controls : given.a_document_delete_handler_comparison
{
    const string Application =
        """
        namespace FactParity
        {
            public sealed record Ledger(int Id);
            public sealed record Journal(int Id);
            public sealed record Quote(int Id);
            public sealed record Fake(int Id);
            public sealed record Repeat(int Id);
            public sealed record StoreOnly(int Id);
            public sealed record Failure(int Id);
            public sealed record Dormant(int Id);
            public sealed class OtherOperations
            {
                public void Insert<T>(params T[] documents) { }
            }
            public static class RepeatHandler
            {
                public static void Handle(Repeat message, Marten.IDocumentSession session, OtherOperations other)
                {
                    session.Store(new Journal(1));
                    session.Insert(new Ledger(1));
                    session.Insert(new Ledger(2));
                    session.Update(new Ledger(3));
                    session.Delete<Journal>(2);
                    session.Delete<Journal>(3);
                    session.Delete(new Ledger(4));
                    session.DeleteWhere<Quote>(_ => _.Id > 1);
                    other.Insert(new Fake(0));
                }
            }
            public static class StoreOnlyHandler
            {
                public static void Handle(StoreOnly message, Marten.IDocumentSession session) => session.Store(new Journal(message.Id));
            }
            public static class FailureHandler
            {
                public static void OnException(Failure message, System.Exception exception, Marten.IDocumentSession session) => session.Delete<Journal>(message.Id);
            }
            public static class DormantHandler
            {
                public static void Helper(Dormant message, Marten.IDocumentSession session) => session.Delete<Journal>(message.Id);
            }
        }
        """;

    void Because() => CompareHandlers(Application);

    [Fact]
    void should_declare_only_the_oracle_command_and_its_entry_point()
    {
        Commands(Expected).Single().Name.ShouldEqual("Repeat");
        Declared(Native, ArtifactKind.Command).Single().Subject.ShouldEqual(DocumentSubject("Repeat"));
        EntryPoints.Single().Definition.Method.ShouldEqual(MethodSubject("RepeatHandler"));
    }

    [Fact]
    void should_preserve_each_supported_marten_operation()
    {
        var method = MethodSubject("RepeatHandler");
        var ledger = DocumentSubject("Ledger");
        var journal = DocumentSubject("Journal");
        RelationshipKey[] operations =
        [
            Operation(method, RelationshipKind.Stores, journal),
            Operation(method, RelationshipKind.Stores, ledger),
            Operation(method, RelationshipKind.Stores, ledger),
            Operation(method, RelationshipKind.Updates, ledger),
            Operation(method, RelationshipKind.Deletes, journal),
            Operation(method, RelationshipKind.Deletes, journal),
            Operation(method, RelationshipKind.Deletes, ledger),
            Operation(method, RelationshipKind.Deletes, DocumentSubject("Quote"))
        ];
        AssertOperationOccurrences([.. Outgoing(Oracle, method)], operations);
        AssertOperationOccurrences([.. Outgoing(NativeMarten, method)], operations);
    }

    static void AssertOperationOccurrences(RelationshipKey[] actual, RelationshipKey[] expected)
    {
        actual.Length.ShouldEqual(expected.Length);
        foreach (var operation in expected.GroupBy(key => key))
        {
            actual.Count(key => key == operation.Key).ShouldEqual(operation.Count());
        }
    }

    [Fact]
    void should_emit_only_typed_command_deletes()
    {
        var command = DocumentSubject("Repeat");
        var journal = Operation(command, RelationshipKind.Deletes, DocumentSubject("Journal"));
        var ledger = Operation(command, RelationshipKind.Deletes, DocumentSubject("Ledger"));
        Outgoing(Native, command).ShouldEqual([journal, journal, ledger]);
    }

    [Fact]
    void should_give_each_fact_a_distinct_id_in_its_contribution()
    {
        Native.Facts.Select(fact => fact.Id).Distinct().Count().ShouldEqual(Native.Facts.Count);
        NativeMarten.Facts.Select(fact => fact.Id).Distinct().Count().ShouldEqual(NativeMarten.Facts.Count);
    }

    [Fact] void should_admit_both_producers_in_all_compositions() => AllAdmitted().ShouldBeTrue();
    [Fact] void should_match_artifacts_in_all_compositions() => AllArtifactsMatch().ShouldBeTrue();
    [Fact] void should_match_relationships_in_all_compositions() => AllRelationshipsMatch().ShouldBeTrue();
    [Fact] void should_match_full_diagnostics_in_all_compositions() => AssertDiagnosticsMatch();
    [Fact] void should_match_printed_source_in_all_compositions() => AllSourcesMatch().ShouldBeTrue();
    [Fact] void should_match_status_in_all_compositions() => AllStatusesMatch().ShouldBeTrue();
}
