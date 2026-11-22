// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Cratis.CritterStack.Screenplay.for_CritterStackScreenplayAdapter;

public class when_analyzing_ancillary_stores_with_distinct_event_model_names : given.a_marten_store_scope_application
{
    const string Source =
        """
        using Marten;
        namespace StoreScope;
        public interface IAccounts : IDocumentStore;
        public interface IBilling : IDocumentStore;
        public static class Configuration
        {
            public static void Configure(Microsoft.Extensions.DependencyInjection.IServiceCollection services)
            {
                services.AddMartenStore<IAccounts>(options => options.EventModelName = "Accounts");
                services.AddMartenStore<IBilling>(options => options.EventModelName = "Billing");
            }
        }
        """;

    AdapterContribution _contribution = null!;

    void Because() => _contribution = Analyze(ProjectFor(Source));

    [Fact] void should_preserve_two_case_sensitive_qualifiers() => _contribution.Facts.OfType<BackendScopeFact>().Select(fact => fact.Definition.Key.EventModelName).ShouldContainOnly("Accounts", "Billing");
    [Fact] void should_not_report_supported_scopes_as_unsupported() => _contribution.Diagnostics.ShouldBeEmpty();
    [Fact] void should_locate_each_declaration() => _contribution.Facts.OfType<BackendScopeFact>().Select(fact => (fact.Evidence.Source.Path, fact.Evidence.Source.StartLine)).ShouldContainOnly(("StoreScope/Configuration.cs", 9), ("StoreScope/Configuration.cs", 10));
    [Fact] void should_not_map_store_names_to_modules() => _contribution.Facts.OfType<ArtifactFact>().ShouldBeEmpty();
}
