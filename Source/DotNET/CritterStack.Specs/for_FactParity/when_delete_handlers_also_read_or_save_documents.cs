// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Cratis.CritterStack.Screenplay.for_FactParity;

/// <summary>Document reads and session saves preserve compatibility parity for a conventional delete handler.</summary>
public class when_delete_handlers_also_read_or_save_documents : given.a_document_delete_handler_comparison
{
    [Theory]
    [InlineData("await session.LoadAsync<Document>(command.Id);", true)]
    [InlineData("session.LoadMany<Document>(command.Id);", false)]
    [InlineData("await session.LoadManyAsync<Document>(command.Id);", true)]
    [InlineData("session.Query<Document>();", false)]
    [InlineData("await session.Query<Document>().Where(item => item.Id == command.Id).ToListAsync();", true)]
    [InlineData("await session.Query<Document>().Where(item => item.Id == command.Id).FirstOrDefaultAsync();", true)]
    [InlineData("await session.Query<Document>().Where(item => item.Id == command.Id).CountAsync();", true)]
    [InlineData("await session.Query<Document>().Where(item => item.Id == command.Id).AnyAsync();", true)]
    [InlineData("await session.Query<Document>().Where(item => item.Id == command.Id).FirstAsync();", true)]
    [InlineData("await session.Query<Document>().Where(item => item.Id == command.Id).SingleOrDefaultAsync();", true)]
    [InlineData("await session.Events.LoadAsync<RecordAdded>(command.Id);", true)]
    [InlineData("session.SaveChanges();", false)]
    [InlineData("session.Load<Document>(command.Id); session.Store(new Document(command.Id));", false)]
    void should_match_compatibility_in_both_adapter_orders(string operation, bool isAsync)
    {
        var returnType = isAsync ? "async System.Threading.Tasks.Task" : "void";
        CompareHandlers($$"""
            namespace FactParity
            {
                using System.Linq;
                using Marten;

                public sealed record Document(System.Guid Id);
                public sealed record DeleteDocument(System.Guid Id);
                public sealed record RecordAdded(System.Guid Id);

                public static class DeleteHandler
                {
                    public static {{returnType}} Handle(DeleteDocument command, Marten.IDocumentSession session)
                    {
                        session.Delete<Document>(command.Id);
                        {{operation}}
                    }
                }
            }
            """);

        ComparedProject.Compilation.GetDiagnostics(TestContext.Current.CancellationToken)
            .Where(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error).ShouldBeEmpty();
        AllAdmitted().ShouldBeTrue();
        AllArtifactsMatch().ShouldBeTrue();
        AllRelationshipsMatch().ShouldBeTrue();
        AssertDiagnosticsMatch();
        AllSourcesMatch().ShouldBeTrue();
        AllStatusesMatch().ShouldBeTrue();
        var request = DocumentSubject("DeleteDocument");
        Declared(Native, ArtifactKind.Command).Select(key => key.Subject).ShouldEqual([request]);
        Outgoing(Native, request).ShouldEqual([Operation(request, RelationshipKind.Deletes, DocumentSubject("Document"))]);
    }
}
