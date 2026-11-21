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

    [Fact] void should_report_two_registrations_two_selections_a_name_and_a_collision() => _contribution.Diagnostics.Count.ShouldEqual(6);
    [Fact] void should_use_the_reserved_scope_code_and_warning_outcome() => _contribution.Diagnostics.All(_ => _.Code == MartenDiagnosticCodes.StoreScopeNotRepresented && _.Severity == GenerationDiagnosticSeverity.Warning && _.Outcome == GenerationDiagnosticOutcome.Unsupported).ShouldBeTrue();
    [Fact] void should_locate_all_scope_evidence() => _contribution.Diagnostics.Select(_ => (_.Source!.Path, _.Source!.StartLine)).ShouldContainOnly(("StoreScope/Configuration.cs", 5), ("StoreScope/Configuration.cs", 8), ("StoreScope/Configuration.cs", 15), ("StoreScope/Configuration.cs", 15), ("StoreScope/Configuration.cs", 15), ("StoreScope/Configuration.cs", 16));
    [Fact] void should_warn_of_default_name_coalescing_within_the_project() => _contribution.Diagnostics.Count(_ => _.Message.StartsWith("Multiple ancillary stores may use the default event-model name", StringComparison.Ordinal)).ShouldEqual(1);
    [Fact] void should_explain_the_missing_ownership_boundary() => _contribution.Diagnostics.All(_ => _.Message.Contains("facts are not scoped by store; ownership not represented", StringComparison.Ordinal)).ShouldBeTrue();
    [Fact] void should_not_invent_modules_ownership_or_store_merging() => _contribution.Facts.ShouldBeEmpty();
    [Fact] void should_use_compilable_store_signatures() => ProjectFor(Source).Compilation.GetDiagnostics().Where(_ => _.Severity == DiagnosticSeverity.Error).ShouldBeEmpty();
}
