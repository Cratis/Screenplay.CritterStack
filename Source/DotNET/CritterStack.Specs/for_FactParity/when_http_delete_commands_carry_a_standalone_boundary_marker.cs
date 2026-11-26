// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Cratis.CritterStack.Screenplay.for_FactParity;

/// <summary>A typed-delete endpoint whose parameter carries a standalone BoundaryAggregate marker: the oracle's boundary diagnostic is preserved exactly.</summary>
public class when_http_delete_commands_carry_a_standalone_boundary_marker : given.a_document_delete_http_command_comparison
{
    const string Application =
        """
        namespace JasperFx.Events.Aggregation
        {
            [System.AttributeUsage(System.AttributeTargets.Class)]
            public class BoundaryAggregateAttribute : System.Attribute;
        }
        namespace FactParity
        {
            public sealed record Document(System.Guid Id);
            public sealed record RemoveDocument(System.Guid Id);
            [JasperFx.Events.Aggregation.BoundaryAggregate]
            public sealed class MarkerOnly { public decimal Balance { get; set; } }
            public static class RemoveDocumentEndpoint
            {
                [Wolverine.Http.WolverineDelete("/documents")]
                public static void Delete(RemoveDocument command, MarkerOnly marker, Marten.IDocumentSession session) => session.Delete<Document>(command.Id);
            }
        }
        """;

    void Because() => CompareWithMarten(Application);

    [Fact] void should_emit_the_command() => Commands(PublicComposed).Count().ShouldEqual(1);
    [Fact] void should_report_the_boundary_diagnostic_natively() => Native.Diagnostics.Count(_ => _.Code == WolverineDiagnosticCodes.DcbBoundaryOmitted).ShouldEqual(1);
    [Fact] void should_report_the_boundary_diagnostic_in_the_oracle() => Expected.Diagnostics.Count(_ => _.Code == WolverineDiagnosticCodes.DcbBoundaryOmitted).ShouldEqual(1);
    [Fact] void should_match_artifacts_in_every_order() => AllArtifactsMatch().ShouldBeTrue();
    [Fact] void should_match_relationships_in_every_order() => AllRelationshipsMatch().ShouldBeTrue();
    [Fact] void should_match_full_diagnostics_in_every_order() => AllDiagnosticsMatch().ShouldBeTrue();
    [Fact] void should_match_printed_source_in_every_order() => AllSourcesMatch().ShouldBeTrue();
    [Fact] void should_match_status_in_every_order() => AllStatusesMatch().ShouldBeTrue();
    [Fact] void should_be_admitted_by_the_real_descriptor() => WasAdmitted().ShouldBeTrue();
}
