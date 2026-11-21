// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Cratis.CritterStack.Screenplay.for_CritterStackScreenplayAdapter;

public class when_analyzing_marten_flat_table_projection : given.a_marten_store_scope_application
{
    const string Source =
        """
        namespace StoreScope;
        public class TableProjection : Marten.Events.Projections.Flattened.FlatTableProjection
        {
            public TableProjection() : base("totals", Marten.Events.Projections.Flattened.SchemaNameSource.Explicit) { }
        }
        public class FlatTableProjection;
        public static class Configuration
        {
            public static void Configure(Marten.StoreOptions options)
            {
                options.Projections.Add<TableProjection>();
                options.Projections.Add(new TableProjection());
                options.Projections.Add<FlatTableProjection>();
            }
        }
        """;

    AdapterContribution _contribution = null!;

    void Because() => _contribution = Analyze(ProjectFor(Source));

    [Fact] void should_report_both_real_registration_forms_only() => _contribution.Diagnostics.Count.ShouldEqual(2);
    [Fact] void should_use_the_reserved_flat_table_code() => _contribution.Diagnostics.All(_ => _.Code == MartenDiagnosticCodes.FlatTableProjectionOmitted).ShouldBeTrue();
    [Fact] void should_report_unsupported_processing_as_warnings() => _contribution.Diagnostics.All(_ => _.Severity == GenerationDiagnosticSeverity.Warning && _.Outcome == GenerationDiagnosticOutcome.Unsupported).ShouldBeTrue();
    [Fact] void should_locate_each_registration() => _contribution.Diagnostics.Select(_ => (_.Source!.Path, _.Source!.StartLine)).ShouldContainOnly(("StoreScope/Configuration.cs", 11), ("StoreScope/Configuration.cs", 12));
    [Fact] void should_name_the_projection_type() => _contribution.Diagnostics.All(_ => _.Message.Contains("StoreScope.TableProjection", StringComparison.Ordinal)).ShouldBeTrue();
    [Fact] void should_not_invent_flat_table_state_or_relationships() => _contribution.Facts.ShouldBeEmpty();
    [Fact] void should_use_compilable_registration_signatures() => ProjectFor(Source).Compilation.GetDiagnostics().Where(_ => _.Severity == DiagnosticSeverity.Error).ShouldBeEmpty();
}
