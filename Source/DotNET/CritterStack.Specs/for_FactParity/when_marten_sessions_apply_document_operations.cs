// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Cratis.CritterStack.Screenplay.for_FactParity;

/// <summary>Direct Store, Insert, Update, Delete and DeleteWhere effects, repeated and in dormant methods, outside Wolverine handler conventions.</summary>
public class when_marten_sessions_apply_document_operations : given.a_marten_document_effects_comparison
{
    const string Application =
        """
        namespace FactParity
        {
            public sealed record Customer(int Id, string Name);
            public sealed record Invoice(int Id, decimal Amount);
            public sealed record Receipt(int Id);
            public sealed record Refund(int Id);
            public sealed record Archive(int Id);
            public sealed record Quote(int Id);
            public sealed record Ledger(int Id);
            public sealed record Journal(int Id);
            public static class Saving
            {
                public static void Run(Marten.IDocumentSession session)
                {
                    session.Store(new Customer(1, "a"));
                    session.Store(new Customer(2, "b"));
                    session.Insert(new Invoice(1, 2));
                    session.Update(new Receipt(1));
                }
            }
            public static class Removing
            {
                public static void Run(Marten.IDocumentOperations operations)
                {
                    operations.Delete<Refund>(1);
                    operations.Delete(new Archive(1));
                    operations.DeleteWhere<Quote>(_ => _.Id > 3);
                    operations.Store(new Customer(3, "c"));
                }
            }
            public static class Repeating
            {
                public static void Run(Marten.IDocumentSession session)
                {
                    session.Insert(new Ledger(1));
                    session.Insert(new Ledger(2));
                    session.Update(new Ledger(3));
                    session.Update(new Journal(1));
                    session.Delete<Journal>(2);
                    session.Delete<Journal>(3);
                    session.DeleteWhere<Ledger>(_ => _.Id > 1);
                    session.DeleteWhere<Ledger>(_ => _.Id > 2);
                }
            }
            public static class Dormant
            {
                public static void Run(Marten.IDocumentSession session) => session.Insert(new Quote(2));
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
    [Fact] void should_admit_every_marten_fact_in_the_public_roster() => Public.Diagnostics.ShouldNotContain(_ => _.Code == "DOTNETADAPTER007");
    [Fact] void should_print_the_same_source_in_the_public_roster() => Public.Source.ShouldEqual(Native.Source);
    [Fact] void should_not_emit_compatibility_document_artifacts() => Contribution.Facts.OfType<ArtifactFact>().Where(fact => fact.Definition.Key.Kind != ArtifactKind.Concept).ShouldBeEmpty();
    [Fact] void should_observe_the_document_operations() => Definitions(Expected).Count(definition => definition.Key.Kind == ArtifactKind.Handler).ShouldBeGreaterThan(0);
}
