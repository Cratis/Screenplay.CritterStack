// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Cratis.CritterStack.Screenplay.for_FactParity;

/// <summary>Document effects under single, several, conflicting and unknown store owners never emit optimistically, outside Wolverine handler conventions.</summary>
public class when_marten_sessions_have_store_owners : given.a_marten_document_effects_comparison
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
            public static class Configuration
            {
                public static void Configure(Microsoft.Extensions.DependencyInjection.IServiceCollection services)
                {
                    Marten.MartenServiceCollectionExtensions.AddMartenStore<IAccounts>(services, options => options.Schema.For<Shared>());
                    Marten.MartenServiceCollectionExtensions.AddMartenStore<IBilling>(services, options => options.Schema.For<Shared>());
                }
            }
            public static class Opening
            {
                [Wolverine.Marten.MartenStore(typeof(IAccounts))]
                public static void Run(Marten.IDocumentSession session)
                {
                    session.Store(new Account(1));
                    session.Store(new Shared(1));
                }
            }
            public static class Billing
            {
                [Wolverine.Marten.MartenStore(typeof(IBilling))]
                public static void Run(Marten.IDocumentSession session)
                {
                    session.Insert(new Charge(1));
                    session.Update(new Shared(2));
                }
            }
            [Wolverine.Marten.MartenStore(typeof(IAccounts))]
            [Wolverine.Marten.MartenStore(typeof(IBilling))]
            public static class Both
            {
                public static void Run(Marten.IDocumentSession session) => session.Store(new Ambiguous(1));
            }
            public static class Either
            {
                public static void Run(Marten.IDocumentSession session) => session.Delete<Ambiguous>(2);
            }
            public static class Typed
            {
                public static void Run(Marten.Ancillary.IDocumentSession<IBilling> session) => session.DeleteWhere<Charge>(_ => _.Id > 1);
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
}
