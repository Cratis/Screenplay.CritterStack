// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Cratis.CritterStack.Screenplay.for_FactParity;

/// <summary>Other document session operations do not prevent native Wolverine from emitting the command and its delete edge.</summary>
public class when_conventional_delete_handlers_mix_document_session_operations : given.a_document_delete_handler_comparison
{
    const string Application =
        """
        namespace FactParity
        {
            public sealed record Document(System.Guid Id);
            public sealed record DeleteAndLoad(System.Guid Id);
            public sealed record DeleteAndSave(System.Guid Id);
            public sealed record DeleteAndStoreInsideLambda(System.Guid Id);
            public sealed record DeleteAndStoreGenerated(System.Guid Id);

            public static class LoadHandler
            {
                public static void Handle(DeleteAndLoad command, Marten.IDocumentSession session)
                {
                    session.Delete<Document>(command.Id);
                    session.Load<Document>(command.Id);
                }
            }

            public static class SaveHandler
            {
                public static async System.Threading.Tasks.Task Handle(DeleteAndSave command, Marten.IDocumentSession session)
                {
                    session.Delete<Document>(command.Id);
                    await session.SaveChangesAsync();
                }
            }

            public static class NestedStoreHandler
            {
                public static void Handle(DeleteAndStoreInsideLambda command, Marten.IDocumentSession session)
                {
                    session.Delete<Document>(command.Id);
                    System.Action store = () => session.Store(new Document(command.Id));
                    store();
                }
            }

            public static class GeneratedStoreHandler
            {
                public static void Handle(DeleteAndStoreGenerated command, Marten.IDocumentSession session)
                {
                    session.Delete<Document>(command.Id);
                    session.Store(new GeneratedDocument(command.Id));
                }
            }
        }
        """;

    const string GeneratedDocument = "namespace FactParity; public sealed record GeneratedDocument(System.Guid Id);";

    void Because() => CompareHandlerFiles(
        (Application, "/workspace/FactParity/Application.cs"),
        (GeneratedDocument, "/workspace/FactParity/GeneratedDocument.g.cs"));

    [Fact]
    void should_declare_all_four_native_commands() => Declared(Native, ArtifactKind.Command).Select(key => key.Subject).ToArray()
        .ShouldContainOnly([DocumentSubject("DeleteAndLoad"), DocumentSubject("DeleteAndSave"), DocumentSubject("DeleteAndStoreInsideLambda"), DocumentSubject("DeleteAndStoreGenerated")]);

    [Theory]
    [InlineData("DeleteAndLoad")]
    [InlineData("DeleteAndSave")]
    [InlineData("DeleteAndStoreInsideLambda")]
    [InlineData("DeleteAndStoreGenerated")]
    void should_emit_only_the_command_delete_edge(string requestName)
    {
        var request = DocumentSubject(requestName);
        Outgoing(Native, request).ShouldEqual([Operation(request, RelationshipKind.Deletes, DocumentSubject("Document"))]);
    }

    [Fact] void should_be_admitted_in_both_orders() => AllAdmitted().ShouldBeTrue();
    [Fact] void should_match_artifacts_in_both_orders() => AllArtifactsMatch().ShouldBeTrue();
    [Fact]
    void should_match_relationships_in_both_orders()
    {
        foreach (var actual in Composites)
        {
            Relationships(actual).ShouldEqual(Relationships(Expected));
        }
    }

    [Fact] void should_match_full_diagnostics_in_both_orders() => AssertDiagnosticsMatch();
    [Fact] void should_match_printed_source_in_both_orders() => AllSourcesMatch().ShouldBeTrue();
    [Fact] void should_match_status_in_both_orders() => AllStatusesMatch().ShouldBeTrue();
}
