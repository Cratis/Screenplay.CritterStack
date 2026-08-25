// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Cratis.CritterStack.Screenplay.for_CritterStackScreenplayAdapter;

public class when_analyzing_marten_wire_configuration : given.a_marten_wire_configuration_application
{
    ResolvedApplicationGraph _graph = null!;

    void Because() => _graph = new GenerationResolver().Resolve([Contribution]);

    [Fact] void should_compile_the_baseline_fixture() => BaselineProject.Compilation.GetDiagnostics().Where(_ => _.Severity == Microsoft.CodeAnalysis.DiagnosticSeverity.Error).ShouldBeEmpty();
    [Fact] void should_compile_the_configured_fixture() => Project.Compilation.GetDiagnostics().Where(_ => _.Severity == Microsoft.CodeAnalysis.DiagnosticSeverity.Error).ShouldBeEmpty();
    [Fact] void should_report_each_wire_configuration() => WireDiagnostics.Count.ShouldEqual(3);
    [Fact] void should_report_the_binary_serialized_event_type() => BinarySerializerDiagnostic.Message.ShouldContain("WireEvent");
    [Fact] void should_report_the_binary_serializer_type() => BinarySerializerDiagnostic.Message.ShouldContain("CustomBinarySerializer");
    [Fact] void should_report_the_append_mode() => WireDiagnostics.Single(_ => _.Message.Contains("append-mode", StringComparison.Ordinal)).Message.ShouldContain("Quick");
    [Fact] void should_report_the_stream_identity() => WireDiagnostics.Single(_ => _.Message.Contains("stream-identity", StringComparison.Ordinal)).Message.ShouldContain("AsString");
    [Fact] void should_classify_exact_wire_configuration_as_unsupported() => WireDiagnostics.All(_ => _.Outcome == GenerationDiagnosticOutcome.Unsupported).ShouldBeTrue();
    [Fact] void should_nominate_each_registered_value_type_as_a_concept() => ConceptNames.ShouldContainOnly("AlertId", "OtherId");
    [Fact] void should_retain_configured_value_type_evidence() => Contribution.Facts.OfType<ArtifactFact>().Where(_ => _.Definition.Key.Kind == ArtifactKind.Concept).All(_ => _.Evidence.Strength == EvidenceStrength.Configured).ShouldBeTrue();
    [Fact] void should_not_classify_wire_configured_events_as_events() => ArtifactNames(ArtifactKind.Event).ShouldBeEmpty();
    [Fact] void should_not_change_non_concept_facts() => Contribution.Facts.Where(_ => _ is not ArtifactFact { Definition.Key.Kind: ArtifactKind.Concept }).Select(_ => _.Id.Value).ShouldContainOnly(BaselineContribution.Facts.Select(_ => _.Id.Value));
    [Fact] void should_not_report_other_loss() => Contribution.Diagnostics.Count.ShouldEqual(3);

    GenerationDiagnostic BinarySerializerDiagnostic => WireDiagnostics.Single(_ => _.Message.Contains("binary serializer", StringComparison.Ordinal));

    IReadOnlyList<GenerationDiagnostic> WireDiagnostics =>
        [.. Contribution.Diagnostics.Where(_ => _.Code == MartenDiagnosticCodes.EventTypeConfigurationOmitted)];

    IReadOnlyList<string> ConceptNames => ArtifactNames(ArtifactKind.Concept);

    IReadOnlyList<string> ArtifactNames(ArtifactKind kind) =>
        [.. _graph.Artifacts.Where(_ => _.Key.Kind == kind).Select(_ => _.Variants.Single().Definition.Name)];
}
