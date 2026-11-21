// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Cratis.CritterStack.Screenplay.for_CritterStackScreenplayGenerator;

public class when_failure_stage_printed_model_has_bus_and_storage_effects : given.a_wolverine_attribute_middleware_application
{
    const string Source =
        """
        namespace FailureEffects;
        public record Process(System.Guid Id);
        public record Succeeded(System.Guid Id);
        public record FailureReturned(System.Guid Id);
        public record FailureSent(System.Guid Id);
        public record FailurePublished(System.Guid Id);
        public record FailureAppended(System.Guid Id);
        public class FailureDocument { public System.Guid Id { get; set; } }
        public static class ProcessHandler
        {
            public static Succeeded Handle(Process message) => new(message.Id);
            public static Wolverine.OutgoingMessages OnException(Process message, System.InvalidOperationException exception, Wolverine.IMessageBus bus, Marten.IDocumentSession session)
            {
                session.Store(new FailureDocument { Id = message.Id });
                session.Events.Append(message.Id, new FailureAppended(message.Id));
                _ = bus.SendAsync(new FailureSent(message.Id));
                _ = bus.PublishAsync(new FailurePublished(message.Id));
                return [new FailureReturned(message.Id)];
            }
        }
        """;

    DotNetProjectCompilation _project = null!;
    AdapterContribution _contribution = null!;
    GeneratedScreenplayDefinition _result = null!;

    void Establish() => _project = CreateProject(Source);

    void Because()
    {
        _contribution = Analyze(_project);
        _result = new CritterStackScreenplayGenerator().Generate([_project], new CritterStackScreenplayOptions { Domain = "FailureEffects" });
    }

    [Fact] void should_compile_the_fixture() => _project.Compilation.GetDiagnostics(TestContext.Current.CancellationToken).Where(_ => _.Severity == DiagnosticSeverity.Error).ShouldBeEmpty();
    [Fact] void should_generate_a_compiler_verified_candidate() => _result.IsSuccess.ShouldBeTrue();
    [Fact] void should_keep_the_success_message() => _result.Source.ShouldContain("Succeeded");
    [Fact] void should_not_print_failure_return_values() => _result.Source.ShouldNotContain("FailureReturned");
    [Fact] void should_not_print_failure_only_sends() => _result.Source.ShouldNotContain("FailureSent");
    [Fact] void should_not_print_failure_only_publications() => _result.Source.ShouldNotContain("FailurePublished");
    [Fact] void should_not_print_failure_only_documents() => _result.Source.ShouldNotContain("FailureDocument");
    [Fact] void should_not_print_failure_only_events() => _result.Source.ShouldNotContain("FailureAppended");
    [Fact] void should_not_emit_failure_storage_or_production_facts() => _contribution.Facts.OfType<RelationshipFact>().Where(_ => _.Definition.Key.Kind is RelationshipKind.Stores or RelationshipKind.Produces or RelationshipKind.Appends).ShouldBeEmpty();
    [Fact] void should_not_emit_failure_cascades() => _contribution.Facts.OfType<RelationshipFact>().Count(_ => _.Definition.Key.Kind == RelationshipKind.Cascades).ShouldEqual(1);
    [Fact] void should_name_the_failure_only_returned_message() => FailureDiagnostic.Message.ShouldContain("'FailureReturned'");
    [Fact] void should_name_the_failure_only_sent_message() => FailureDiagnostic.Message.ShouldContain("'FailureSent'");
    [Fact] void should_name_the_failure_only_published_message() => FailureDiagnostic.Message.ShouldContain("'FailurePublished'");
    [Fact] void should_keep_the_loss_located_at_the_failure_hook() => FailureDiagnostic.Source.StartLine.ShouldEqual(12);
    [Fact] void should_keep_the_loss_attached_to_the_owning_handler() => FailureDiagnostic.Subject.ShouldEqual(_contribution.Facts.OfType<ArtifactFact>().Single(_ => _.Definition.Key.Kind == ArtifactKind.Reaction).Definition.Key.Subject);

    GenerationDiagnostic FailureDiagnostic => _contribution.Diagnostics.Single(_ => _.Code == WolverineDiagnosticCodes.FailureStageConditionOmitted);
}
