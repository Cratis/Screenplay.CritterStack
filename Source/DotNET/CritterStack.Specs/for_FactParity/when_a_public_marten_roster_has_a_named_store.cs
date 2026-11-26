// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Cratis.CritterStack.Screenplay.for_FactParity;

/// <summary>An explicit public Marten roster with a named store declares BackendScope facts and is admitted, not rejected atomically.</summary>
public class when_a_public_marten_roster_has_a_named_store : given.a_marten_document_effects_comparison
{
    const string Application =
        """
        namespace FactParity
        {
            public interface IAccounts : Marten.IDocumentStore;
            public sealed record Account(int Id);
            public static class Configuration
            {
                public static void Configure(Microsoft.Extensions.DependencyInjection.IServiceCollection services) =>
                    Marten.MartenServiceCollectionExtensions.AddMartenStore<IAccounts>(services, options => options.Schema.For<Account>());
            }
            public static class Opening
            {
                [Wolverine.Marten.MartenStore(typeof(IAccounts))]
                public static void Run(Marten.IDocumentSession session) => session.Store(new Account(1));
            }
        }
        """;

    void Because() => Compare(Application);

    [Fact] void should_declare_the_backend_scope_capability() => new MartenScreenplayAdapter().Descriptor.EmittedFactCapabilities.ShouldContain(GenerationFactCapability.BackendScope);
    [Fact] void should_not_reject_the_marten_contribution() => Public.Diagnostics.ShouldNotContain(_ => _.Code == "DOTNETADAPTER007");
    [Fact] void should_generate_a_compiler_verified_candidate() => Public.IsSuccess.ShouldBeTrue();
}
