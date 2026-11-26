// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Cratis.CritterStack.Screenplay.for_FactParity;

/// <summary>Two delete routes sharing one request type and backend: the contribution stays admissible and matches the oracle.</summary>
public class when_http_delete_routes_share_a_request_type : given.a_document_delete_http_command_comparison
{
    const string Application =
        """
        namespace FactParity
        {
            public sealed record Document(System.Guid Id);
            public sealed record RemoveDocument(System.Guid Id);
            public static class RemoveDocumentEndpoints
            {
                [Wolverine.Http.WolverineDelete("/documents")]
                public static void Delete(RemoveDocument command, Marten.IDocumentSession session) => session.Delete<Document>(command.Id);

                [Wolverine.Http.WolverineDelete("/documents/again")]
                public static void DeleteAgain(RemoveDocument command, Marten.IDocumentSession session) => session.Delete<Document>(command.Id);
            }
        }
        """;

    void Because() => CompareWithMarten(Application);

    [Fact] void should_emit_the_shared_command_once() => Commands(PublicComposed).Count().ShouldEqual(1);
    [Fact] void should_declare_the_shared_artifact_once() => Native.Facts.OfType<ArtifactDeclarationFact>().Count().ShouldEqual(1);
    [Fact] void should_not_repeat_any_fact_id() => Native.Facts.GroupBy(_ => _.Id).All(_ => _.Count() == 1).ShouldBeTrue();
    [Fact] void should_emit_both_entry_points() => Native.Facts.OfType<EntryPointFact>().Count().ShouldEqual(2);
    [Fact] void should_emit_native_deletes_for_every_call() => NativeDeletes.Count().ShouldEqual(2);
    [Fact] void should_be_admitted_by_the_real_runner() => IsAdmitted.ShouldBeTrue();
    [Fact] void should_match_artifacts_in_every_order() => AllArtifactsMatch().ShouldBeTrue();
    [Fact] void should_match_relationships_in_every_order() => AllRelationshipsMatch().ShouldBeTrue();
    [Fact] void should_match_full_diagnostics_in_every_order() => AllDiagnosticsMatch().ShouldBeTrue();
    [Fact] void should_match_printed_source_in_every_order() => AllSourcesMatch().ShouldBeTrue();
    [Fact] void should_match_status_in_every_order() => AllStatusesMatch().ShouldBeTrue();
    [Fact] void should_be_admitted_by_the_real_descriptor() => WasAdmitted().ShouldBeTrue();
}
