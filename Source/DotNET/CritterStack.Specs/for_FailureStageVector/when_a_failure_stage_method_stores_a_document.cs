// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Cratis.CritterStack.Screenplay.for_FailureStageVector;

/// <summary>
/// Native Wolverine classifications suppress failure-only Marten declarations and stores through shared
/// method eligibility, matching the compatibility facade without inspecting its completed facts.
/// </summary>
public class when_a_failure_stage_method_stores_a_document : given.a_wolverine_attribute_middleware_application
{
    const string Source =
        """
        namespace FailureVector;
        public record Process(System.Guid Id);
        public class FailureDocument { public System.Guid Id { get; set; } }
        public class SuccessDocument { public System.Guid Id { get; set; } }
        public static class ProcessHandler
        {
            public static void Handle(Process message, Marten.IDocumentSession session) => session.Store(new SuccessDocument { Id = message.Id });
            public static void OnException(Process message, System.InvalidOperationException exception, Marten.IDocumentSession session) =>
                session.Store(new FailureDocument { Id = message.Id });
        }
        """;

    DotNetProjectCompilation _project = null!;
    AdapterContribution _facade = null!;
    AdapterContribution _marten = null!;
    GeneratedScreenplayDefinition _result = null!;
    GeneratedScreenplayDefinition _native = null!;
    AdapterContribution _failures = null!;

    void Establish() => _project = CreateProject(Source);

    void Because()
    {
        _facade = Analyze(_project);
        _marten = new MartenScreenplayAdapter().Analyze(new DotNetAnalysisContext([_project]), new DotNetAdapterOptions());
        _result = new CritterStackScreenplayGenerator().Generate([_project], new CritterStackScreenplayOptions { Domain = "FailureVector" });
        var adapter = new WolverineScreenplayAdapter().Identity;
        var type = _project.Compilation.GetTypeByMetadataName("FailureVector.ProcessHandler");
        var handler = type.GetMembers("Handle").OfType<IMethodSymbol>().Single();
        var request = (INamedTypeSymbol)handler.Parameters[0].Type;
        var owner = WolverineBehaviorIdentity.ForMethod(_project, handler, "reaction");
        var failures = WolverineCompoundStages.StagesFor(handler, request, new WolverineCompoundStageSource(_project, new([])))
            .Where(_ => _.IsFailurePath)
            .Select(stage => WolverineFacts.ObserveFailureStage(_project, adapter, owner, stage))
            .ToList();
        _failures = new()
        {
            Adapter = adapter,
            Facts = [.. failures.Select(failure => WolverineFacts.ClassifyFailureStage(_project, failure))],
            Diagnostics = [.. failures.Select(_ => _.OmissionReason)]
        };
        _native = new ScreenplayDefinitionGenerator().Generate(
            [_marten, _failures],
            new ScreenplayGenerationOptions { Domain = "FailureVector" });
    }

    [Fact] void should_compile_the_fixture() => _project.Compilation.GetDiagnostics(TestContext.Current.CancellationToken).Where(_ => _.Severity == DiagnosticSeverity.Error).ShouldBeEmpty();
    [Fact] void should_generate_a_compiler_verified_candidate() => _result.IsSuccess.ShouldBeTrue();
    [Fact] void should_keep_the_success_document_in_the_facade() => StoreTargets(_facade).Count(_ => _.EndsWith("/FailureVector.SuccessDocument", StringComparison.Ordinal)).ShouldEqual(1);
    [Fact] void should_not_store_the_failure_document_in_the_facade() => StoreTargets(_facade).Any(_ => _.EndsWith("/FailureVector.FailureDocument", StringComparison.Ordinal)).ShouldBeFalse();
    [Fact] void should_not_declare_the_failure_document_in_the_facade() => _facade.Facts.OfType<ArtifactFact>().Any(_ => _.Definition.Key.Subject.Value.Contains("FailureDocument", StringComparison.Ordinal)).ShouldBeFalse();
    [Fact] void should_not_print_the_failure_document() => _result.Source.ShouldNotContain("FailureDocument");
    [Fact] void should_store_the_failure_document_in_the_standalone_marten_adapter_which_cannot_know_the_stage() => StoreTargets(_marten).Any(_ => _.EndsWith("/FailureVector.FailureDocument", StringComparison.Ordinal)).ShouldBeTrue();

    [Fact] void should_keep_the_native_success_document_in_the_effective_graph() => _native.Graph.Artifacts.Any(_ => _.Key.Subject.Value.EndsWith("/FailureVector.SuccessDocument", StringComparison.Ordinal)).ShouldBeTrue();
    [Fact] void should_omit_the_native_failure_only_document_from_the_effective_graph() => _native.Graph.Artifacts.Any(_ => _.Key.Subject.Value.EndsWith("/FailureVector.FailureDocument", StringComparison.Ordinal)).ShouldBeFalse();
    [Fact] void should_keep_the_discovered_failure_warning_on_the_native_snapshot() => _native.Diagnostics.Any(_ => _.Code == WolverineDiagnosticCodes.FailureStageConditionOmitted).ShouldBeTrue();
    [Fact] void should_discover_the_same_failure_warning_from_source() => _failures.Diagnostics.Single().Message.ShouldEqual(_facade.Diagnostics.Single(_ => _.Code == WolverineDiagnosticCodes.FailureStageConditionOmitted).Message);
    [Fact] void should_preserve_the_failure_warning_subject_in_the_native_discovery() => _failures.Diagnostics.Single().Subject.ShouldEqual(_facade.Diagnostics.Single(_ => _.Code == WolverineDiagnosticCodes.FailureStageConditionOmitted).Subject);
    [Fact] void should_not_report_invalid_or_conflicting_eligibility_metadata() => _native.Diagnostics.Where(_ => _.Outcome == GenerationDiagnosticOutcome.Conflict || _.Outcome == GenerationDiagnosticOutcome.Unknown).ShouldBeEmpty();

    static IEnumerable<string> StoreTargets(AdapterContribution contribution) => contribution.Facts.OfType<RelationshipFact>()
        .Where(_ => _.Definition.Key.Kind == RelationshipKind.Stores)
        .Select(_ => _.Definition.Key.Target.Value);
}
