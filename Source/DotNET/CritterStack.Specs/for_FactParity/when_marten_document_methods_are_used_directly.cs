// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Cratis.CritterStack.Screenplay.for_FactParity;

/// <summary>Direct Store, Insert, Update, Delete and DeleteWhere effects keep the oracle's handlers, relationships and diagnostics.</summary>
public class when_marten_document_methods_are_used_directly : when_marten_document_effects_are_compared
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
            public sealed record Save;
            public sealed record Remove;
            public sealed record Audit;
            public static class SaveHandler
            {
                public static void Handle(Save message, Marten.IDocumentSession session)
                {
                    session.Store(new Customer(1, "a"));
                    session.Store(new Customer(2, "b"));
                    session.Insert(new Invoice(1, 2));
                    session.Update(new Receipt(1));
                }
            }
            public static class RemoveHandler
            {
                public static void Handle(Remove message, Marten.IDocumentOperations operations)
                {
                    operations.Delete<Refund>(1);
                    operations.Delete(new Archive(1));
                    operations.DeleteWhere<Quote>(_ => _.Id > 3);
                    operations.Store(new Customer(3, "c"));
                }
            }
            public static class AuditHandler
            {
                public static void Handle(Audit message, Marten.IQuerySession session) => session.Load<Customer>(1);
                public static void OnException(Save message, System.Exception exception, Marten.IDocumentSession session) => session.Store(new Receipt(2));
            }
            public static class Dormant
            {
                public static void Helper(Marten.IDocumentSession session) => session.Insert(new Quote(2));
            }
        }
        """;

    void Because() => Compare(Application);

    [Fact] void should_declare_a_handler_for_each_effecting_method() => Handlers(Native).ShouldEqual(Handlers(Expected));
    [Fact] void should_report_each_document_omission() => Warnings(Native, "GEN0004").ShouldEqual(Warnings(Expected, "GEN0004"));
}
