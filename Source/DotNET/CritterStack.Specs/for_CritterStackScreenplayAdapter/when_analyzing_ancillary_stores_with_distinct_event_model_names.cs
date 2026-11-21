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

    [Fact] void should_report_the_two_names_and_two_registrations_only() => _contribution.Diagnostics.Count.ShouldEqual(4);
    [Fact] void should_still_report_that_ownership_is_not_represented() => _contribution.Diagnostics.All(_ => _.Code == MartenDiagnosticCodes.StoreScopeNotRepresented && _.Severity == GenerationDiagnosticSeverity.Warning && _.Outcome == GenerationDiagnosticOutcome.Unsupported).ShouldBeTrue();
    [Fact] void should_not_report_a_default_name_collision() => _contribution.Diagnostics.Any(_ => _.Message.StartsWith("Multiple ancillary stores", StringComparison.Ordinal)).ShouldBeFalse();
    [Fact] void should_locate_each_declaration() => _contribution.Diagnostics.Select(_ => (_.Source.Path, _.Source.StartLine)).ShouldContainOnly(("StoreScope/Configuration.cs", 9), ("StoreScope/Configuration.cs", 9), ("StoreScope/Configuration.cs", 10), ("StoreScope/Configuration.cs", 10));
    [Fact] void should_not_map_store_names_to_modules() => _contribution.Facts.ShouldBeEmpty();
}
