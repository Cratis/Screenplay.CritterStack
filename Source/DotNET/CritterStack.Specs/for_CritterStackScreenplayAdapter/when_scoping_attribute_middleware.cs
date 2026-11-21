// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Cratis.CritterStack.Screenplay.for_CritterStackScreenplayAdapter;

public class when_scoping_attribute_middleware : given.a_wolverine_attribute_middleware_application
{
    const string Source =
        """
        using Wolverine.Attributes;
        namespace ScopedMiddleware;
        public record Process(string Id);
        public record Create(string Id);
        [Middleware(typeof(Shared), Scoping = MiddlewareScoping.MessageHandlers)]
        [Middleware(typeof(Excluded), Scoping = MiddlewareScoping.HttpEndpoints)]
        public static class ProcessHandler
        {
            public static void Handle(Process message) { }
            [WolverineBefore(Scoping = MiddlewareScoping.HttpEndpoints)]
            public static void Before(Process message) { }
        }
        [Middleware(typeof(Shared), Scoping = MiddlewareScoping.HttpEndpoints)]
        [Middleware(typeof(Excluded), Scoping = MiddlewareScoping.MessageHandlers)]
        public static class CreateEndpoint
        {
            [Wolverine.Http.WolverinePost("/create")]
            public static void Post(Create message) { }
            [WolverineAfter(Scoping = MiddlewareScoping.MessageHandlers)]
            public static void After(Create message) { }
        }
        public static class Shared
        {
            [WolverineBefore]
            public static void Initialize() { }
        }
        public static class Excluded
        {
            [WolverineBefore]
            public static void Never() { }
        }
        [Middleware(typeof(Shared), Scoping = MiddlewareScoping.HttpEndpoints)]
        public static class ViewEndpoint
        {
            [Wolverine.Http.WolverineGet("/view")]
            public static View Get() => new("view");
        }
        public record View(string Id);
        """;

    DotNetProjectCompilation _project = null!;
    AdapterContribution _contribution = null!;
    DotNetProjectCompilation _aliasedProject = null!;
    DotNetProjectCompilation _asyncProject = null!;
    AdapterContribution _aliased = null!;
    AdapterContribution _async = null!;

    void Establish()
    {
        _project = CreateProject(Source);
        _aliasedProject = CreateProject("using Scope = Wolverine.Attributes.MiddlewareScoping;\n" + Source.Replace("MiddlewareScoping.", "Scope.", StringComparison.Ordinal));
        _asyncProject = CreateProject(Source.Replace(
            "public static void Initialize() { }",
            "public static System.Threading.Tasks.Task BeforeAsync() => System.Threading.Tasks.Task.CompletedTask;",
            StringComparison.Ordinal));
    }

    void Because()
    {
        _contribution = Analyze(_project);
        _aliased = Analyze(_aliasedProject);
        _async = Analyze(_asyncProject);
    }

    [Fact] void should_compile_the_fixture() => _project.Compilation.GetDiagnostics(TestContext.Current.CancellationToken).Where(_ => _.Severity == DiagnosticSeverity.Error).ShouldBeEmpty();
    [Fact] void should_apply_shared_middleware_to_each_permitted_chain() => Stages.Count.ShouldEqual(3);
    [Fact] void should_not_fall_back_to_names_when_attribute_scoping_excludes_the_chain() => Stages.ShouldEachConformTo(_ => _.Message.Contains("'Initialize'", StringComparison.Ordinal));
    [Fact] void should_keep_the_distinct_application_locations() => Stages.Select(_ => _.Source.StartLine).ShouldContainOnly(5, 13, 32);

    [Fact] void should_include_the_query_endpoint_chain() => _contribution.Facts.OfType<ArtifactFact>().Count(_ => _.Definition.Key.Kind == ArtifactKind.Query).ShouldEqual(1);
    [Fact] void should_compile_aliased_scopes() => _aliasedProject.Compilation.GetDiagnostics(TestContext.Current.CancellationToken).Where(_ => _.Severity == DiagnosticSeverity.Error).ShouldBeEmpty();
    [Fact] void should_compile_async_middleware() => _asyncProject.Compilation.GetDiagnostics(TestContext.Current.CancellationToken).Where(_ => _.Severity == DiagnosticSeverity.Error).ShouldBeEmpty();
    [Fact] void should_keep_container_filtering_with_aliased_scopes() => _aliased.Diagnostics.Count(_ => _.Code == WolverineDiagnosticCodes.CompoundStageOmitted).ShouldEqual(3);
    [Fact] void should_not_apply_excluded_stages_with_aliased_scopes() => _aliased.Diagnostics.Where(_ => _.Code == WolverineDiagnosticCodes.CompoundStageOmitted).ShouldEachConformTo(_ => _.Message.Contains("'Initialize'", StringComparison.Ordinal));
    [Fact] void should_keep_async_stages_on_each_permitted_chain() => _async.Diagnostics.Count(_ => _.Code == WolverineDiagnosticCodes.CompoundStageOmitted).ShouldEqual(3);
    [Fact] void should_keep_async_attribute_stage_matching() => _async.Diagnostics.Where(_ => _.Code == WolverineDiagnosticCodes.CompoundStageOmitted).ShouldEachConformTo(_ => _.Message.Contains("'BeforeAsync'", StringComparison.Ordinal));

    IReadOnlyList<GenerationDiagnostic> Stages => [.. _contribution.Diagnostics.Where(_ => _.Code == WolverineDiagnosticCodes.CompoundStageOmitted)];
}
