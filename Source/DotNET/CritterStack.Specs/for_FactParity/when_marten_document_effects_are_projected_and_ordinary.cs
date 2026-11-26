// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Cratis.CritterStack.Screenplay.for_FactParity;

/// <summary>Projected and ordinary observations of one document keep the oracle's selected evidence and diagnostics.</summary>
public class when_marten_document_effects_are_projected_and_ordinary : when_marten_document_effects_are_compared
{
    const string Application =
        """
        namespace FactParity
        {
            public sealed record Ledger(int Id);
            public sealed record Entry(int Id);
            public sealed record Posted(int Id);
            public sealed record Post;
            public sealed class PostingProjection : Marten.Events.Projections.EventProjection
            {
                public override System.Threading.Tasks.ValueTask ApplyAsync(Marten.IDocumentOperations operations, object e, System.Threading.CancellationToken token)
                {
                    operations.Store(new Ledger(1));
                    operations.Store(new Entry(1));
                    return default;
                }
            }
            public static class Configuration
            {
                public static void Configure(Marten.StoreOptions options) => options.Projections.Add<PostingProjection>();
            }
            public static class PostHandler
            {
                public static void Handle(Post message, Marten.IDocumentSession session)
                {
                    session.Store(new Ledger(2));
                    session.Update(new Entry(2));
                }
            }
        }
        """;

    void Because() => Compare(Application);

    [Fact] void should_report_the_same_document_omissions() => Warnings(Native, "GEN0004").ShouldEqual(Warnings(Expected, "GEN0004"));
}
