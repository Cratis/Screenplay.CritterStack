// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Cratis.CritterStack.Screenplay.for_CritterStackScreenplayAdapter;

public class when_analyzing_marten_composite_projection : given.a_marten_store_scope_application
{
    const string Source =
        """
        namespace StoreScope;
        public record Changed;
        public class First { public void Apply(Changed e) { } }
        public class Second { public void Apply(Changed e) { } }
        public static class Configuration
        {
            public static void Configure(Marten.StoreOptions options)
            {
                options.Projections.CompositeProjectionFor("Combined", composite =>
                {
                    composite.Snapshot<First>(2);
                    composite.Add(stageNumber: 1, projection: new Second());
                });
                new ProjectionOptions().CompositeProjectionFor("Impostor", _ => { });
            }
        }
        public class ProjectionOptions
        {
            public void CompositeProjectionFor(string name, System.Action<CompositeProjection> configure) { }
        }
        public class CompositeProjection;
        """;

    AdapterContribution _contribution = null!;

    void Because() => _contribution = Analyze(ProjectFor(Source));

    [Fact] void should_report_only_the_real_composite_registration() => _contribution.Diagnostics.Count.ShouldEqual(1);
    [Fact] void should_use_the_reserved_composite_code() => _contribution.Diagnostics.Single().Code.ShouldEqual(MartenDiagnosticCodes.CompositeProjectionOmitted);
    [Fact] void should_report_a_warning_for_unsupported_processing() => (_contribution.Diagnostics.Single().Severity, _contribution.Diagnostics.Single().Outcome).ShouldEqual((GenerationDiagnosticSeverity.Warning, GenerationDiagnosticOutcome.Unsupported));
    [Fact] void should_locate_the_registration() => (_contribution.Diagnostics.Single().Source!.Path, _contribution.Diagnostics.Single().Source!.StartLine).ShouldEqual(("StoreScope/Configuration.cs", 9));
    [Fact] void should_list_both_constituent_types() => _contribution.Diagnostics.Single().Message.ShouldContain("StoreScope.First, StoreScope.Second");
    [Fact] void should_not_invent_subprojection_facts_or_ordering() => _contribution.Facts.ShouldBeEmpty();
    [Fact] void should_use_compilable_registration_signatures() => ProjectFor(Source).Compilation.GetDiagnostics().Where(_ => _.Severity == DiagnosticSeverity.Error).ShouldBeEmpty();
}
