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
    IReadOnlyDictionary<string, DotNetProjectCompilation> _variants = null!;
    IReadOnlyDictionary<string, AdapterContribution> _results = null!;

    void Establish()
    {
        _project = CreateProject(Source);
        var named = Source
            .Replace("MiddlewareScoping.HttpEndpoints, Scoping = MiddlewareScoping.Handlers", "MiddlewareScoping.Handlers", StringComparison.Ordinal)
            .Replace("MiddlewareScoping.Handlers, Scoping = MiddlewareScoping.HttpEndpoints", "MiddlewareScoping.HttpEndpoints", StringComparison.Ordinal)
            .Replace("(MiddlewareScoping.", "(Scoping = MiddlewareScoping.", StringComparison.Ordinal);
        _variants = new Dictionary<string, DotNetProjectCompilation>
        {
            ["named"] = CreateProject(named),
            ["anywhere"] = CreateProject(Anywhere(Source)),
            ["named-anywhere"] = CreateProject(Anywhere(named)),
            ["alias"] = CreateProject("using Scope = Wolverine.Attributes.MiddlewareScoping;\n" + Source.Replace("MiddlewareScoping.", "Scope.", StringComparison.Ordinal)),
            ["named-alias"] = CreateProject("using Scope = Wolverine.Attributes.MiddlewareScoping;\n" + named.Replace("MiddlewareScoping.", "Scope.", StringComparison.Ordinal)),
            ["async"] = CreateProject(Source.Replace(
                "public static HttpMessage HttpFinish() => new();",
                "public static System.Threading.Tasks.Task<HttpMessage> HttpFinishAsync() => System.Threading.Tasks.Task.FromResult(new HttpMessage());",
                StringComparison.Ordinal))
        };
    }

    void Because()
    {
        _contribution = Analyze(_project);
        _results = _variants.ToDictionary(_ => _.Key, _ => Analyze(_.Value));
    }

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

    [Fact] void should_compile_all_scope_variants() => _variants.Values.SelectMany(_ => _.Compilation.GetDiagnostics(TestContext.Current.CancellationToken)).Where(_ => _.Severity == DiagnosticSeverity.Error).ShouldBeEmpty();
    [Fact] void should_keep_all_four_named_stage_scopes_and_overrides() => StagesFor("named").Count.ShouldEqual(10);
    [Fact] void should_keep_named_handler_stages_off_endpoints() => StagesFor("named").Where(_ => _.Message.Contains("'Handler", StringComparison.Ordinal)).ShouldEachConformTo(_ => _.Subject.Value.EndsWith("/ConstructorMiddleware.Process", StringComparison.Ordinal));
    [Fact] void should_keep_named_http_stages_off_handlers() => StagesFor("named").Where(_ => _.Message.Contains("'Http", StringComparison.Ordinal)).ShouldEachConformTo(_ => _.Subject.Value.EndsWith("/ConstructorMiddleware.Create", StringComparison.Ordinal));
    [Fact] void should_apply_constructor_anywhere_to_both_chains() => StagesFor("anywhere").Count.ShouldEqual(20);
    [Fact] void should_apply_named_anywhere_to_both_chains() => StagesFor("named-anywhere").Count.ShouldEqual(20);
    [Fact] void should_resolve_aliased_constructor_scopes() => StagesFor("alias").Count.ShouldEqual(10);
    [Fact] void should_keep_aliased_constructor_handler_scope() => StagesFor("alias").Where(_ => _.Message.Contains("'Handler", StringComparison.Ordinal)).ShouldEachConformTo(_ => _.Subject.Value.EndsWith("/ConstructorMiddleware.Process", StringComparison.Ordinal));
    [Fact] void should_resolve_aliased_named_scopes() => StagesFor("named-alias").Count.ShouldEqual(10);
    [Fact] void should_keep_aliased_named_http_scope() => StagesFor("named-alias").Where(_ => _.Message.Contains("'Http", StringComparison.Ordinal)).ShouldEachConformTo(_ => _.Subject.Value.EndsWith("/ConstructorMiddleware.Create", StringComparison.Ordinal));
    [Fact] void should_keep_async_constructor_attribute_stages() => StagesFor("async").Single(_ => _.Message.Contains("'HttpFinishAsync'", StringComparison.Ordinal)).Subject.Value.ShouldContain("/ConstructorMiddleware.Create");
    [Fact] void should_keep_async_constructor_attribute_cascades() => _results["async"].Facts.OfType<RelationshipFact>().Count(_ => _.Definition.Key.Kind == RelationshipKind.Cascades && _.Definition.Key.Source.Value.EndsWith("/ConstructorMiddleware.Create", StringComparison.Ordinal)).ShouldEqual(5);

    static string Anywhere(string source) => source
        .Replace("MiddlewareScoping.HttpEndpoints", "MiddlewareScoping.Anywhere", StringComparison.Ordinal)
        .Replace("MiddlewareScoping.Handlers", "MiddlewareScoping.Anywhere", StringComparison.Ordinal);

    IReadOnlyList<GenerationDiagnostic> StagesFor(string variant) => [.. _results[variant].Diagnostics.Where(_ => _.Code == WolverineDiagnosticCodes.CompoundStageOmitted)];
    GenerationDiagnostic Stage(string method) => _contribution.Diagnostics.Single(_ => _.Code == WolverineDiagnosticCodes.CompoundStageOmitted && _.Message.Contains($"'{method}'", StringComparison.Ordinal));
    IReadOnlyList<RelationshipFact> CascadesFor(string command) => [.. _contribution.Facts.OfType<RelationshipFact>().Where(_ => _.Definition.Key.Kind == RelationshipKind.Cascades && _.Definition.Key.Source.Value.EndsWith($"/ConstructorMiddleware.{command}", StringComparison.Ordinal))];
    IReadOnlyList<RelationshipFact> ProductionsFor(string message) => [.. _contribution.Facts.OfType<RelationshipFact>().Where(_ => _.Definition.Key.Kind == RelationshipKind.Produces && _.Definition.Key.Target.Value.EndsWith($"/ConstructorMiddleware.{message}", StringComparison.Ordinal))];
}
