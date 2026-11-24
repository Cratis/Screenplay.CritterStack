// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Cratis.CritterStack.Screenplay.Wolverine;

namespace Cratis.CritterStack.Screenplay.for_WolverineSourceEntryPoints;

public class when_observing_authored_entry_points : Specification
{
    const string FrameworkSource =
        """
        namespace Wolverine
        {
            public class WolverineOptions;
            public interface IWolverineHandler;
            public abstract class Saga;
        }
        namespace Wolverine.Attributes
        {
            public class WolverineHandlerAttribute : System.Attribute;
            public class WolverineIgnoreAttribute : System.Attribute;
        }
        namespace Wolverine.Http
        {
            public class WolverinePostAttribute(string route) : System.Attribute;
        }
        """;

    const string ApplicationSource =
        """
        namespace EntryPoints;
        public record Request(int Id);
        public record OtherRequest(int Id);
        public static class ConventionalHandler
        {
            public static void Handle(Request request) { }
        }
        public static class ExplicitActions
        {
            [Wolverine.Attributes.WolverineHandler]
            public static void Process(Request request) { }
        }
        public static partial class OverloadedHandler
        {
            public static void Handle(Request request) { }
        }
        public static class MixedHandler
        {
            [Wolverine.Http.WolverinePost("/requests")]
            public static void Handle(Request request) { }
        }
        [Wolverine.Attributes.WolverineIgnore]
        public static class IgnoredHandler
        {
            public static void Handle(Request request) { }
        }
        public static class MethodIgnoredHandler
        {
            [Wolverine.Attributes.WolverineIgnore]
            public static void Handle(Request request) { }
        }
        public static class NotAnEntryPoint
        {
            public static void Handle(Request request) { }
        }
        """;

    static readonly IReadOnlyList<MetadataReference> _references =
    [
        .. ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES"))
            .Split(Path.PathSeparator)
            .Select(path => MetadataReference.CreateFromFile(path))
    ];

    IReadOnlyList<WolverineSourceEntryPoint> _entries = null!;

    void Because()
    {
        var framework = CSharpSyntaxTree.ParseText(FrameworkSource, path: "/workspace/Framework.cs");
        var application = CSharpSyntaxTree.ParseText(ApplicationSource, path: "/workspace/EntryPoints/Application.cs");
        var partialHandler = CSharpSyntaxTree.ParseText(
            "namespace EntryPoints; public static partial class OverloadedHandler { public static void Handle(OtherRequest request) { } }",
            path: "/workspace/EntryPoints/Other.cs");
        var excluded = CSharpSyntaxTree.ParseText(
            "namespace EntryPoints; public static class ExcludedHandler { public static void Handle(Request request) { } }",
            path: "/workspace/EntryPoints/Excluded.cs");
        var compilation = CSharpCompilation.Create(
            "EntryPoints",
            [framework, application, partialHandler, excluded],
            _references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
        var project = new DotNetProjectCompilation
        {
            Name = "EntryPoints",
            ProjectPath = "/workspace/EntryPoints/EntryPoints.csproj",
            SourceRoot = "/workspace",
            Compilation = compilation,
            AuthoredSyntaxTrees = new HashSet<SyntaxTree> { framework, application, partialHandler }
        };
        var subjects = new CritterStackSubjectResolver(new([project]));
        var discovery = WolverineHandlerDiscovery.Discover(project, subjects, WolverinePolicyDiscovery.DiscoverRegistrations(project));
        _entries = WolverineFacts.ObserveEntryPoints(project, discovery.Policy);
    }

    [Fact] void should_keep_the_conventional_handler() => _entries.Count(entry => entry.Method.ContainingType.Name == "ConventionalHandler").ShouldEqual(1);
    [Fact] void should_keep_the_explicit_nonconventional_method() => _entries.Single(entry => entry.Method.ContainingType.Name == "ExplicitActions").Method.Name.ShouldEqual("Process");
    [Fact] void should_keep_both_overloads_across_partial_declarations() => _entries.Where(entry => entry.Method.ContainingType.Name == "OverloadedHandler").Select(entry => entry.Method.Parameters[0].Type.Name).Order(StringComparer.Ordinal).ShouldEqual(["OtherRequest", "Request"]);
    [Fact] void should_observe_an_http_method_only_once() => _entries.Count(entry => entry.Method.ContainingType.Name == "MixedHandler").ShouldEqual(1);
    [Fact] void should_keep_the_http_verb() => _entries.Single(entry => entry.Endpoint is not null).Endpoint!.Verb.ShouldEqual("POST");
    [Fact] void should_keep_the_http_route() => _entries.Single(entry => entry.Endpoint is not null).Endpoint!.Route.ShouldEqual("/requests");
    [Fact] void should_keep_the_same_method_symbol_for_the_http_observation() => _entries.Single(entry => entry.Endpoint is not null).Endpoint!.Method.ShouldEqual(_entries.Single(entry => entry.Endpoint is not null).Method);
    [Fact] void should_exclude_ignored_nonhandler_and_unselected_source() => _entries.Count.ShouldEqual(5);
}
