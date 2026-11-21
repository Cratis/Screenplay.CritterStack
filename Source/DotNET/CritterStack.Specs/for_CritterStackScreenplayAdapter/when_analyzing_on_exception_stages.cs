// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Cratis.CritterStack.Screenplay.for_CritterStackScreenplayAdapter;

public class when_analyzing_on_exception_stages : given.an_on_exception_handler_application
{
    ResolvedApplicationGraph _graph = null!;

    void Because() => _graph = new GenerationResolver().Resolve([Contribution]);

    [Fact] void should_compile_the_fixture() => Project.Compilation.GetDiagnostics(TestContext.Current.CancellationToken).Where(_ => _.Severity == DiagnosticSeverity.Error).ShouldBeEmpty();
    [Fact] void should_keep_the_success_cascade() => Cascade("OrderShipped").Definition.Key.Discriminator.ShouldEqual("immediate");
    [Fact] void should_lower_the_outgoing_failure_message() => Cascade("ShipmentFailed").Definition.Key.Discriminator.ShouldEqual("failure:System.InvalidOperationException");
    [Fact] void should_lower_the_returned_failure_message_of_the_exception_first_overload() => Cascade("ShipmentTimedOut").Definition.Key.Discriminator.ShouldEqual("failure:System.TimeoutException");
    [Fact] void should_lower_the_returned_failure_message_of_the_dependency_first_async_hook() => Cascade("ShipmentRejected").Definition.Key.Discriminator.ShouldEqual("failure:System.ArgumentException");
    [Fact] void should_own_failure_messages_by_the_handler() => Cascade("ShipmentFailed").Definition.Key.Source.ShouldEqual(Cascade("OrderShipped").Definition.Key.Source);
    [Fact] void should_not_link_failure_messages_as_success_cascades() => FailureCascades.ShouldEachConformTo(_ => _.Definition.Key.Discriminator!.StartsWith("failure:", StringComparison.Ordinal) && _.Id.Value.StartsWith("wolverine:cascades:failure:", StringComparison.Ordinal));
    [Fact] void should_lower_one_cascade_per_failure_hook() => FailureCascades.Count.ShouldEqual(3);
    [Fact] void should_not_match_a_hook_without_an_exception() => ArtifactNames(ArtifactKind.Message).ShouldNotContain("ShipmentNoted");
    [Fact] void should_not_match_an_unrelated_method() => ArtifactNames(ArtifactKind.Message).ShouldNotContain("ShipmentCompensated");
    [Fact] void should_not_match_a_hook_for_another_message() => ArtifactNames(ArtifactKind.Message).ShouldNotContain("CancellationFailed");
    [Fact] void should_report_one_condition_loss_per_failure_hook() => FailureDiagnostics.Count.ShouldEqual(3);
    [Fact] void should_locate_the_loss_on_the_invalid_operation_hook() => FailureDiagnostic("InvalidOperationException").Source.StartLine.ShouldEqual(16);
    [Fact] void should_locate_the_loss_on_the_timeout_hook() => FailureDiagnostic("TimeoutException").Source.StartLine.ShouldEqual(19);
    [Fact] void should_locate_the_loss_in_the_handler_file() => FailureDiagnostics.ShouldEachConformTo(_ => _.Source.Path == "OnExceptionHandlers/Handlers.cs");
    [Fact] void should_name_the_failure_message() => FailureDiagnostic("InvalidOperationException").Message.ShouldContain("'ShipmentFailed' is sent only on failure");
    [Fact] void should_attach_the_loss_to_the_owning_handler() => FailureDiagnostics.ShouldEachConformTo(_ => _.Subject == Cascade("OrderShipped").Definition.Key.Source);
    [Fact] void should_classify_the_loss_as_an_unsupported_warning() => FailureDiagnostics.ShouldEachConformTo(_ => _.Severity == GenerationDiagnosticSeverity.Warning && _.Outcome == GenerationDiagnosticOutcome.Unsupported);
    [Fact] void should_not_report_failure_hooks_as_compound_stages() => Contribution.Diagnostics.Any(_ => _.Code == WolverineDiagnosticCodes.CompoundStageOmitted).ShouldBeFalse();

    IReadOnlyList<RelationshipFact> FailureCascades =>
        [.. Contribution.Facts.OfType<RelationshipFact>().Where(_ => _.Definition.Key.Kind == RelationshipKind.Cascades && RelationshipTarget(_) != "OrderShipped")];

    IReadOnlyList<GenerationDiagnostic> FailureDiagnostics =>
        [.. Contribution.Diagnostics.Where(_ => _.Code == WolverineDiagnosticCodes.FailureStageConditionOmitted)];

    GenerationDiagnostic FailureDiagnostic(string exceptionName) =>
        FailureDiagnostics.Single(_ => _.Message.Contains($"'{exceptionName}'", StringComparison.Ordinal));

    RelationshipFact Cascade(string target) =>
        Contribution.Facts.OfType<RelationshipFact>().Single(_ => _.Definition.Key.Kind == RelationshipKind.Cascades && RelationshipTarget(_) == target);

    IReadOnlyList<string> ArtifactNames(ArtifactKind kind) =>
        [.. _graph.Artifacts.Where(_ => _.Key.Kind == kind).Select(_ => _.Variants.Single().Definition.Name)];

    string RelationshipTarget(RelationshipFact relationship) =>
        _graph.Artifacts.Single(_ => _.Key.Subject == relationship.Definition.Key.Target).Variants.Single().Definition.Name;
}
