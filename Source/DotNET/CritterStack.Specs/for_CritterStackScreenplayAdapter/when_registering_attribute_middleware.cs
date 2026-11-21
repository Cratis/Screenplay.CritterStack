// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Cratis.CritterStack.Screenplay.for_CritterStackScreenplayAdapter;

public class when_registering_attribute_middleware : given.a_wolverine_attribute_middleware_application
{
    const string Source =
        """
        namespace RegisteredMiddleware;
        public record Process(string Id);
        public record Create(string Id);
        public record FilteredMessage(string Id);
        public static class ProcessHandler { public static void Handle(Process message) { } }
        public static class CreateEndpoint
        {
            [Wolverine.Http.WolverinePost("/create")]
            public static void Post(Create message) { }
        }
        public static class Registered { public static void Before(Process message) { } }
        public class Generic { public static void Before(Process message) { } }
        public static class HttpRegistered { public static void Before(Create message) { } }
        public static class Filtered { public static FilteredMessage Before(Process message) => new(message.Id); }
        public static class Configuration
        {
            public static void Configure(Wolverine.WolverineOptions options, Wolverine.Http.WolverineHttpOptions http)
            {
                options.Policies.AddMiddleware(typeof(Registered));
                options.Policies.AddMiddleware<Generic>();
                http.AddMiddleware(typeof(HttpRegistered));
                options.Policies.AddMiddleware(typeof(Filtered), filter: _ => true);
                if (System.DateTime.UtcNow.Ticks > 0) options.Policies.AddMiddleware(typeof(Filtered));
                System.Type selected = typeof(Filtered);
                options.Policies.AddMiddleware(selected);
                new LookalikePolicies().AddMiddleware(typeof(Filtered));
                options?.Policies.AddMiddleware(typeof(Filtered));
            }
        }
        public class LookalikePolicies { public void AddMiddleware(System.Type middlewareType) { } }
        """;

    DotNetProjectCompilation _project = null!;
    AdapterContribution _contribution = null!;

    void Establish() => _project = CreateProject(Source);
    void Because() => _contribution = Analyze(_project);

    [Fact] void should_compile_the_fixture() => _project.Compilation.GetDiagnostics(TestContext.Current.CancellationToken).Where(_ => _.Severity == DiagnosticSeverity.Error).ShouldBeEmpty();
    [Fact] void should_apply_each_resolved_registration_to_its_own_chain_kind() => Stages.Count.ShouldEqual(3);
    [Fact] void should_locate_handler_middleware_at_its_registration() => Stages.Count(_ => _.Source.StartLine == 19).ShouldEqual(1);
    [Fact] void should_locate_generic_middleware_at_its_registration() => Stages.Count(_ => _.Source.StartLine == 20).ShouldEqual(1);
    [Fact] void should_locate_http_middleware_at_its_registration() => Stages.Count(_ => _.Source.StartLine == 21).ShouldEqual(1);
    [Fact] void should_report_all_runtime_registrations() => Unresolved.Count.ShouldEqual(4);
    [Fact] void should_locate_runtime_registration_diagnostics() => Unresolved.Select(_ => _.Source.StartLine).ShouldContainOnly(22, 23, 25, 27);
    [Fact] void should_not_invent_runtime_filtered_consequences() => _contribution.Facts.OfType<RelationshipFact>().Where(_ => _.Definition.Key.Kind == RelationshipKind.Cascades).ShouldBeEmpty();
    [Fact] void should_classify_runtime_registrations_as_unknown() => Unresolved.ShouldEachConformTo(_ => _.Severity == GenerationDiagnosticSeverity.Warning && _.Outcome == GenerationDiagnosticOutcome.Unknown);

    IReadOnlyList<GenerationDiagnostic> Stages => [.. _contribution.Diagnostics.Where(_ => _.Code == WolverineDiagnosticCodes.CompoundStageOmitted)];
    IReadOnlyList<GenerationDiagnostic> Unresolved => [.. _contribution.Diagnostics.Where(_ => _.Code == WolverineDiagnosticCodes.HandlerDiscoveryConfigurationUnresolved)];
}
