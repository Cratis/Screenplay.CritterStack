// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Cratis.CritterStack.Screenplay.for_CritterStackScreenplayGenerator;

public class when_generating_legacy_wolverine_saga_evidence : given.a_legacy_wolverine_saga_application
{
    GeneratedScreenplayDefinition _result = null!;

    void Because() => _result = new CritterStackScreenplayGenerator().Generate(
        [Project],
        new CritterStackScreenplayOptions { Domain = "LegacyOrders" });

    [Fact] void should_succeed() => _result.IsSuccess.ShouldBeTrue();
    [Fact] void should_admit_the_source_compatible_legacy_saga() => ArtifactNames(ArtifactKind.Saga).ShouldContainOnly(["LegacyWorkflow"]);
    [Fact] void should_admit_all_legacy_role_spellings_and_async_twins() => SagaHandles.Count.ShouldEqual(22);
    [Fact] void should_preserve_legacy_start_roles() => SagaHandles.Count(_ => _.Key.Discriminator == "wolverine:saga:start").ShouldEqual(4);
    [Fact] void should_preserve_legacy_start_or_handle_roles() => SagaHandles.Count(_ => _.Key.Discriminator == "wolverine:saga:start-or-handle").ShouldEqual(4);
    [Fact] void should_preserve_legacy_existing_roles() => SagaHandles.Count(_ => _.Key.Discriminator == "wolverine:saga:orchestrate").ShouldEqual(12);
    [Fact] void should_preserve_legacy_not_found_roles() => SagaHandles.Count(_ => _.Key.Discriminator == "wolverine:saga:not-found").ShouldEqual(2);
    [Fact] void should_use_exact_legacy_identity_evidence() => SagaHandles.All(_ => _.Definitions.Single().TargetMember == "workflowId" && _.Evidence.Single().Strength == EvidenceStrength.Exact).ShouldBeTrue();
    [Fact] void should_keep_each_legacy_method_as_one_handler() => SagaHandles.Select(_ => _.Key.Source).Distinct().Count().ShouldEqual(22);
    [Fact] void should_not_create_a_message_or_event_for_the_returned_legacy_state() => ArtifactNames(ArtifactKind.Message).Contains("LegacyWorkflow", StringComparer.Ordinal).ShouldBeFalse();
    [Fact] void should_report_one_located_legacy_workflow_loss() => _result.Diagnostics.Count(_ => _.Code == WolverineDiagnosticCodes.SagaWorkflowOmitted && _.Source?.Path == "LegacyOrders/Sagas.cs").ShouldEqual(1);
    [Fact] void should_not_report_runtime_correlation_for_exact_legacy_identity() => _result.Diagnostics.Any(_ => _.Code == WolverineDiagnosticCodes.SagaCorrelationRuntime).ShouldBeFalse();

    IReadOnlyList<ResolvedRelationship> SagaHandles =>
    [
        .. _result.Graph.Relationships.Where(_ =>
            _.Key.Kind == RelationshipKind.Handles &&
            _.Key.Discriminator?.StartsWith("wolverine:saga:", StringComparison.Ordinal) == true)
    ];

    IReadOnlyList<string> ArtifactNames(ArtifactKind kind) =>
    [
        .. _result.Graph.Artifacts
            .Where(_ => _.Key.Kind == kind)
            .SelectMany(_ => _.Variants)
            .Select(_ => _.Definition.Name)
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal)
    ];
}
