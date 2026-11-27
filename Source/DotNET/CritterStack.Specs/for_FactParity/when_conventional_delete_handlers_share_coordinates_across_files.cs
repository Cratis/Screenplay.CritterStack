// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Cratis.CritterStack.Screenplay.for_FactParity;

/// <summary>Deletes in different source files keep distinct identities even when their spans match.</summary>
public class when_conventional_delete_handlers_share_coordinates_across_files : given.a_document_delete_handler_comparison
{
    const string Types =
        """
        namespace FactParity
        {
            public sealed record Document(System.Guid Id);
            public sealed record Remove(System.Guid Id);
        }
        """;

    const string FirstFile =
        """
        namespace FactParity
        {
            public static class FirstHandler
            {
                public static void Handle(Remove message, Marten.IDocumentSession session) => session.Delete<Document>(message.Id);
            }
        }
        """;

    static readonly string _secondFile = FirstFile.Replace("FirstHandler", "OtherHandler");

    void Because() => CompareHandlerFiles(
        (Types, "/workspace/FactParity/Types.cs"),
        (FirstFile, "/workspace/FactParity/First.cs"),
        (_secondFile, "/workspace/FactParity/Other.cs"));

    [Fact] void should_emit_one_command_for_the_shared_request() => Commands(PublicComposed).Count().ShouldEqual(1);
    [Fact] void should_use_identical_spans_in_both_files() => FirstFile.IndexOf("session.Delete", StringComparison.Ordinal).ShouldEqual(_secondFile.IndexOf("session.Delete", StringComparison.Ordinal));
    [Fact] void should_keep_entry_points_from_both_host_files() => EntryPoints.Select(fact => fact.Evidence.Source?.FileIdentity).Distinct().Count().ShouldEqual(2);
    [Fact] void should_give_each_entry_point_its_own_fact_id() => EntryPoints.Select(fact => fact.Id).Distinct().Count().ShouldEqual(2);
    [Fact] void should_give_each_delete_its_own_fact_id() => NativeDeletes.Select(fact => fact.Id).Distinct().Count().ShouldEqual(2);
    [Fact] void should_not_repeat_any_fact_id() => Native.Facts.GroupBy(fact => fact.Id).All(group => group.Count() == 1).ShouldBeTrue();
    [Fact]
    void should_not_leak_physical_paths_into_fact_ids() => EntryPoints.Select(fact => fact.Id).Concat(NativeDeletes.Select(fact => fact.Id))
        .All(id => !id.Value.Contains("workspace", StringComparison.OrdinalIgnoreCase) && !id.Value.Contains(Path.GetTempPath()))
        .ShouldBeTrue();
    [Fact] void should_be_admitted_in_both_orders() => AllAdmitted().ShouldBeTrue();
    [Fact] void should_match_artifacts_in_both_orders() => AllArtifactsMatch().ShouldBeTrue();
    [Fact] void should_match_relationships_in_both_orders() => AllRelationshipsMatch().ShouldBeTrue();
    [Fact] void should_match_full_diagnostics_in_both_orders() => AssertDiagnosticsMatch();
    [Fact] void should_match_printed_source_in_both_orders() => AllSourcesMatch().ShouldBeTrue();
    [Fact] void should_match_status_in_both_orders() => AllStatusesMatch().ShouldBeTrue();
}
