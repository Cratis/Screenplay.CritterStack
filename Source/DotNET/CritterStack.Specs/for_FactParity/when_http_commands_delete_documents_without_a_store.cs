// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Cratis.CritterStack.Screenplay.for_FactParity;

/// <summary>Direct typed document deletes from HTTP endpoints without a named store: the command, its Deletes edges and Marten's independent handler edges.</summary>
public class when_http_commands_delete_documents_without_a_store : given.a_document_delete_http_command_comparison
{
    const string Application =
        """
        namespace FactParity
        {
            public sealed record Document(System.Guid Id, string Name);
            public sealed record RemoveDocument(System.Guid Id);
            public static class RemoveDocumentEndpoint
            {
                [Wolverine.Http.WolverineDelete("/documents")]
                public static void Delete(RemoveDocument command, Marten.IDocumentSession session)
                {
                    session.Delete<Document>(command.Id);
                    session.Delete(new Document(command.Id, "Example"));
                }
            }
            public static class RemoveByIdEndpoint
            {
                [Wolverine.Http.WolverineDelete("/documents/{id}")]
                public static void Delete(System.Guid id, Marten.IDocumentSession session) => session.Delete<Document>(id);
            }
        }
        """;

    void Because() => CompareWithMarten(Application);

    [Fact] void should_emit_both_commands() => Commands(PublicComposed).Count().ShouldEqual(2);
    [Fact] void should_emit_native_deletes_for_every_call() => NativeDeletes.Count().ShouldEqual(3);
    [Fact] void should_keep_unscoped_command_ownership() => NativeDeletes.All(_ => _.Definition.Key.SourceScope is null && _.Definition.Key.TargetScope is null).ShouldBeTrue();
    [Fact] void should_keep_marten_method_delete_edges_beside_the_command_edges() => Relationships(Composed).ShouldEqual(Relationships(Expected));
    [Fact] void should_match_artifacts_in_every_order() => AllArtifactsMatch().ShouldBeTrue();
    [Fact] void should_match_relationships_in_every_order() => AllRelationshipsMatch().ShouldBeTrue();
    [Fact] void should_match_full_diagnostics_in_every_order() => AllDiagnosticsMatch().ShouldBeTrue();
    [Fact] void should_match_printed_source_in_every_order() => AllSourcesMatch().ShouldBeTrue();
    [Fact] void should_match_status_in_every_order() => AllStatusesMatch().ShouldBeTrue();
    [Fact] void should_be_admitted_by_the_real_descriptor() => WasAdmitted().ShouldBeTrue();
}
