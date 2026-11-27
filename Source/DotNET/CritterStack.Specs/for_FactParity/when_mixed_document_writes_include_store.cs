// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Cratis.CritterStack.Screenplay.for_FactParity;

/// <summary>A direct store can accompany a typed delete in a conventional command.</summary>
public class when_mixed_document_writes_include_store : given.a_document_delete_handler_comparison
{
    const string Application =
        """
        namespace FactParity
        {
            public sealed record Document(System.Guid Id);
            public sealed record RemoveAndStore(System.Guid Id);
            public static class StoreHandler
            {
                public static void Handle(RemoveAndStore command, Marten.IDocumentSession session)
                {
                    session.Delete<Document>(command.Id);
                    session.Store(new Document(command.Id));
                }
            }
        }
        """;

    void Because() => CompareHandlers(Application);

    [Fact]
    void should_preserve_the_oracle_command_in_every_admitted_composition()
    {
        Commands(Expected).Single().Name.ShouldEqual("RemoveAndStore");
        AllAdmitted().ShouldBeTrue();
        foreach (var actual in Composites)
        {
            Commands(actual).Select(command => command.Name).ShouldEqual(["RemoveAndStore"]);
        }
    }

    [Fact]
    void should_leave_the_operations_to_marten()
    {
        var method = MethodSubject("StoreHandler");
        var document = DocumentSubject("Document");
        var operations = new[] { Operation(method, RelationshipKind.Deletes, document), Operation(method, RelationshipKind.Stores, document) };
        Outgoing(Oracle, method).ShouldEqual(operations);
        Outgoing(NativeMarten, method).ShouldEqual(operations);
        Declared(NativeMarten, ArtifactKind.Handler).Distinct().Single().Subject.ShouldEqual(method);
        Outgoing(Native, DocumentSubject("RemoveAndStore")).ShouldEqual([Operation(DocumentSubject("RemoveAndStore"), RelationshipKind.Deletes, document)]);
        EntryPoints.Single().Definition.Method.ShouldEqual(method);
    }

    [Fact] void should_match_artifacts_in_all_compositions() => AllArtifactsMatch().ShouldBeTrue();
    [Fact] void should_match_relationships_in_all_compositions() => AllRelationshipsMatch().ShouldBeTrue();
    [Fact] void should_match_full_diagnostics_in_all_compositions() => AssertDiagnosticsMatch();
    [Fact] void should_match_printed_source_in_all_compositions() => AllSourcesMatch().ShouldBeTrue();
    [Fact] void should_match_status_in_all_compositions() => AllStatusesMatch().ShouldBeTrue();
}
