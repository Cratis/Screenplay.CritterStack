// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Cratis.CritterStack.Screenplay.for_CritterStackScreenplayAdapter;

public class when_analyzing_unresolved_marten_projection_and_subscription_configuration : given.a_marten_projection_metadata_application
{
    [Fact] void should_report_a_computed_projection_name() => ProjectionMetadata.Any(_ => _.Message.Contains("ComputedProjection", StringComparison.Ordinal) && _.Message.Contains("non-constant value", StringComparison.Ordinal)).ShouldBeTrue();
    [Fact] void should_report_a_conditional_projection_version() => ProjectionMetadata.Any(_ => _.Message.Contains("ComputedProjection", StringComparison.Ordinal) && _.Message.Contains("otherwise non-constant value", StringComparison.Ordinal)).ShouldBeTrue();
    [Fact] void should_report_a_computed_lifecycle() => Contribution.Diagnostics.Any(_ => _.Code == MartenDiagnosticCodes.ProjectionLifecycleOmitted && _.Message.Contains("ComputedProjection", StringComparison.Ordinal) && _.Message.Contains("non-constant lifecycle", StringComparison.Ordinal)).ShouldBeTrue();
    [Fact] void should_report_a_computed_daemon_mode() => Contribution.Diagnostics.Any(_ => _.Code == MartenDiagnosticCodes.DaemonConfigurationOmitted && _.Message.Contains("could not be resolved safely", StringComparison.Ordinal)).ShouldBeTrue();
    [Fact] void should_report_a_conditional_subscription_filter() => SubscriptionConfiguration.Any(_ => _.Message.Contains("ConditionalSubscription", StringComparison.Ordinal) && _.Message.Contains("IncludeType conditionally", StringComparison.Ordinal)).ShouldBeTrue();
    [Fact] void should_report_a_computed_archived_policy() => SubscriptionConfiguration.Any(_ => _.Message.Contains("ConditionalSubscription", StringComparison.Ordinal) && _.Message.Contains("archived-event policy conditionally", StringComparison.Ordinal)).ShouldBeTrue();
    [Fact] void should_report_an_unresolved_time_start() => SubscriptionConfiguration.Any(_ => _.Message.Contains("ConditionalSubscription", StringComparison.Ordinal) && _.Message.Contains("SubscribeFromTime", StringComparison.Ordinal)).ShouldBeTrue();
    [Fact] void should_not_fabricate_a_computed_projection_name() => ProjectionMetadata.Any(_ => _.Message.Contains("projection name 'computed'", StringComparison.Ordinal)).ShouldBeFalse();
    [Fact] void should_not_fabricate_a_conditional_filter_event() => Graph.Artifacts.Any(_ => _.Key.Kind == ArtifactKind.Event && _.Variants.Any(variant => variant.Definition.Name == "ConditionalSubscription")).ShouldBeFalse();
    [Fact] void should_not_classify_raw_subscription_storage() => Graph.Artifacts.Any(_ => _.Key.Kind == ArtifactKind.Document && _.Variants.Any(variant => variant.Definition.Name == "HiddenDocument")).ShouldBeFalse();

    IReadOnlyList<GenerationDiagnostic> ProjectionMetadata => [.. Contribution.Diagnostics.Where(_ => _.Code == MartenDiagnosticCodes.ProjectionMetadataOmitted)];
    IReadOnlyList<GenerationDiagnostic> SubscriptionConfiguration => [.. Contribution.Diagnostics.Where(_ => _.Code == MartenDiagnosticCodes.SubscriptionConfigurationOmitted)];
}
