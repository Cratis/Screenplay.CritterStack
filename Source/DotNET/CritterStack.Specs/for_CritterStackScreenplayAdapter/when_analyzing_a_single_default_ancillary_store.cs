// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Cratis.CritterStack.Screenplay.for_CritterStackScreenplayAdapter;

public class when_analyzing_a_single_default_ancillary_store : given.a_marten_store_scope_application
{
    const string Source =
        """
        using Marten;
        namespace StoreScope;
        public interface IAccounts : IDocumentStore;
        public static class Configuration
        {
            public static void Configure(Microsoft.Extensions.DependencyInjection.IServiceCollection services)
            {
                services.AddMartenStore<IAccounts>(options => { });
            }
        }
        """;

    AdapterContribution _contribution = null!;

    void Because() => _contribution = Analyze(ProjectFor(Source));

    [Fact] void should_emit_one_located_scope_declaration() => (_contribution.Facts.OfType<BackendScopeFact>().Count(), _contribution.Facts.Single().Evidence.Source.Path, _contribution.Facts.Single().Evidence.Source.StartLine).ShouldEqual((1, "StoreScope/Configuration.cs", 8));
    [Fact] void should_keep_the_default_qualifier_null() => _contribution.Facts.OfType<BackendScopeFact>().Single().Definition.Key.EventModelName.ShouldBeNull();
    [Fact] void should_not_report_supported_scopes_as_unsupported() => _contribution.Diagnostics.ShouldBeEmpty();
    [Fact] void should_not_invent_a_default_store_from_a_single_ancillary_registration() => _contribution.Facts.OfType<ArtifactFact>().ShouldBeEmpty();
    [Fact] void should_not_add_diagnostics_for_an_implicit_single_store() => Analyze(ProjectFor("namespace StoreScope; public class Configuration { public void Configure(Marten.StoreOptions options) { } }")).Diagnostics.ShouldBeEmpty();
}
