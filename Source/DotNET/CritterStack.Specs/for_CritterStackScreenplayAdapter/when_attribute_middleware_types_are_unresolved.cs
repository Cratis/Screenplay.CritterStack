// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Cratis.CritterStack.Screenplay.for_CritterStackScreenplayAdapter;

public class when_attribute_middleware_types_are_unresolved : given.a_wolverine_attribute_middleware_application
{
    const string Source =
        """
        namespace UnknownMiddleware;
        public record Process(string Id);
        public record NotProven(string Id);
        [Wolverine.Attributes.Middleware(null)]
        [Wolverine.Attributes.Middleware(typeof(Shared), null)]
        public static class ProcessHandler { public static void Handle(Process message) { } }
        public static class Shared { public static NotProven Before(Process message) => new(message.Id); }
        """;

    DotNetProjectCompilation _project = null!;
    AdapterContribution _contribution = null!;

    void Establish() => _project = CreateProject(Source);
    void Because() => _contribution = Analyze(_project);

    [Fact] void should_compile_the_fixture() => _project.Compilation.GetDiagnostics(TestContext.Current.CancellationToken).Where(_ => _.Severity == DiagnosticSeverity.Error).ShouldBeEmpty();
    [Fact] void should_report_each_unresolved_attribute() => Unresolved.Count.ShouldEqual(2);
    [Fact] void should_locate_each_loss_at_the_attribute() => Unresolved.Select(_ => _.Source.StartLine).ShouldContainOnly(4, 5);
    [Fact] void should_not_apply_a_partially_resolved_middleware_list() => _contribution.Diagnostics.Where(_ => _.Code == WolverineDiagnosticCodes.CompoundStageOmitted).ShouldBeEmpty();
    [Fact] void should_not_invent_a_stage_return_consequence() => _contribution.Facts.OfType<RelationshipFact>().Where(_ => _.Definition.Key.Kind == RelationshipKind.Cascades).ShouldBeEmpty();

    IReadOnlyList<GenerationDiagnostic> Unresolved => [.. _contribution.Diagnostics.Where(_ => _.Code == WolverineDiagnosticCodes.HandlerDiscoveryConfigurationUnresolved)];
}
