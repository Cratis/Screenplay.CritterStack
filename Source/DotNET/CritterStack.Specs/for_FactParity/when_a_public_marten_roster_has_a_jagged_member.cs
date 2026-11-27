// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Cratis.CritterStack.Screenplay.for_FactParity;

/// <summary>
/// A document with an int[][] member reaches exact parity with the compatibility oracle through the opt-in legacy jagged array
/// representation, and is admitted by the public roster without rejecting the Marten contribution.
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

    [Fact] void should_preserve_every_artifact_definition() => Artifacts(Native).ShouldEqual(Artifacts(Expected));
    [Fact] void should_preserve_every_artifact_definition_in_reverse_order() => Artifacts(Reverse).ShouldEqual(Artifacts(Expected));
    [Fact] void should_preserve_every_artifact_definition_in_the_public_roster() => Artifacts(Public).ShouldEqual(Artifacts(Expected));
    [Fact] void should_preserve_every_relationship_definition() => Relationships(Native).ShouldEqual(Relationships(Expected));
    [Fact] void should_preserve_every_relationship_definition_in_reverse_order() => Relationships(Reverse).ShouldEqual(Relationships(Expected));
    [Fact] void should_preserve_every_relationship_definition_in_the_public_roster() => Relationships(Public).ShouldEqual(Relationships(Expected));
    [Fact] void should_preserve_complete_diagnostics() => Diagnostics(Native).ShouldEqual(Diagnostics(Expected));
    [Fact] void should_preserve_complete_diagnostics_in_reverse_order() => Diagnostics(Reverse).ShouldEqual(Diagnostics(Expected));
    [Fact] void should_preserve_printed_source() => Native.Source.ShouldEqual(Expected.Source);
    [Fact] void should_preserve_printed_source_in_reverse_order() => Reverse.Source.ShouldEqual(Expected.Source);
    [Fact] void should_preserve_printed_source_in_the_public_roster() => Public.Source.ShouldEqual(Expected.Source);
    [Fact] void should_preserve_verification_status() => Native.IsSuccess.ShouldEqual(Expected.IsSuccess);
    [Fact] void should_preserve_verification_status_in_reverse_order() => Reverse.IsSuccess.ShouldEqual(Expected.IsSuccess);
    [Fact] void should_preserve_verification_status_in_the_public_roster() => Public.IsSuccess.ShouldEqual(Expected.IsSuccess);
    [Fact] void should_not_reject_the_marten_contribution() => Public.Diagnostics.ShouldNotContain(_ => _.Code == "DOTNETADAPTER007");
    [Fact] void should_not_report_the_unrepresentable_shape() => Public.Diagnostics.ShouldNotContain(_ => _.Code == "GEN0049");
}
