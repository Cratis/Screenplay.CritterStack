// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Cratis.CritterStack.Screenplay.for_FactParity;

/// <summary>Document effects under one, several, conflicting and unknown store owners never emit optimistically.</summary>
public class when_marten_document_effects_have_store_owners : when_marten_document_effects_are_compared
{
    const string Application =
        """
        namespace FactParity
        {
            public interface IAccounts : Marten.IDocumentStore;
            public interface IBilling : Marten.IDocumentStore;
            public sealed record Account(int Id);
            public sealed record Charge(int Id);
            public sealed record Shared(int Id);
            public sealed record Ambiguous(int Id);
            public sealed record Open;
            public sealed record Bill;
            public sealed record Both;
            public sealed record Either;
            public sealed record Typed;
            public static class Configuration
            {
                public static void Configure(Microsoft.Extensions.DependencyInjection.IServiceCollection services)
                {
                    Marten.MartenServiceCollectionExtensions.AddMartenStore<IAccounts>(services, options => options.Schema.For<Shared>());
                    Marten.MartenServiceCollectionExtensions.AddMartenStore<IBilling>(services, options => options.Schema.For<Shared>());
                }
            }
            public static class OpenHandler
            {
                [Wolverine.Marten.MartenStore(typeof(IAccounts))]
                public static void Handle(Open message, Marten.IDocumentSession session)
                {
                    session.Store(new Account(1));
                    session.Store(new Shared(1));
                }
            }
            public static class BillHandler
            {
                [Wolverine.Marten.MartenStore(typeof(IBilling))]
                public static void Handle(Bill message, Marten.IDocumentSession session)
                {
                    session.Insert(new Charge(1));
                    session.Update(new Shared(2));
                }
            }
            [Wolverine.Marten.MartenStore(typeof(IAccounts))]
            [Wolverine.Marten.MartenStore(typeof(IBilling))]
            public static class BothHandler
            {
                public static void Handle(Both message, Marten.IDocumentSession session) => session.Store(new Ambiguous(1));
            }
            public static class EitherHandler
            {
                public static void Handle(Either message, Marten.IDocumentSession session) => session.Delete<Ambiguous>(2);
            }
            public static class TypedHandler
            {
                public static void Handle(Typed message, Marten.Ancillary.IDocumentSession<IBilling> session) => session.DeleteWhere<Charge>(_ => _.Id > 1);
            }
        }
        """;

    void Because() => Compare(Application);

    [Fact] void should_declare_the_same_owned_handlers() => Handlers(Native).ShouldEqual(Handlers(Expected));
    [Fact] void should_report_the_same_ownership_diagnostics() => Warnings(Native, "MARTEN0001").ShouldEqual(Warnings(Expected, "MARTEN0001"));
}
