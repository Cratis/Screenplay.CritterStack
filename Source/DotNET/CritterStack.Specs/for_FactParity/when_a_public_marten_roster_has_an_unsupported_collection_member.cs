// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Cratis.CritterStack.Screenplay.for_FactParity;

/// <summary>
/// A collection of arrays is not a jagged array: the native producer does not request the legacy jagged representation for it,
/// so it keeps reporting the ordinary unrepresentable-shape diagnostic, identically in the composed and the public roster.
/// </summary>
public class when_a_public_marten_roster_has_an_unsupported_collection_member : given.a_marten_document_effects_comparison
{
    const string Application =
        """
        namespace FactParity
        {
            public sealed record Grid(int Id, System.Collections.Generic.List<int[]> Rows);
            public static class Gridding
            {
                public static void Run(Marten.IDocumentSession session) => session.Store(new Grid(1, []));
            }
        }
        """;

    void Because() => Compare(Application);

    [Fact] void should_report_the_unrepresentable_shape_once_when_composed() => Warnings(Native, "GEN0049").ShouldEqual(1);
    [Fact] void should_report_the_unrepresentable_shape_once_in_the_public_roster() => Warnings(Public, "GEN0049").ShouldEqual(1);
    [Fact] void should_not_reject_the_marten_contribution() => Public.Diagnostics.ShouldNotContain(_ => _.Code == "DOTNETADAPTER007");
}
