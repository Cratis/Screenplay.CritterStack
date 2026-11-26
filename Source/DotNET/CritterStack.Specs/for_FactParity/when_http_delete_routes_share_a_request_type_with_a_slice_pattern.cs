// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Cratis.CritterStack.Screenplay.for_FactParity;

/// <summary>Two delete routes sharing a request type where only the second declares a SlicePattern: placement and classification stay per route, exactly as the oracle.</summary>
public class when_http_delete_routes_share_a_request_type_with_a_slice_pattern : given.a_document_delete_http_command_comparison
{
    const string Application =
        """
        namespace Wolverine.Persistence.EventSourcing
        {
            [System.AttributeUsage(System.AttributeTargets.Method | System.AttributeTargets.Class, Inherited = true)]
            public sealed class SlicePatternAttribute(JasperFx.Events.EventModeling.SlicePattern pattern) : System.Attribute
            {
                public JasperFx.Events.EventModeling.SlicePattern Pattern { get; } = pattern;
            }
        }
        namespace JasperFx.Events.EventModeling
        {
            public enum SlicePattern { Command, View, Automation, Translation }
        }
        namespace FactParity
        {
            public sealed record Document(System.Guid Id);
            public sealed record RemoveDocument(System.Guid Id);
            public static class RemoveDocumentEndpoints
            {
                [Wolverine.Http.WolverineDelete("/documents")]
                public static void Delete(RemoveDocument command, Marten.IDocumentSession session) => session.Delete<Document>(command.Id);

                [Wolverine.Http.WolverineDelete("/documents/again"), Wolverine.Persistence.EventSourcing.SlicePattern(JasperFx.Events.EventModeling.SlicePattern.Command)]
                public static void DeleteAgain(RemoveDocument command, Marten.IDocumentSession session) => session.Delete<Document>(command.Id);
            }
        }
        """;

    void Because() => CompareWithMarten(Application);

    [Fact] void should_emit_the_shared_command_once() => Commands(PublicComposed).Count().ShouldEqual(1);
    [Fact] void should_not_report_an_omitted_slice_pattern() => Native.Diagnostics.ShouldNotContain(_ => _.Code == WolverineDiagnosticCodes.SlicePatternOmitted);
    [Fact] void should_not_repeat_any_fact_id() => Native.Facts.GroupBy(_ => _.Id).All(_ => _.Count() == 1).ShouldBeTrue();
    [Fact] void should_emit_both_entry_points() => Native.Facts.OfType<EntryPointFact>().Count().ShouldEqual(2);
    [Fact] void should_be_admitted_by_the_real_runner() => Composites.All(Admitted).ShouldBeTrue();
    [Fact] void should_match_artifacts_in_every_order() => AllArtifactsMatch().ShouldBeTrue();
    [Fact] void should_match_relationships_in_every_order() => AllRelationshipsMatch().ShouldBeTrue();
    [Fact] void should_match_full_diagnostics_in_every_order() => AllDiagnosticsMatch().ShouldBeTrue();
    [Fact] void should_match_printed_source_in_every_order() => AllSourcesMatch().ShouldBeTrue();
    [Fact] void should_match_status_in_every_order() => AllStatusesMatch().ShouldBeTrue();
    [Fact] void should_be_admitted_by_the_real_descriptor() => WasAdmitted().ShouldBeTrue();
}
