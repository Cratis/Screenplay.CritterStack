// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Cratis.CritterStack.Screenplay.for_CritterStackScreenplayAdapter;

public class when_failure_stage_returns_an_event_tag_query : given.a_wolverine_attribute_middleware_application
{
    const string Source =
        """
        namespace JasperFx.Events.Tags { public class EventTagQuery; }
        namespace FailureQueries
        {
            public record Process(System.Guid Id);
            public record Succeeded(System.Guid Id);
            public record Failed(System.Guid Id);
            public static class ProcessHandler
            {
                public static Succeeded Handle(Process message) => new(message.Id);
                public static (JasperFx.Events.Tags.EventTagQuery, Failed) OnException(Process message, System.InvalidOperationException exception) =>
                    (new(), new(message.Id));
            }
        }
        """;

    DotNetProjectCompilation _project = null!;
    AdapterContribution _contribution = null!;

    void Establish() => _project = CreateProject(Source);
    void Because() => _contribution = Analyze(_project);

    [Fact] void should_compile_the_fixture() => _project.Compilation.GetDiagnostics(TestContext.Current.CancellationToken).Where(_ => _.Severity == DiagnosticSeverity.Error).ShouldBeEmpty();
    [Fact] void should_name_the_real_failure_message() => FailureDiagnostic.Message.ShouldContain("'Failed'");
    [Fact] void should_not_describe_query_metadata_as_a_cascaded_message() => FailureDiagnostic.Message.ShouldNotContain("EventTagQuery");
    [Fact] void should_not_emit_query_metadata_as_an_artifact() => _contribution.Facts.OfType<ArtifactFact>().Where(_ => _.Definition.Name == "EventTagQuery").ShouldBeEmpty();
    [Fact] void should_not_emit_a_normal_failure_cascade() => _contribution.Facts.OfType<RelationshipFact>().Where(_ => _.Definition.Key.Kind == RelationshipKind.Cascades && _.Definition.Key.Target.Value.EndsWith("/FailureQueries.Failed", StringComparison.Ordinal)).ShouldBeEmpty();
    [Fact] void should_retain_the_success_cascade() => _contribution.Facts.OfType<RelationshipFact>().Count(_ => _.Definition.Key.Kind == RelationshipKind.Cascades && _.Definition.Key.Target.Value.EndsWith("/FailureQueries.Succeeded", StringComparison.Ordinal)).ShouldEqual(1);

    GenerationDiagnostic FailureDiagnostic => _contribution.Diagnostics.Single(_ => _.Code == WolverineDiagnosticCodes.FailureStageConditionOmitted);
}
