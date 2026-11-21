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

    [Fact] void should_report_only_one_located_information_note() => (_contribution.Diagnostics.Count, _contribution.Diagnostics.Single().Source.Path, _contribution.Diagnostics.Single().Source.StartLine).ShouldEqual((1, "StoreScope/Configuration.cs", 8));
    [Fact] void should_use_the_reserved_scope_code_without_a_warning() => (_contribution.Diagnostics.Single().Code, _contribution.Diagnostics.Single().Severity, _contribution.Diagnostics.Single().Outcome).ShouldEqual((MartenDiagnosticCodes.StoreScopeNotRepresented, GenerationDiagnosticSeverity.Information, GenerationDiagnosticOutcome.Unsupported));
    [Fact] void should_not_invent_a_collision() => _contribution.Diagnostics.Single().Message.ShouldNotContain("Multiple ancillary stores");
    [Fact] void should_leave_single_store_facts_unchanged() => _contribution.Facts.ShouldBeEmpty();
    [Fact] void should_not_add_diagnostics_for_an_implicit_single_store() => Analyze(ProjectFor("namespace StoreScope; public class Configuration { public void Configure(Marten.StoreOptions options) { } }")).Diagnostics.ShouldBeEmpty();
}
