// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Cratis.CritterStack.Screenplay.for_FactParity;

/// <summary>Projected Store/Insert/Update/Delete/DeleteWhere beside ordinary session use of the same documents, and document member shapes.</summary>
public class when_marten_projections_and_sessions_use_documents : given.a_marten_document_effects_comparison
{
    const string Application =
        """
        #nullable enable
        namespace FactParity
        {
            public sealed record Ledger(int Id);
            public sealed record Entry(int Id);
            public sealed record Balance(int Id);
            public sealed record Voided(int Id);
            public sealed record Part(int Id);
            public sealed record Shapes(
                int Id,
                int[] Array,
                int[][] Jagged,
                System.Collections.Generic.List<System.Collections.Generic.List<int>> Nested,
                System.Collections.Generic.IEnumerable<int?> Sequence,
                int?[] NullableElements,
                int? Count,
                System.DateTime? When,
                System.Collections.Generic.Dictionary<string, int> Map,
                System.Collections.Generic.List<Part>? Parts,
                string? Note);
            public sealed record NullableIdentity(int? Id, string Name);
            public sealed record CollectionIdentity(int[] Id);
            public sealed class PostingProjection : Marten.Events.Projections.EventProjection
            {
                public override System.Threading.Tasks.ValueTask ApplyAsync(Marten.IDocumentOperations operations, object e, System.Threading.CancellationToken token)
                {
                    operations.Store(new Ledger(1));
                    operations.Insert(new Entry(1));
                    operations.Update(new Entry(2));
                    operations.Delete<Balance>(1);
                    operations.Delete(new Voided(1));
                    operations.DeleteWhere<Entry>(_ => _.Id > 5);
                    return default;
                }
            }
            public static class Configuration
            {
                public static void Configure(Marten.StoreOptions options) => options.Projections.Add<PostingProjection>();
            }
            public static class Posting
            {
                public static void Run(Marten.IDocumentSession session)
                {
                    session.Store(new Ledger(2));
                    session.Update(new Entry(3));
                    session.Insert(new Balance(1));
                    session.Store(new Shapes(1, [], [], [], [], [], null, null, new(), null, null));
                    session.Store(new NullableIdentity(null, "a"));
                    session.Store(new CollectionIdentity([]));
                }
            }
        }
        """;

    void Because() => Compare(Application);

    [Fact] void should_preserve_every_artifact_definition() => Artifacts(Native).ShouldEqual(Artifacts(Expected));
    [Fact] void should_preserve_every_artifact_definition_in_reverse_order() => Artifacts(Reverse).ShouldEqual(Artifacts(Expected));
    [Fact] void should_preserve_every_relationship_definition() => Relationships(Native).ShouldEqual(Relationships(Expected));
    [Fact] void should_preserve_every_relationship_definition_in_reverse_order() => Relationships(Reverse).ShouldEqual(Relationships(Expected));
    [Fact] void should_preserve_complete_diagnostics() => Diagnostics(Native).ShouldEqual(Diagnostics(Expected));
    [Fact] void should_preserve_complete_diagnostics_in_reverse_order() => Diagnostics(Reverse).ShouldEqual(Diagnostics(Expected));
    [Fact] void should_preserve_printed_source() => Native.Source.ShouldEqual(Expected.Source);
    [Fact] void should_preserve_printed_source_in_reverse_order() => Reverse.Source.ShouldEqual(Expected.Source);
    [Fact] void should_preserve_verification_status() => Native.IsSuccess.ShouldEqual(Expected.IsSuccess);
    [Fact] void should_preserve_verification_status_in_reverse_order() => Reverse.IsSuccess.ShouldEqual(Expected.IsSuccess);
    [Fact] void should_not_emit_compatibility_document_artifacts() => Contribution.Facts.OfType<ArtifactFact>().Where(fact => fact.Definition.Key.Kind != ArtifactKind.Concept).ShouldBeEmpty();
}
