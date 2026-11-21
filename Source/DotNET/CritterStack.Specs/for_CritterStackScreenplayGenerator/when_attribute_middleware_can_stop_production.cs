// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Cratis.CritterStack.Screenplay.for_CritterStackScreenplayGenerator;

public class when_attribute_middleware_can_stop_production : given.a_wolverine_attribute_middleware_application
{
    const string Source =
        """
        namespace StoppingMiddleware;
        public record Process(System.Guid Id, bool Allowed);
        public record Processed(System.Guid Id);
        [Wolverine.Attributes.Middleware(typeof(AccessCheck))]
        public static class ProcessEndpoint
        {
            [Wolverine.Http.WolverinePost("/process")]
            public static void Post(Process message, Marten.IDocumentSession session) =>
                session.Events.Append(message.Id, new Processed(message.Id));
        }
        public static class AccessCheck
        {
            [Wolverine.Attributes.WolverineBefore]
            public static Wolverine.HandlerContinuation Check(Process message) =>
                message.Allowed ? Wolverine.HandlerContinuation.Continue : Wolverine.HandlerContinuation.Stop;
        }
        """;

    DotNetProjectCompilation _project = null!;
    GeneratedScreenplayDefinition _result = null!;

    void Establish() => _project = CreateProject(Source);
    void Because() => _result = new CritterStackScreenplayGenerator().Generate([_project], new CritterStackScreenplayOptions { Domain = "StoppingMiddleware" });

    [Fact] void should_compile_the_fixture() => _project.Compilation.GetDiagnostics(TestContext.Current.CancellationToken).Where(_ => _.Severity == DiagnosticSeverity.Error).ShouldBeEmpty();
    [Fact] void should_generate_a_compiler_verified_candidate() => _result.IsSuccess.ShouldBeTrue();
    [Fact] void should_not_claim_unconditional_production() => _result.Source.ShouldNotContain("produces Processed");
    [Fact] void should_report_the_compound_production_condition() => _result.Diagnostics.Where(_ => _.Code == WolverineDiagnosticCodes.ProductionLinkOmitted).ShouldNotBeEmpty();
    [Fact] void should_report_the_before_stage_at_its_application() => _result.Diagnostics.Single(_ => _.Code == WolverineDiagnosticCodes.CompoundStageOmitted).Source.StartLine.ShouldEqual(4);
}
