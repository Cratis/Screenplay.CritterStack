// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Cratis.CritterStack.Screenplay.for_FactParity;

/// <summary>A conventional command handler can delete documents without a named store.</summary>
public class when_conventional_handlers_delete_documents_without_a_store : given.a_document_delete_handler_comparison
{
    const string Application =
        """
        namespace FactParity
        {
            public sealed record Refund(System.Guid Id);
            public sealed record Archive(int Id);
            public sealed record Quote(int Id);
            public sealed record Remove(System.Guid Id);
            public sealed record Fail(System.Guid Id);
            public static class RemoveHandler
            {
                public static void Handle(Remove message, Marten.IDocumentOperations operations)
                {
                    operations.Delete<Refund>(message.Id);
                    operations.Delete(new Archive(1));
                    operations.DeleteWhere<Quote>(_ => _.Id > 3);
                    operations.Delete<Refund>(message.Id);
                }
            }
            public static class FailureOnlyHandler
            {
                public static void OnException(Fail message, System.Exception exception, Marten.IDocumentSession session) => session.Delete<Refund>(message.Id);
            }
            public static class Dormant
            {
                public static void Helper(Remove message, Marten.IDocumentSession session) => session.Delete<Refund>(message.Id);
            }
        }
        """;

    void Because() => CompareHandlers(Application);

    [Fact] void should_declare_only_the_discovered_command() => Commands(PublicComposed).Single().Name.ShouldEqual("Remove");
    [Fact] void should_emit_one_entry_point() => EntryPoints.Count().ShouldEqual(1);
    [Fact] void should_emit_one_command_delete_for_each_typed_id_delete() => NativeDeletes.Count().ShouldEqual(2);
    [Fact] void should_give_repeated_deletes_distinct_fact_ids() => NativeDeletes.Select(fact => fact.Id).Distinct().Count().ShouldEqual(2);
    [Fact] void should_leave_ownership_unscoped() => NativeDeletes.All(fact => fact.Definition.Key.SourceScope is null && fact.Definition.Key.TargetScope is null).ShouldBeTrue();
    [Fact] void should_be_admitted_in_both_orders() => AllAdmitted().ShouldBeTrue();
    [Fact] void should_match_artifacts_in_both_orders() => AllArtifactsMatch().ShouldBeTrue();
    [Fact] void should_match_relationships_in_both_orders() => AllRelationshipsMatch().ShouldBeTrue();
    [Fact] void should_match_full_diagnostics_in_both_orders() => AssertDiagnosticsMatch();
    [Fact] void should_match_printed_source_in_both_orders() => AllSourcesMatch().ShouldBeTrue();
    [Fact] void should_match_status_in_both_orders() => AllStatusesMatch().ShouldBeTrue();
}
