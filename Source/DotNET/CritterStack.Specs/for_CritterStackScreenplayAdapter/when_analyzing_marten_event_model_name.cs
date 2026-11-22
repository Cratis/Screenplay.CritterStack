// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Cratis.CritterStack.Screenplay.for_CritterStackScreenplayAdapter;

public class when_analyzing_marten_event_model_name : given.a_marten_store_scope_application
{
    const string Source =
        """
        namespace StoreScope;
        public static class Configuration
        {
            const string SharedName = "Shared";
            public static void Configure(Marten.StoreOptions options, string runtimeName)
            {
                options.EventModelName = "Accounts";
                options.EventModelName = SharedName;
                options.EventModelName = runtimeName;
                options.EventModelName += "suffix";
                new Lookalike().EventModelName = "NotMarten";
            }
        }
        public class Lookalike { public string? EventModelName { get; set; } }
        """;

    AdapterContribution _contribution = null!;

    void Because() => _contribution = Analyze(ProjectFor(Source));

    [Fact] void should_only_report_the_four_authored_marten_assignments() => _contribution.Diagnostics.Count.ShouldEqual(4);
    [Fact] void should_report_unbound_ownership() => _contribution.Diagnostics.All(_ => _.Code == GenerationDiagnosticCodes.UnknownBackendScope).ShouldBeTrue();
    [Fact] void should_use_error_severity_for_unproved_ownership() => _contribution.Diagnostics.All(_ => _.Severity == GenerationDiagnosticSeverity.Error && _.Outcome == GenerationDiagnosticOutcome.Unknown).ShouldBeTrue();
    [Fact] void should_locate_each_assignment() => _contribution.Diagnostics.Select(_ => (_.Source.Path, _.Source.StartLine)).ShouldContainOnly(("StoreScope/Configuration.cs", 7), ("StoreScope/Configuration.cs", 8), ("StoreScope/Configuration.cs", 9), ("StoreScope/Configuration.cs", 10));
    [Fact] void should_not_invent_store_declarations_from_unbound_names() => _contribution.Facts.OfType<BackendScopeFact>().ShouldBeEmpty();
    [Fact] void should_not_change_facts_or_placement() => _contribution.Facts.ShouldBeEmpty();
}
