// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Cratis.CritterStack.Screenplay.for_CritterStackScreenplayAdapter;

public class when_analyzing_marten_ancillary_stores : given.a_marten_store_scope_application
{
    const string Source =
        """
        using Marten;
        namespace StoreScope;
        public interface IAccounts : IDocumentStore;
        public interface IBilling : IDocumentStore;
        [Wolverine.Marten.MartenStore(typeof(IAccounts))]
        public class Handler
        {
            [Wolverine.Marten.MartenStore(typeof(IBilling))]
            public void Handle() { }
        }
        public static class Configuration
        {
            public static void Configure(Microsoft.Extensions.DependencyInjection.IServiceCollection services)
            {
                services.AddMartenStore<IAccounts>(options => options.EventModelName = "EventModel");
                services.AddMartenStore<IBilling>(options => { });
                new Lookalike().AddMartenStore<IAccounts>();
            }
        }
        public class Lookalike { public void AddMartenStore<T>() { } }
        [MartenStore(typeof(IAccounts))]
        public class UnrelatedHandler;
        public class MartenStoreAttribute(System.Type storeType) : System.Attribute;
        """;

    AdapterContribution _contribution = null!;

    void Because() => _contribution = Analyze(ProjectFor(Source));

    [Fact] void should_declare_two_distinct_store_occurrences() => _contribution.Facts.OfType<BackendScopeFact>().Select(fact => fact.Definition.Key.Store).Distinct().Count().ShouldEqual(2);
    [Fact] void should_preserve_the_default_qualifier_as_null() => _contribution.Facts.OfType<BackendScopeFact>().Select(fact => fact.Definition.Key.EventModelName).ShouldContainOnly("EventModel", null);
    [Fact] void should_locate_the_registrations() => _contribution.Facts.OfType<BackendScopeFact>().Select(fact => (fact.Evidence.Source.Path, fact.Evidence.Source.StartLine)).ShouldContainOnly(("StoreScope/Configuration.cs", 15), ("StoreScope/Configuration.cs", 16));
    [Fact] void should_not_report_supported_scopes_as_unsupported() => _contribution.Diagnostics.ShouldBeEmpty();
    [Fact] void should_not_turn_scope_configuration_into_output_modules() => _contribution.Facts.OfType<ArtifactFact>().ShouldBeEmpty();
    [Fact] void should_use_compilable_store_signatures() => ProjectFor(Source).Compilation.GetDiagnostics(TestContext.Current.CancellationToken).Where(_ => _.Severity == DiagnosticSeverity.Error).ShouldBeEmpty();
}
