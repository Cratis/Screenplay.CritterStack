// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Cratis.CritterStack.Screenplay.for_FactParity;

/// <summary>A consistency marker alone does not prevent a delete endpoint from sharing its command with a handler.</summary>
public class when_a_delete_endpoint_and_handler_share_a_request_type : given.a_document_delete_handler_comparison
{
    const string Application =
        """
        namespace Wolverine.Persistence.EventSourcing
        {
            public class DeciderFunctionAttribute : System.Attribute
            {
                public bool AlwaysEnforceConsistency { get; set; }
            }
        }
        namespace Wolverine.Marten
        {
            public class AggregateHandlerAttribute : Wolverine.Persistence.EventSourcing.DeciderFunctionAttribute;
            [System.AttributeUsage(System.AttributeTargets.Class | System.AttributeTargets.Method)]
            public class ConsistentAggregateHandlerAttribute : AggregateHandlerAttribute
            {
                public ConsistentAggregateHandlerAttribute() => AlwaysEnforceConsistency = true;
            }
        }
        namespace FactParity
        {
            public sealed record Document(System.Guid Id);
            public sealed record Remove(System.Guid Id);
            public static class RemoveEndpoint
            {
                [Wolverine.Http.WolverineDelete("/documents")]
                [Wolverine.Marten.ConsistentAggregateHandler]
                public static void Delete(Remove command, Marten.IDocumentSession session) => session.Delete(new Document(command.Id));
            }
            public static class RemoveHandler
            {
                public static void Handle(Remove command, Marten.IDocumentSession session) => session.Delete<Document>(command.Id);
            }
        }
        """;

    void Because() => CompareHandlers(Application);

    [Fact] void should_declare_the_shared_command_once() => Native.Facts.OfType<ArtifactDeclarationFact>().Count().ShouldEqual(1);
    [Fact] void should_emit_both_entry_points() => EntryPoints.Count().ShouldEqual(2);
    [Fact] void should_keep_both_delete_sites() => NativeDeletes.Select(fact => fact.Id).Distinct().Count().ShouldEqual(2);
    [Fact] void should_report_consistency_omitted_in_the_oracle() => Expected.Diagnostics.Count(diagnostic => diagnostic.Code == WolverineDiagnosticCodes.StreamVersionOmitted).ShouldEqual(1);
    [Fact] void should_match_full_diagnostics_in_both_orders() => AssertDiagnosticsMatch();
    [Fact] void should_match_artifacts_in_both_orders() => AllArtifactsMatch().ShouldBeTrue();
    [Fact] void should_match_relationships_in_both_orders() => AllRelationshipsMatch().ShouldBeTrue();
    [Fact] void should_match_printed_source_in_both_orders() => AllSourcesMatch().ShouldBeTrue();
    [Fact] void should_match_status_in_both_orders() => AllStatusesMatch().ShouldBeTrue();
    [Fact] void should_be_admitted_in_both_orders() => AllAdmitted().ShouldBeTrue();
}
