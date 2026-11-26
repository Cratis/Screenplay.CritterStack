// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Cratis.CritterStack.Screenplay.for_FactParity;

/// <summary>Two delete routes in separate files with identical coordinates, the same request type and the same document: every fact keeps its own identity.</summary>
public class when_http_delete_routes_share_coordinates_across_files : given.a_document_delete_http_command_comparison
{
    const string Types =
        """
        namespace FactParity
        {
            public sealed record Document(System.Guid Id);
            public sealed record RemoveDocument(System.Guid Id);
        }
        """;

    const string FirstFile =
        """
        namespace FactParity
        {
            public static class RemoveFirstEndpoint
            {
                [Wolverine.Http.WolverineDelete("/documents")]
                public static void Delete(RemoveDocument command, Marten.IDocumentSession session) => session.Delete<Document>(command.Id);
            }
        }
        """;

    const string SecondFile =
        """
        namespace FactParity
        {
            public static class RemoveSecondEndpoint
            {
                [Wolverine.Http.WolverineDelete("/documents")]
                public static void Delete(RemoveDocument command, Marten.IDocumentSession session) => session.Delete<Document>(command.Id);
            }
        }
        """;

    void Because() => CompareWithMartenFiles(Types, FirstFile, SecondFile);

    [Fact] void should_emit_the_shared_command_once() => Commands(PublicComposed).Count().ShouldEqual(1);
    [Fact] void should_emit_a_delete_for_each_site() => NativeDeletes.Count().ShouldEqual(2);
    [Fact] void should_not_repeat_any_fact_id() => Native.Facts.GroupBy(_ => _.Id).All(_ => _.Count() == 1).ShouldBeTrue();
    [Fact]
    void should_not_leak_an_absolute_path_into_entry_point_or_delete_fact_ids() =>
        Native.Facts
            .Where(_ => _ is EntryPointFact || (_ is RelationshipFact relationship && relationship.Definition.Key.Kind == RelationshipKind.Deletes))
            .All(_ => !_.Id.Value.Contains("workspace", StringComparison.OrdinalIgnoreCase) && !_.Id.Value.Contains(Path.GetTempPath()))
            .ShouldBeTrue();

    [Fact] void should_emit_both_entry_points() => Native.Facts.OfType<EntryPointFact>().Count().ShouldEqual(2);
    [Fact] void should_be_admitted_by_the_real_runner() => Composites.All(Admitted).ShouldBeTrue();
    [Fact] void should_match_artifacts_in_every_order() => AllArtifactsMatch().ShouldBeTrue();
    [Fact] void should_match_relationships_in_every_order() => AllRelationshipsMatch().ShouldBeTrue();
    [Fact] void should_match_full_diagnostics_in_every_order() => AllDiagnosticsMatch().ShouldBeTrue();
    [Fact] void should_match_printed_source_in_every_order() => AllSourcesMatch().ShouldBeTrue();
    [Fact] void should_match_status_in_every_order() => AllStatusesMatch().ShouldBeTrue();
    [Fact] void should_be_admitted_by_the_real_descriptor() => WasAdmitted().ShouldBeTrue();
}
