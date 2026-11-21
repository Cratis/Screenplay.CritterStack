// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Cratis.CritterStack.Screenplay.for_CritterStackScreenplayAdapter;

public class when_analyzing_marten_evolve_overrides : given.a_marten_evolve_application
{
    AdapterContribution _contribution = null!;

    IEnumerable<string> Events => _contribution.Facts.OfType<ArtifactFact>()
        .Where(_ => _.Definition.Key.Kind == ArtifactKind.Event).Select(_ => _.Definition.Name);

    void Because() => _contribution = new MartenScreenplayAdapter().Analyze(Context, new DotNetAdapterOptions());

    [Fact] void should_only_report_the_five_genuine_overrides_including_the_intermediate_chain() => _contribution.Diagnostics.Count.ShouldEqual(5);
    [Fact] void should_classify_untyped_processing_as_information() => _contribution.Diagnostics.All(_ => _.Severity == GenerationDiagnosticSeverity.Information).ShouldBeTrue();
    [Fact] void should_report_unsupported_processing_without_guessing_event_types() => _contribution.Diagnostics.All(_ => _.Code == MartenDiagnosticCodes.CustomProcessingOmitted && _.Outcome == GenerationDiagnosticOutcome.Unsupported).ShouldBeTrue();
    [Fact] void should_locate_each_override_in_authored_source() => _contribution.Diagnostics.All(_ => _.Source is { StartLine: > 0, StartColumn: > 0 } && _.Source.Path == "Evolution/Projections.cs").ShouldBeTrue();
    [Fact] void should_name_each_unsupported_dispatch_method() => _contribution.Diagnostics.Select(_ => _.Message.Split('\'')[1]).ShouldContainOnly("Evolve", "Evolve", "EvolveAsync", "DetermineAction", "DetermineActionAsync");
    [Fact] void should_not_mistake_snapshot_identity_session_or_identity_setter_for_events() => Events.ShouldContainOnly("Applied", "Created", "Deleted", "RegisteredDelete", "ChainedDelete");
    [Fact] void should_ignore_typed_plain_hiding_private_and_unrelated_evolve_members() => Events.ShouldNotContain("NotAnEvent");
    [Fact] void should_ignore_the_lambda_delete_overload() => Events.ShouldNotContain("IgnoredLambda");
    [Fact] void should_keep_direct_and_fluent_constructor_delete_registrations_as_consumed_events() => _contribution.Facts.OfType<RelationshipFact>().Count(_ => _.Definition.Key.Kind == RelationshipKind.Consumes).ShouldEqual(5);
    [Fact] void should_use_compilable_upstream_signatures() => Project.Compilation.GetDiagnostics(TestContext.Current.CancellationToken).Where(_ => _.Severity == DiagnosticSeverity.Error).ShouldBeEmpty();
}
