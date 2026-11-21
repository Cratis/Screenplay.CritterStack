// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Cratis.CritterStack.Screenplay.for_CritterStackScreenplayAdapter;

public class when_constructor_scoping_attribute_middleware : given.a_wolverine_attribute_middleware_application
{
    const string Source =
        """
        using Wolverine.Attributes;
        namespace ConstructorMiddleware;
        public record Process(System.Guid Id);
        public record Create(System.Guid Id);
        public record Processed(System.Guid Id);
        public record Created(System.Guid Id);
        public record HttpMessage;
        public record HandlerMessage;
        [Middleware(typeof(Shared))]
        public static class ProcessHandler
        {
            public static void Handle(Process message, Marten.IDocumentSession session) =>
                session.Events.Append(message.Id, new Processed(message.Id));
        }
        [Middleware(typeof(Shared))]
        public static class CreateEndpoint
        {
            [Wolverine.Http.WolverinePost("/create")]
            public static void Post(Create message, Marten.IDocumentSession session) =>
                session.Events.Append(message.Id, new Created(message.Id));
        }
        public static class Shared
        {
            [WolverineBefore(MiddlewareScoping.HttpEndpoints)]
            public static (Wolverine.HandlerContinuation, HttpMessage) HttpPrepare() => (Wolverine.HandlerContinuation.Stop, new());
            [WolverineAfter(MiddlewareScoping.HttpEndpoints)]
            public static HttpMessage HttpFinish() => new();
            [WolverineFinally(MiddlewareScoping.HttpEndpoints)]
            public static HttpMessage HttpClean() => new();
            [WolverineAfterCommit(MiddlewareScoping.HttpEndpoints)]
            public static HttpMessage HttpSaved() => new();
            [WolverineBefore(MiddlewareScoping.Handlers)]
            public static HandlerMessage HandlerPrepare() => new();
            [WolverineAfter(MiddlewareScoping.Handlers)]
            public static HandlerMessage HandlerFinish() => new();
            [WolverineFinally(MiddlewareScoping.Handlers)]
            public static HandlerMessage HandlerClean() => new();
            [WolverineAfterCommit(MiddlewareScoping.Handlers)]
            public static HandlerMessage HandlerSaved() => new();
            [WolverineBefore(MiddlewareScoping.HttpEndpoints, Scoping = MiddlewareScoping.Handlers)]
            public static HandlerMessage HandlerOverride() => new();
            [WolverineAfter(MiddlewareScoping.Handlers, Scoping = MiddlewareScoping.HttpEndpoints)]
            public static HttpMessage HttpOverride() => new();
        }
        """;

    DotNetProjectCompilation _project = null!;
    AdapterContribution _contribution = null!;

    void Establish() => _project = CreateProject(Source);
    void Because() => _contribution = Analyze(_project);

    [Fact] void should_compile_the_fixture() => _project.Compilation.GetDiagnostics(TestContext.Current.CancellationToken).Where(_ => _.Severity == DiagnosticSeverity.Error).ShouldBeEmpty();
    [Fact] void should_scope_before_to_http_endpoints() => Stage("HttpPrepare").Subject.Value.ShouldContain("/ConstructorMiddleware.Create");
    [Fact] void should_scope_after_to_http_endpoints() => Stage("HttpFinish").Subject.Value.ShouldContain("/ConstructorMiddleware.Create");
    [Fact] void should_scope_finally_to_http_endpoints() => Stage("HttpClean").Subject.Value.ShouldContain("/ConstructorMiddleware.Create");
    [Fact] void should_scope_after_commit_to_http_endpoints() => Stage("HttpSaved").Subject.Value.ShouldContain("/ConstructorMiddleware.Create");
    [Fact] void should_scope_before_to_message_handlers() => Stage("HandlerPrepare").Subject.Value.ShouldContain("/ConstructorMiddleware.Process");
    [Fact] void should_scope_after_to_message_handlers() => Stage("HandlerFinish").Subject.Value.ShouldContain("/ConstructorMiddleware.Process");
    [Fact] void should_scope_finally_to_message_handlers() => Stage("HandlerClean").Subject.Value.ShouldContain("/ConstructorMiddleware.Process");
    [Fact] void should_scope_after_commit_to_message_handlers() => Stage("HandlerSaved").Subject.Value.ShouldContain("/ConstructorMiddleware.Process");
    [Fact] void should_allow_named_scoping_to_override_http_constructor_scoping() => Stage("HandlerOverride").Subject.Value.ShouldContain("/ConstructorMiddleware.Process");
    [Fact] void should_allow_named_scoping_to_override_handler_constructor_scoping() => Stage("HttpOverride").Subject.Value.ShouldContain("/ConstructorMiddleware.Create");
    [Fact] void should_not_cascade_http_middleware_messages_from_handlers() => CascadesFor("Process").ShouldEachConformTo(_ => _.Definition.Key.Target.Value.EndsWith("/ConstructorMiddleware.HandlerMessage", StringComparison.Ordinal));
    [Fact] void should_not_cascade_handler_middleware_messages_from_endpoints() => CascadesFor("Create").ShouldEachConformTo(_ => _.Definition.Key.Target.Value.EndsWith("/ConstructorMiddleware.HttpMessage", StringComparison.Ordinal));
    [Fact] void should_keep_each_permitted_handler_cascade() => CascadesFor("Process").Count.ShouldEqual(5);
    [Fact] void should_keep_each_permitted_endpoint_cascade() => CascadesFor("Create").Count.ShouldEqual(5);
    [Fact] void should_not_suppress_handler_production_for_http_only_short_circuit_control() => ProductionsFor("Processed").Single().Definition.Key.Discriminator.ShouldNotEqual("imperative");
    [Fact] void should_keep_endpoint_production_conditional_on_its_short_circuit_control() => ProductionsFor("Created").Single().Definition.Key.Discriminator.ShouldEqual("imperative");

    GenerationDiagnostic Stage(string method) => _contribution.Diagnostics.Single(_ => _.Code == WolverineDiagnosticCodes.CompoundStageOmitted && _.Message.Contains($"'{method}'", StringComparison.Ordinal));
    IReadOnlyList<RelationshipFact> CascadesFor(string command) => [.. _contribution.Facts.OfType<RelationshipFact>().Where(_ => _.Definition.Key.Kind == RelationshipKind.Cascades && _.Definition.Key.Source.Value.EndsWith($"/ConstructorMiddleware.{command}", StringComparison.Ordinal))];
    IReadOnlyList<RelationshipFact> ProductionsFor(string message) => [.. _contribution.Facts.OfType<RelationshipFact>().Where(_ => _.Definition.Key.Kind == RelationshipKind.Produces && _.Definition.Key.Target.Value.EndsWith($"/ConstructorMiddleware.{message}", StringComparison.Ordinal))];
}
