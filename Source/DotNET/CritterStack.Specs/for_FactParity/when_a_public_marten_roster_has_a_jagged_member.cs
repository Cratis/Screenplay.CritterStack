// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Cratis.CritterStack.Screenplay.for_FactParity;

/// <summary>
/// A document with an int[][] member is admitted on the public roster. The compatibility oracle's empty element name is itself
/// inadmissible, so exact parity for this shape is parked (blocked-jagged-member-name); native reports GEN0049 and prints the same text.
/// </summary>
public class when_a_public_marten_roster_has_a_jagged_member : given.a_marten_document_effects_comparison
{
    const string Application =
        """
        namespace FactParity
        {
            public sealed record Grid(int Id, int[][] Jagged);
            public static class Gridding
            {
                public static void Run(Marten.IDocumentSession session) => session.Store(new Grid(1, []));
            }
        }
        """;

    void Because() => Compare(Application);

    [Fact] void should_not_reject_the_marten_contribution() => Public.Diagnostics.ShouldNotContain(_ => _.Code == "DOTNETADAPTER007");
    [Fact] void should_report_the_unrepresentable_shape_for_the_nested_collection() => Public.Diagnostics.ShouldContain(_ => _.Code == "GEN0049");
}
