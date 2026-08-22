// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Cratis.CritterStack.Screenplay.for_CritterStackScreenplayAdapter;

public class when_analyzing_legacy_marten_projection_metadata : given.a_legacy_marten_projection_metadata_application
{
    [Fact] void should_preserve_legacy_projection_name_metadata() => ProjectionMetadata.Any(_ => _.Message.Contains("projection name 'legacy-named'", StringComparison.Ordinal)).ShouldBeTrue();
    [Fact] void should_preserve_legacy_registration_name_metadata() => ProjectionMetadata.Any(_ => _.Message.Contains("registers projection name 'legacy-raw'", StringComparison.Ordinal)).ShouldBeTrue();
    [Fact] void should_preserve_the_non_inline_legacy_lifecycle() => Contribution.Diagnostics.Count(_ => _.Code == MartenDiagnosticCodes.ProjectionLifecycleOmitted).ShouldEqual(1);
    [Fact] void should_preserve_legacy_async_mode_assignment() => DaemonConfiguration.Any(_ => _.Message.Contains("AsyncMode 'HotCold'", StringComparison.Ordinal)).ShouldBeTrue();
    [Fact] void should_preserve_legacy_daemon_registration() => DaemonConfiguration.Any(_ => _.Message.Contains("mode 'Solo'", StringComparison.Ordinal)).ShouldBeTrue();
    [Fact] void should_preserve_raw_legacy_projections_neutrally() => Graph.Artifacts.Count(_ => _.Key.Kind == ArtifactKind.Projection).ShouldEqual(2);
    [Fact] void should_not_invent_legacy_projection_versions() => ProjectionMetadata.Any(_ => _.Message.Contains("version", StringComparison.OrdinalIgnoreCase)).ShouldBeFalse();
    [Fact] void should_not_classify_legacy_custom_projection_storage() => Graph.Artifacts.Any(_ => _.Key.Kind == ArtifactKind.Document && _.Variants.Any(variant => variant.Definition.Name == "HiddenDocument")).ShouldBeFalse();
    [Fact] void should_not_infer_state_views_for_legacy_custom_projections() => Graph.Artifacts.Any(_ => _.Key.Kind == ArtifactKind.ReadModel || _.Key.Kind == ArtifactKind.Reducer).ShouldBeFalse();
    [Fact] void should_anchor_legacy_evidence_in_authored_source() => Contribution.Diagnostics.Where(_ => _.Code == MartenDiagnosticCodes.ProjectionMetadataOmitted || _.Code == MartenDiagnosticCodes.DaemonConfigurationOmitted || _.Code == MartenDiagnosticCodes.CustomProcessingOmitted).All(_ => _.Source?.Path == "Legacy/Configuration.cs").ShouldBeTrue();

    IReadOnlyList<GenerationDiagnostic> ProjectionMetadata => [.. Contribution.Diagnostics.Where(_ => _.Code == MartenDiagnosticCodes.ProjectionMetadataOmitted)];
    IReadOnlyList<GenerationDiagnostic> DaemonConfiguration => [.. Contribution.Diagnostics.Where(_ => _.Code == MartenDiagnosticCodes.DaemonConfigurationOmitted)];
}
