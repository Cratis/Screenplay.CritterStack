// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Cratis.CritterStack.Screenplay.for_CritterStackScreenplayAdapter;

public class when_analyzing_typed_snapshots_and_custom_sending : given.a_marten_typed_snapshot_and_sending_application
{
    AdapterContribution _result = null!;

    void Because() => _result = new CritterStackScreenplayAdapter().Analyze(new([Project]), new());

    [Fact] void should_compile_the_real_overload_shapes() => Project.Compilation.GetDiagnostics().Where(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error).ShouldBeEmpty();
    [Fact] void should_admit_the_snapshot_model() => Names(ArtifactKind.ReadModel).ShouldContain("SnapshotModel");
    [Fact] void should_admit_the_live_model() => Names(ArtifactKind.ReadModel).ShouldContain("LiveModel");
    [Fact] void should_not_treat_the_identity_type_as_a_model() => Names(ArtifactKind.ReadModel).ShouldNotContain("NaturalId");
    [Fact] void should_not_treat_the_identity_type_as_a_reducer() => Names(ArtifactKind.Reducer).ShouldNotContain("NaturalIdSnapshot");
    [Fact] void should_retain_lifecycle_loss() => _result.Diagnostics.Count(diagnostic => diagnostic.Code == MartenDiagnosticCodes.ProjectionLifecycleOmitted).ShouldEqual(2);
    [Fact] void should_preserve_literal_domain_messages() => Names(ArtifactKind.Message).ShouldContain("Changed");
    [Fact] void should_preserve_timeout_messages() => Names(ArtifactKind.Message).ShouldContain("Reminder");
    [Fact] void should_keep_the_timeout_publish_relationship() => _result.Facts.OfType<RelationshipFact>().Any(fact => fact.Definition.Key.Kind == RelationshipKind.Publishes && fact.Definition.Key.Target.Value.EndsWith(".Reminder", StringComparison.Ordinal)).ShouldBeTrue();
    [Fact] void should_ignore_an_unrelated_sending_interface() => Names(ArtifactKind.Message).ShouldContain("Impostor");
    [Fact] void should_not_publish_the_custom_delivery_wrapper() => Names(ArtifactKind.Message).ShouldNotContain("Delivery");
    [Fact] void should_diagnose_custom_sending_as_unsupported() => CustomSending.Outcome.ShouldEqual(GenerationDiagnosticOutcome.Unsupported);
    [Fact] void should_locate_custom_sending_loss() => CustomSending.Source!.Path.ShouldEqual("NewConventions/Projections.cs");
    [Fact] void should_explain_the_apply_async_boundary() => CustomSending.Message.ShouldContain("ApplyAsync sending behavior is not interpreted");

    GenerationDiagnostic CustomSending => _result.Diagnostics.Single(diagnostic => diagnostic.Code == MartenDiagnosticCodes.ProjectionSideEffectUnresolved);
    IReadOnlyList<string> Names(ArtifactKind kind) => [.. _result.Facts.OfType<ArtifactFact>().Where(fact => fact.Definition.Key.Kind == kind).Select(fact => fact.Definition.Name)];
}
