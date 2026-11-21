// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Cratis.CritterStack.Screenplay.for_CritterStackScreenplayAdapter;

public class when_excluding_recognized_failure_stage_document_usage : given.a_wolverine_attribute_middleware_application
{
    const string Source =
        """
        namespace FailureDocuments;
        public record Process(System.Guid Id);
        public record Succeeded(System.Guid Id);
        public class FailureDocument { public System.Guid Id { get; set; } }
        public class NestedFailureDocument { public System.Guid Id { get; set; } }
        public class OrdinaryDocument { public System.Guid Id { get; set; } }
        [Wolverine.Attributes.Middleware(typeof(FailureMiddleware))]
        public static class ProcessHandler
        {
            public static Succeeded Handle(Process message) => new(message.Id);
        }
        public static class FailureMiddleware
        {
            public static void OnException(Process message, System.InvalidOperationException exception, Marten.IDocumentSession session)
            {
                session.Store(new FailureDocument { Id = message.Id });
                System.Action store = () => session.Store(new NestedFailureDocument { Id = message.Id });
                store();
            }
        }
        public class OrdinaryService
        {
            public void OnException(Process message, System.InvalidOperationException exception, Marten.IDocumentSession session) =>
                session.Store(new OrdinaryDocument { Id = message.Id });
        }
        """;

    DotNetProjectCompilation _project = null!;
    AdapterContribution _contribution = null!;

    void Establish() => _project = CreateProject(Source);
    void Because() => _contribution = Analyze(_project);

    [Fact] void should_compile_the_fixture() => _project.Compilation.GetDiagnostics(TestContext.Current.CancellationToken).Where(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error).ShouldBeEmpty();
    [Fact] void should_recognize_the_registered_failure_stage() => _contribution.Diagnostics.Count(diagnostic => diagnostic.Code == WolverineDiagnosticCodes.FailureStageConditionOmitted).ShouldEqual(1);
    [Fact] void should_not_emit_the_failure_stage_document() => DocumentNames.ShouldNotContain("FailureDocument");
    [Fact] void should_not_emit_the_nested_failure_stage_document() => DocumentNames.ShouldNotContain("NestedFailureDocument");
    [Fact] void should_preserve_an_unbound_method_with_the_same_name() => DocumentNames.ShouldContain("OrdinaryDocument");
    [Fact] void should_emit_only_the_unbound_method_store() => _contribution.Facts.OfType<RelationshipFact>().Count(fact => fact.Definition.Key.Kind == RelationshipKind.Stores).ShouldEqual(1);

    IEnumerable<string> DocumentNames => _contribution.Facts.OfType<ArtifactFact>().Where(fact => fact.Definition.Key.Kind == ArtifactKind.Document).Select(fact => fact.Definition.Name);
}
