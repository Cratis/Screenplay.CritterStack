// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Cratis.CritterStack.Screenplay.for_CritterStackScreenplayAdapter;

public class when_analyzing_projection_side_effects : given.a_projection_side_effect_application
{
    ResolvedApplicationGraph _graph = null!;

    void Because() => _graph = new GenerationResolver().Resolve([Contribution]);

    [Fact] void should_compile_the_fixture() => Project.Compilation.GetDiagnostics().Where(_ => _.Severity == Microsoft.CodeAnalysis.DiagnosticSeverity.Error).ShouldBeEmpty();
    [Fact] void should_compile_the_unconfigured_fixture() => UnconfiguredProject.Compilation.GetDiagnostics().Where(_ => _.Severity == Microsoft.CodeAnalysis.DiagnosticSeverity.Error).ShouldBeEmpty();
    [Fact] void should_discover_the_published_message() => ArtifactNames(ArtifactKind.Message).ShouldContain("SummaryChanged");
    [Fact] void should_publish_from_the_projection() => RelationshipSource(Publishes).ShouldEqual("SummaryProjection");
    [Fact] void should_publish_the_literal_message() => RelationshipTarget(Publishes).ShouldEqual("SummaryChanged");
    [Fact] void should_retain_exact_side_effect_evidence() => Publishes.Evidence.Strength.ShouldEqual(EvidenceStrength.Exact);
    [Fact] void should_retain_the_unconfigured_side_effect() => UnconfiguredPublishes.ShouldNotBeNull();
    [Fact] void should_mark_unconfigured_side_effect_evidence_as_conventional() => UnconfiguredPublishes.Evidence.Strength.ShouldEqual(EvidenceStrength.Conventional);
    [Fact] void should_explain_the_unobserved_side_effect_option() => UnconfiguredPublishes.Evidence.Explanation.ShouldContain("side-effect option not observed in authored configuration");
    [Fact] void should_not_classify_the_published_message_as_an_event() => ArtifactNames(ArtifactKind.Event).ShouldNotContain("SummaryChanged");
    [Fact] void should_report_the_unresolved_payload() => SideEffectDiagnostics.Count.ShouldEqual(1);
    [Fact] void should_classify_the_unresolved_payload_as_unknown() => SideEffectDiagnostics.Single().Outcome.ShouldEqual(GenerationDiagnosticOutcome.Unknown);
    [Fact] void should_not_report_other_loss() => Contribution.Diagnostics.Count.ShouldEqual(1);

    RelationshipFact Publishes => Contribution.Facts.OfType<RelationshipFact>().Single(_ => _.Definition.Key.Kind == RelationshipKind.Publishes);
    RelationshipFact UnconfiguredPublishes => UnconfiguredContribution.Facts.OfType<RelationshipFact>().Single(_ => _.Definition.Key.Kind == RelationshipKind.Publishes);

    IReadOnlyList<GenerationDiagnostic> SideEffectDiagnostics =>
        [.. Contribution.Diagnostics.Where(_ => _.Code == "MARTEN0015")];

    IReadOnlyList<string> ArtifactNames(ArtifactKind kind) =>
        [.. _graph.Artifacts.Where(_ => _.Key.Kind == kind).Select(_ => _.Variants.Single().Definition.Name)];

    string RelationshipSource(RelationshipFact relationship) =>
        _graph.Artifacts.Single(_ => _.Key.Subject == relationship.Definition.Key.Source).Variants.Single().Definition.Name;

    string RelationshipTarget(RelationshipFact relationship) =>
        _graph.Artifacts.Single(_ => _.Key.Subject == relationship.Definition.Key.Target).Variants.Single().Definition.Name;
}
