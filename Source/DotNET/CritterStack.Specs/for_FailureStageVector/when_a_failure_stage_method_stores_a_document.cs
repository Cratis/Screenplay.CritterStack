// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Cratis.CritterStack.Screenplay.for_FailureStageVector;

/// <summary>
/// Facade characterization of failure-only method eligibility: the facade suppresses Marten document and
/// Stores facts for Wolverine failure-stage methods, which the standalone Marten adapter cannot know.
/// The neutral-fact mapping that would replace it is recorded in the cycle vector mapping.
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

    void Establish() => _project = CreateProject(Source);

    void Because()
    {
        _facade = Analyze(_project);
        _marten = new MartenScreenplayAdapter().Analyze(new DotNetAnalysisContext([_project]), new DotNetAdapterOptions());
        _result = new CritterStackScreenplayGenerator().Generate([_project], new CritterStackScreenplayOptions { Domain = "FailureVector" });
    }

    [Fact] void should_compile_the_fixture() => _project.Compilation.GetDiagnostics(TestContext.Current.CancellationToken).Where(_ => _.Severity == DiagnosticSeverity.Error).ShouldBeEmpty();
    [Fact] void should_generate_a_compiler_verified_candidate() => _result.IsSuccess.ShouldBeTrue();
    [Fact] void should_keep_the_success_document_in_the_facade() => StoreTargets(_facade).Count(_ => _.EndsWith("/FailureVector.SuccessDocument", StringComparison.Ordinal)).ShouldEqual(1);
    [Fact] void should_not_store_the_failure_document_in_the_facade() => StoreTargets(_facade).Any(_ => _.EndsWith("/FailureVector.FailureDocument", StringComparison.Ordinal)).ShouldBeFalse();
    [Fact] void should_not_declare_the_failure_document_in_the_facade() => _facade.Facts.OfType<ArtifactFact>().Any(_ => _.Definition.Key.Subject.Value.Contains("FailureDocument", StringComparison.Ordinal)).ShouldBeFalse();
    [Fact] void should_not_print_the_failure_document() => _result.Source.ShouldNotContain("FailureDocument");
    [Fact] void should_store_the_failure_document_in_the_standalone_marten_adapter_which_cannot_know_the_stage() => StoreTargets(_marten).Any(_ => _.EndsWith("/FailureVector.FailureDocument", StringComparison.Ordinal)).ShouldBeTrue();

    static IEnumerable<string> StoreTargets(AdapterContribution contribution) => contribution.Facts.OfType<RelationshipFact>()
        .Where(_ => _.Definition.Key.Kind == RelationshipKind.Stores)
        .Select(_ => _.Definition.Key.Target.Value);
}
