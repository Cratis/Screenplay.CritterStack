// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Cratis.CritterStack.Screenplay.for_CritterStackScreenplayAdapter;

public class when_excluding_grpc_scoped_middleware : given.a_wolverine_attribute_middleware_application
{
    const string Source =
        """
        using Wolverine.Attributes;
        namespace GrpcMiddleware;
        public record Process(System.Guid Id);
        public record Create(System.Guid Id);
        public record Processed(System.Guid Id);
        public record Created(System.Guid Id);
        public record GrpcMessage;
        [Middleware(typeof(Shared), Scoping = MiddlewareScoping.Grpc)]
        public static class ProcessHandler
        {
            public static void Handle(Process message, Marten.IDocumentSession session) =>
                session.Events.Append(message.Id, new Processed(message.Id));
            [WolverineBefore(MiddlewareScoping.Grpc)]
            public static GrpcMessage Before() => new();
        }
        [Middleware(typeof(Shared), Scoping = MiddlewareScoping.Grpc)]
        public static class CreateEndpoint
        {
            [Wolverine.Http.WolverinePost("/create")]
            public static void Post(Create message, Marten.IDocumentSession session) =>
                session.Events.Append(message.Id, new Created(message.Id));
            [WolverineAfter(Scoping = MiddlewareScoping.Grpc)]
            public static GrpcMessage After() => new();
        }
        public static class Shared
        {
            [WolverineBefore]
            public static GrpcMessage Prepare() => new();
        }
        """;

    DotNetProjectCompilation _project = null!;
    AdapterContribution _contribution = null!;

    void Establish() => _project = CreateProject(Source);

    void Because() => _contribution = Analyze(_project);

    [Fact] void should_compile_the_fixture() => _project.Compilation.GetDiagnostics(TestContext.Current.CancellationToken).Where(_ => _.Severity == DiagnosticSeverity.Error).ShouldBeEmpty();
    [Fact] void should_analyze_both_handler_and_endpoint_commands() => _contribution.Facts.OfType<ArtifactFact>().Count(_ => _.Definition.Key.Kind == ArtifactKind.Command).ShouldEqual(2);
    [Fact] void should_recognize_grpc_as_a_resolved_scope() => _contribution.Diagnostics.Where(_ => _.Code == WolverineDiagnosticCodes.HandlerDiscoveryConfigurationUnresolved).ShouldBeEmpty();
    [Fact] void should_exclude_grpc_middleware_from_both_chains() => _contribution.Diagnostics.Where(_ => _.Code == WolverineDiagnosticCodes.CompoundStageOmitted).ShouldBeEmpty();
    [Fact] void should_not_cascade_grpc_middleware_messages() => _contribution.Facts.OfType<RelationshipFact>().Where(_ => _.Definition.Key.Kind == RelationshipKind.Cascades).ShouldBeEmpty();
}
