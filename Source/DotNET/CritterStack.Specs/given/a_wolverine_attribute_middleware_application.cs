// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Cratis.CritterStack.Screenplay.given;

public class a_wolverine_attribute_middleware_application : Specification
{
    const string FrameworkSource =
        """
        namespace Wolverine
        {
            public class WolverineOptions { public IPolicies Policies { get; } = null!; }
            public enum HandlerContinuation { Continue, Stop }
            public interface IPolicies
            {
                void AddMiddleware(System.Type middlewareType, System.Func<object, bool>? filter = null);
                void AddMiddleware<T>(System.Func<object, bool>? filter = null);
            }
            public interface IMessageBus
            {
                System.Threading.Tasks.Task SendAsync(object message);
                System.Threading.Tasks.Task PublishAsync(object message);
            }
            public class OutgoingMessages : System.Collections.Generic.List<object>, Configuration.IWolverineReturnType;
        }
        namespace Wolverine.Configuration { public interface IWolverineReturnType; }
        namespace Wolverine.Attributes
        {
            public enum MiddlewareScoping { Anywhere, MessageHandlers, HttpEndpoints, All = Anywhere, Handlers = MessageHandlers }
            public abstract class ScopedMiddlewareAttribute : System.Attribute
            {
                public MiddlewareScoping Scoping { get; set; }
                public ScopedMiddlewareAttribute(MiddlewareScoping scoping) { Scoping = scoping; }
                protected ScopedMiddlewareAttribute() { }
            }
            [System.AttributeUsage(System.AttributeTargets.Method)]
            public class WolverineBeforeAttribute : ScopedMiddlewareAttribute
            {
                public WolverineBeforeAttribute(MiddlewareScoping scoping) : base(scoping) { }
                public WolverineBeforeAttribute() { }
            }
            [System.AttributeUsage(System.AttributeTargets.Method)]
            public class WolverineAfterAttribute : ScopedMiddlewareAttribute
            {
                public WolverineAfterAttribute(MiddlewareScoping scoping) : base(scoping) { }
                public WolverineAfterAttribute() { }
            }
            [System.AttributeUsage(System.AttributeTargets.Method)]
            public class WolverineFinallyAttribute : ScopedMiddlewareAttribute
            {
                public WolverineFinallyAttribute(MiddlewareScoping scoping) : base(scoping) { }
                public WolverineFinallyAttribute() { }
            }
            [System.AttributeUsage(System.AttributeTargets.Method)]
            public class WolverineAfterCommitAttribute : ScopedMiddlewareAttribute
            {
                public WolverineAfterCommitAttribute(MiddlewareScoping scoping) : base(scoping) { }
                public WolverineAfterCommitAttribute() { }
            }
            [System.AttributeUsage(System.AttributeTargets.Class | System.AttributeTargets.Method, AllowMultiple = true)]
            public class MiddlewareAttribute(params System.Type[] types) : System.Attribute
            {
                public MiddlewareScoping Scoping { get; set; }
            }
            [System.AttributeUsage(System.AttributeTargets.Method)]
            public class AlwaysPublishResponseAttribute : System.Attribute;
            [System.AttributeUsage(System.AttributeTargets.Class)]
            public class TopicAttribute(string topic) : System.Attribute;
        }
        namespace Wolverine.Http
        {
            public class HttpMethodAttribute(string method, string route) : System.Attribute;
            public class WolverinePostAttribute(string route) : HttpMethodAttribute("POST", route);
            public class WolverineGetAttribute(string route) : HttpMethodAttribute("GET", route);
            public class DeduplicatedWithResponseAttribute : System.Attribute;
            public class WolverineHttpOptions
            {
                public void AddMiddleware(System.Type middlewareType, System.Func<object, bool>? filter = null) { }
            }
        }
        namespace Marten
        {
            public interface IDocumentSession
            {
                void Store<T>(T document);
                Events.IEventStore Events { get; }
            }
        }
        namespace Marten.Events
        {
            public interface IEventStore { void Append(System.Guid id, params object[] events); }
        }
        namespace Lookalike.Attributes
        {
            public class WolverineBeforeAttribute : System.Attribute;
            public class AlwaysPublishResponseAttribute : System.Attribute;
            public class TopicAttribute(string topic) : System.Attribute;
        }
        namespace Lookalike.Http { public class DeduplicatedWithResponseAttribute : System.Attribute; }
        """;

    static readonly IReadOnlyList<MetadataReference> _references =
    [
        .. ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES"))
            .Split(Path.PathSeparator)
            .Select(_ => MetadataReference.CreateFromFile(_))
    ];

    protected static DotNetProjectCompilation CreateProject(string source, string name = "MiddlewareApplication")
    {
        var compilation = CSharpCompilation.Create(
            name,
            [
                CSharpSyntaxTree.ParseText(FrameworkSource, path: "/workspace/Framework.cs"),
                CSharpSyntaxTree.ParseText(source, path: $"/workspace/{name}/Handlers.cs")
            ],
            _references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, nullableContextOptions: NullableContextOptions.Enable));
        return new()
        {
            Name = name,
            ProjectPath = $"/workspace/{name}/{name}.csproj",
            SourceRoot = "/workspace",
            Compilation = compilation,
            AuthoredSyntaxTrees = compilation.SyntaxTrees.ToHashSet()
        };
    }

    protected static AdapterContribution Analyze(DotNetProjectCompilation project) =>
        new CritterStackScreenplayAdapter().Analyze(new DotNetAnalysisContext([project]), new DotNetAdapterOptions());
}
