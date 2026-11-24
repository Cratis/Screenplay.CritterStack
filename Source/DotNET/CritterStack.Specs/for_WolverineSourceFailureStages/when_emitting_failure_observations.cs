// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Cratis.CritterStack.Screenplay.for_WolverineSourceFailureStages;

public class when_emitting_failure_observations : given.an_on_exception_handler_application
{
    readonly AdapterIdentity _adapter = new WolverineScreenplayAdapter().Identity;
    readonly BackendScopeKey _scope = new() { Store = new() { Value = "dotnet://OnExceptionHandlers/store" } };
    IReadOnlyList<WolverineFailureStageObservation> _observations = [];
    IReadOnlyList<MethodExecutionClassificationFact> _classifications = [];
    IReadOnlyList<GenerationDiagnostic> _expected = [];
    IReadOnlyList<WolverineCompoundStage> _stages = [];
    MethodExecutionClassificationFact _scoped = null!;

    void Because()
    {
        var handlerType = Project.Compilation.GetTypeByMetadataName("OnExceptionHandlers.ShipOrderHandler");
        var handler = handlerType.GetMembers("Handle").OfType<IMethodSymbol>().Single();
        var request = Project.Compilation.GetTypeByMetadataName("OnExceptionHandlers.ShipOrder");
        _stages = WolverineCompoundStages.StagesFor(handler, request, new WolverineCompoundStageSource(Project, new([])));
        var owner = WolverineBehaviorIdentity.ForMethod(Project, handler, "reaction");
        _observations = [.. _stages.Where(_ => _.IsFailurePath).Select(stage => WolverineFacts.ObserveFailureStage(
            Project, _adapter, owner, stage))];
        _classifications = [.. _observations.Select(observation => WolverineFacts.ClassifyFailureStage(Project, observation))];
        _scoped = WolverineFacts.ClassifyFailureStage(Project, _observations[0], _scope);
        _expected = [.. Contribution.Diagnostics.Where(_ => _.Code == WolverineDiagnosticCodes.FailureStageConditionOmitted)];
    }

    [Fact] void should_observe_all_three_applicable_failure_method_overloads() => _observations.Count.ShouldEqual(3);
    [Fact] void should_keep_similarly_named_methods_out_of_failure_observations() => _observations.Any(_ => _.Method.Name == "OnExceptionally").ShouldBeFalse();
    [Fact] void should_include_async_failure_stages_in_the_shared_source_roster() => _stages.Any(_ => _.Method.Name == "OnExceptionAsync").ShouldBeTrue();
    [Fact] void should_preserve_the_complete_compatibility_diagnostic_transcript() => Transcript(_observations.Select(_ => _.OmissionReason)).ShouldEqual(Transcript(_expected));
    [Fact] void should_keep_exact_method_evidence() => _observations.ShouldEachConformTo(_ => _.Evidence.Strength == EvidenceStrength.Exact && _.Evidence.Source is not null);
    [Fact] void should_keep_the_native_producer_on_the_source_evidence() => _observations.ShouldEachConformTo(_ => _.Evidence.Adapter == _adapter);
    [Fact] void should_classify_only_failure_paths() => _classifications.ShouldEachConformTo(_ => _.Definition.Kind == MethodExecutionKind.FailureOnly);
    [Fact] void should_classify_each_overload_separately() => _classifications.Select(_ => _.Definition.Method).Distinct().Count().ShouldEqual(3);
    [Fact] void should_target_the_exact_source_method() => _classifications[0].Definition.Method.ShouldEqual(CritterStackMethodFacts.SubjectFor(Project, _observations[0].Method));
    [Fact] void should_keep_the_located_warning_on_the_classification() => _classifications[0].Definition.OmissionReason.ShouldEqual(_observations[0].OmissionReason);
    [Fact] void should_keep_the_backend_occurrence_when_classifying() => _scoped.Definition.Scope.ShouldEqual(_scope);
    [Fact] void should_not_collide_scoped_and_unscoped_classifications() => _scoped.Id.ShouldNotEqual(_classifications[0].Id);

    static string Transcript(IEnumerable<GenerationDiagnostic> diagnostics) => System.Text.Json.JsonSerializer.Serialize(diagnostics.OrderBy(_ => _.Message, StringComparer.Ordinal));
}
