// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Cratis.CritterStack.Screenplay.given;

/// <summary>
/// One isolated handler per behavior of the Wolverine return classification, the Marten session-append and aggregate-yield
/// production links, and the SlicePattern declarations: every consequence kind, the order the kinds are decided in, whether
/// an append is conditional, why a stream identity is not read from the command, and which trigger wins a declared pattern.
/// </summary>
public class a_wolverine_returns_and_links_variants_application : Specification
{
    const string FrameworkSource =
        """
        namespace Microsoft.AspNetCore.Http
        {
            public interface IResult;
        }

        namespace Marten
        {
            public interface IDocumentSession
            {
                Marten.Events.IEventStoreOperations Events { get; }
            }
        }

        namespace Marten.Events
        {
            public class StreamAction;
            public interface IEventStoreOperations
            {
                StreamAction Append(System.Guid stream, params object[] events);
                StreamAction Append(string stream, params object[] events);
                StreamAction Append(System.Guid stream, long expectedVersion, params object[] events);
                StreamAction StartStream<TAggregate>(System.Guid id, params object[] events) where TAggregate : class;
                StreamAction StartStream<TAggregate>(params object[] events) where TAggregate : class;
            }
        }

        namespace Wolverine
        {
            public class WolverineOptions;
            public abstract record TimeoutMessage;
            public interface ISideEffect;
            public interface IResponseAware;
            public class OutgoingMessages : System.Collections.Generic.List<object>, Wolverine.Configuration.IWolverineReturnType;
            public abstract class Saga;
        }

        namespace Wolverine.Attributes
        {
            public class WolverineIgnoreAttribute : System.Attribute;
        }

        namespace Wolverine.Configuration
        {
            public interface IWolverineReturnType;
        }

        namespace Wolverine.Http
        {
            public abstract class WolverineHttpMethodAttribute(string route) : System.Attribute;
            public class WolverinePostAttribute(string route) : WolverineHttpMethodAttribute(route);
            public class WolverineGetAttribute(string route) : WolverineHttpMethodAttribute(route);
            public class WolverineHeadAttribute(string route) : WolverineHttpMethodAttribute(route);
            public class EmptyResponseAttribute : System.Attribute;
            public class CreationResponse(string url);
        }

        namespace Wolverine.Marten
        {
            public class Events : System.Collections.Generic.List<object>, Wolverine.Configuration.IWolverineReturnType;
            public interface IStartStream : Wolverine.Configuration.IWolverineReturnType;
            public class UpdatedAggregate<T>;
            public class AggregateHandlerAttribute : Wolverine.Persistence.EventSourcing.DeciderFunctionAttribute;
        }

        namespace Wolverine.Persistence
        {
            public interface IStorageAction<T>;
            public class UnitOfWork<T> : IStorageAction<T>;
        }

        namespace Wolverine.Persistence.EventSourcing
        {
            public class DeciderFunctionAttribute : System.Attribute;
            public class EventsToAppend : System.Collections.Generic.List<object>, Wolverine.Configuration.IWolverineReturnType;
            [System.AttributeUsage(System.AttributeTargets.Method | System.AttributeTargets.Class, Inherited = true)]
            public sealed class SlicePatternAttribute(JasperFx.Events.EventModeling.SlicePattern pattern) : System.Attribute
            {
                public JasperFx.Events.EventModeling.SlicePattern Pattern { get; } = pattern;
            }
        }

        namespace JasperFx.Events.EventModeling
        {
            public enum SlicePattern { Command, View, Automation, Translation }
        }
        """;

    const string EventsSource =
        """
        using Wolverine.Persistence.EventSourcing;
        using JasperFx.Events.EventModeling;
        namespace Variants;

        public record Happened(string Note);
        public record Another(string Note);
        public record Third(string Note);
        public class State { public System.Guid Id { get; set; } public void Apply(Happened e) { } public void Apply(Another e) { } }
        public class Document { public string Id { get; set; } = string.Empty; }
        public record Reply(string Text);
        public record Follow(string Text);
        public class Effect : Wolverine.ISideEffect { }
        public class Aware : Wolverine.IResponseAware { }
        public class Result : Microsoft.AspNetCore.Http.IResult { }
        public class CustomStore : Wolverine.Persistence.IStorageAction<Document> { }
        public record Expiry : Wolverine.TimeoutMessage;
        public class StoreSaga : Wolverine.Saga { public System.Guid Id { get; set; } }
        public class CreationResponseLike { }
        public class UpdatedAggregateLike { }
        """;

    const string LinksSource =
        """
        namespace Variants;

        public record PlainAppend(System.Guid Id, string Note);
        public record StringAppend(string Key, string Note);
        public record ConditionalAppend(System.Guid Id, string Note, bool Share);
        public record EarlyExitAppend(System.Guid Id, string Note, bool Skip);
        public record UnconditionalAfterGuard(System.Guid Id, string Note);
        public record ComputedAppend(string Slot, string Note, bool Share);
        public record StartedAppend(string Note);
        public record StartedWithId(System.Guid Id, string Note);
        public record LambdaAppend(System.Guid Id, string Note);
        public record IndirectAppend(System.Guid Id, string Note);
        public record ArrayAppend(System.Guid Id, string Note);
        public record VersionedAppend(System.Guid Id, string Note);
        public record SwitchAppend(System.Guid Id, int Kind, string Note);
        public record LoopAppend(System.Guid Id, string Note);
        public record OtherParameterAppend(System.Guid Id, string Note);
        public record SagaStateAppend(System.Guid Id);
        public record ConditionalComputed(string Slot, string Note, bool Share);

        public static class PlainAppendHandler
        {
            public static void Handle(PlainAppend command, Marten.IDocumentSession session) =>
                session.Events.Append(command.Id, new Happened(command.Note));
        }

        public static class StringAppendHandler
        {
            public static void Handle(StringAppend command, Marten.IDocumentSession session) =>
                session.Events.Append(command.Key, new Happened(command.Note));
        }

        public static class ConditionalAppendHandler
        {
            public static void Handle(ConditionalAppend command, Marten.IDocumentSession session)
            {
                if (command.Share) { session.Events.Append(command.Id, new Happened(command.Note)); }
            }
        }

        public static class EarlyExitAppendHandler
        {
            public static void Handle(EarlyExitAppend command, Marten.IDocumentSession session)
            {
                if (command.Skip) { return; }
                session.Events.Append(command.Id, new Happened(command.Note));
            }
        }

        public static class UnconditionalAfterGuardHandler
        {
            public static void Handle(UnconditionalAfterGuard command, Marten.IDocumentSession session)
            {
                var ignored = 1;
                session.Events.Append(command.Id, new Happened(command.Note));
            }
        }

        public static class ComputedAppendHandler
        {
            public static void Handle(ComputedAppend command, Marten.IDocumentSession session) =>
                session.Events.Append(System.Guid.Parse(command.Slot), new Happened(command.Note));
        }

        public static class ConditionalComputedHandler
        {
            public static void Handle(ConditionalComputed command, Marten.IDocumentSession session)
            {
                if (command.Share) { session.Events.Append(System.Guid.Parse(command.Slot), new Happened(command.Note)); }
            }
        }

        public static class StartedAppendHandler
        {
            public static void Handle(StartedAppend command, Marten.IDocumentSession session) =>
                session.Events.StartStream<State>(new Happened(command.Note));
        }

        public static class StartedWithIdHandler
        {
            public static void Handle(StartedWithId command, Marten.IDocumentSession session) =>
                session.Events.StartStream<State>(command.Id, new Happened(command.Note));
        }

        public static class LambdaAppendHandler
        {
            public static void Handle(LambdaAppend command, Marten.IDocumentSession session)
            {
                var later = new System.Action(() => session.Events.Append(command.Id, new Happened(command.Note)));
                later();
            }
        }

        public static class IndirectAppendHandler
        {
            public static void Handle(IndirectAppend command, Marten.IDocumentSession session)
            {
                var created = new Happened(command.Note);
                session.Events.Append(command.Id, created);
            }
        }

        public static class ArrayAppendHandler
        {
            public static void Handle(ArrayAppend command, Marten.IDocumentSession session) =>
                session.Events.Append(command.Id, new object[] { new Happened(command.Note), new Another(command.Note) });
        }

        public static class VersionedAppendHandler
        {
            public static void Handle(VersionedAppend command, Marten.IDocumentSession session) =>
                session.Events.Append(command.Id, 3L, new Happened(command.Note), new Third(command.Note));
        }

        public static class SwitchAppendHandler
        {
            public static void Handle(SwitchAppend command, Marten.IDocumentSession session)
            {
                switch (command.Kind)
                {
                    case 1: session.Events.Append(command.Id, new Happened(command.Note)); break;
                    default: session.Events.Append(command.Id, new Another(command.Note)); break;
                }
            }
        }

        public static class LoopAppendHandler
        {
            public static void Handle(LoopAppend command, Marten.IDocumentSession session)
            {
                foreach (var note in new[] { command.Note }) { session.Events.Append(command.Id, new Happened(note)); }
            }
        }

        public static class OtherParameterAppendHandler
        {
            public static void Handle(OtherParameterAppend command, string tenant, Marten.IDocumentSession session) =>
                session.Events.Append(System.Guid.Parse(tenant), new Happened(command.Note));
        }

        public static class SagaStateAppendHandler
        {
            public static void Handle(SagaStateAppend command, Marten.IDocumentSession session) =>
                session.Events.Append(command.Id, new StoreSaga(), new Happened("x"));
        }
        """;

    const string YieldsSource =
        """
        using Wolverine.Marten;
        namespace Variants;

        public record YieldPlain(System.Guid Id);
        public record YieldNoIdentity(string Note);
        public record YieldConditional(System.Guid Id, bool Go);
        public record YieldConditionalNoIdentity(string Note, bool Go);
        public record YieldIndirect(System.Guid Id);
        public record YieldAfterGuard(System.Guid Id, bool Skip);
        public record YieldBreak(System.Guid Id, bool Stop);
        public record YieldLocal(System.Guid Id);
        public record YieldMany(System.Guid Id);

        public static class YieldPlainHandler
        {
            [AggregateHandler]
            public static System.Collections.Generic.IEnumerable<object> Handle(YieldPlain command, State state)
            {
                yield return new Happened("a");
            }
        }

        public static class YieldNoIdentityHandler
        {
            [AggregateHandler]
            public static System.Collections.Generic.IEnumerable<object> Handle(YieldNoIdentity command, State state)
            {
                yield return new Happened(command.Note);
            }
        }

        public static class YieldConditionalHandler
        {
            [AggregateHandler]
            public static System.Collections.Generic.IEnumerable<object> Handle(YieldConditional command, State state)
            {
                if (command.Go) { yield return new Happened("a"); }
            }
        }

        public static class YieldConditionalNoIdentityHandler
        {
            [AggregateHandler]
            public static System.Collections.Generic.IEnumerable<object> Handle(YieldConditionalNoIdentity command, State state)
            {
                if (command.Go) { yield return new Happened(command.Note); }
            }
        }

        public static class YieldIndirectHandler
        {
            [AggregateHandler]
            public static System.Collections.Generic.IEnumerable<object> Handle(YieldIndirect command, State state)
            {
                var created = new Happened("a");
                yield return created;
            }
        }

        public static class YieldAfterGuardHandler
        {
            [AggregateHandler]
            public static System.Collections.Generic.IEnumerable<object> Handle(YieldAfterGuard command, State state)
            {
                if (command.Skip) { yield break; }
                yield return new Another("a");
            }
        }

        public static class YieldBreakHandler
        {
            [AggregateHandler]
            public static System.Collections.Generic.IEnumerable<object> Handle(YieldBreak command, State state)
            {
                yield return new Happened("first");
                if (command.Stop) { yield break; }
            }
        }

        public static class YieldLocalHandler
        {
            [AggregateHandler]
            public static System.Collections.Generic.IEnumerable<object> Handle(YieldLocal command, State state)
            {
                System.Collections.Generic.IEnumerable<object> Inner() { yield return new Third("inner"); }
                yield return new Happened("outer");
            }
        }
        """;

    const string ReturnsSource =
        """
        using Wolverine.Marten;
        namespace Variants;

        public record ReturnOutgoing(string Text);
        public record ReturnSideEffect(string Text);
        public record ReturnAware(string Text);
        public record ReturnStore(string Text);
        public record ReturnUnit(string Text);
        public record ReturnCustomStore(string Text);
        public record ReturnEvents(string Text);
        public record ReturnAppend(string Text);
        public record ReturnTimeout(string Text);
        public record ReturnCascade(string Text);
        public record ReturnTuple(string Text);
        public record ReturnPrimitive(string Text);
        public record ReturnTask(string Text);
        public record ReturnEnumerable(string Text);
        public record ReturnAggregateEvent(System.Guid Id);
        public record ReturnAggregateWithStream(System.Guid Id);
        public record ReturnAggregateOutgoing(System.Guid Id);

        public static class ReturnOutgoingHandler { public static Wolverine.OutgoingMessages Handle(ReturnOutgoing message) => []; }
        public static class ReturnSideEffectHandler { public static Effect Handle(ReturnSideEffect message) => new(); }
        public static class ReturnAwareHandler { public static Aware Handle(ReturnAware message) => new(); }
        public static class ReturnStoreHandler { public static Wolverine.Persistence.IStorageAction<Document> Handle(ReturnStore message) => null!; }
        public static class ReturnUnitHandler { public static Wolverine.Persistence.UnitOfWork<Document> Handle(ReturnUnit message) => new(); }
        public static class ReturnCustomStoreHandler { public static CustomStore Handle(ReturnCustomStore message) => new(); }
        public static class ReturnEventsHandler { public static Wolverine.Marten.Events Handle(ReturnEvents message) => []; }
        public static class ReturnAppendHandler { public static Wolverine.Persistence.EventSourcing.EventsToAppend Handle(ReturnAppend message) => []; }
        public static class ReturnTimeoutHandler { public static Expiry Handle(ReturnTimeout message) => new(); }
        public static class ReturnCascadeHandler { public static Follow Handle(ReturnCascade message) => new(message.Text); }
        public static class ReturnPrimitiveHandler { public static int Handle(ReturnPrimitive message) => 0; }
        public static class ReturnTaskHandler { public static System.Threading.Tasks.Task<Follow> Handle(ReturnTask message) => System.Threading.Tasks.Task.FromResult(new Follow(message.Text)); }
        public static class ReturnEnumerableHandler { public static System.Collections.Generic.IEnumerable<Follow> Handle(ReturnEnumerable message) => []; }
        public static class ReturnTupleHandler
        {
            public static (Reply, Wolverine.OutgoingMessages, Effect, Follow, Wolverine.Persistence.UnitOfWork<Document>, Expiry, int, StoreSaga) Handle(ReturnTuple message) => default;
        }

        public static class ReturnAggregateEventHandler
        {
            [AggregateHandler]
            public static Happened Handle(ReturnAggregateEvent command, State state) => new("a");
        }

        public static class ReturnAggregateOutgoingHandler
        {
            [AggregateHandler]
            public static (Another, Wolverine.OutgoingMessages, Follow) Handle(ReturnAggregateOutgoing command, State state) => default;
        }
        """;

    const string EndpointsSource =
        """
        using Wolverine.Http;
        namespace Variants;

        public record PostReply(string Text);
        public record PostEmpty(string Text);
        public record PostTuple(string Text);
        public record PostResult(string Text);
        public record PostCreation(string Text);
        public record PostOutgoing(string Text);
        public record PostVoid(string Text);

        public static class Endpoints
        {
            [WolverinePost("/reply")] public static Reply Reply(PostReply command) => new("x");
            [WolverinePost("/empty"), EmptyResponse] public static Follow Empty(PostEmpty command) => new("x");
            [WolverinePost("/tuple")] public static (Reply, Follow, Wolverine.OutgoingMessages) Tuple(PostTuple command) => default;
            [WolverinePost("/result")] public static Result Result(PostResult command) => new();
            [WolverinePost("/creation")] public static CreationResponse Creation(PostCreation command) => new("/x");
            [WolverinePost("/outgoing")] public static (Wolverine.OutgoingMessages, Reply) Outgoing(PostOutgoing command) => default;
            [WolverinePost("/void")] public static void Void(PostVoid command) { }
        }
        """;

    const string PatternsSource =
        """
        using Wolverine.Persistence.EventSourcing;
        using JasperFx.Events.EventModeling;
        namespace Variants;

        public record PatternHttpPost(string Note);
        public record PatternHttpGet;
        public record PatternHttpHead;
        public record PatternTimeoutMessage : Wolverine.TimeoutMessage;
        public record PatternAgreeing;
        public record PatternUnplaced;
        public record PatternMissing;
        public record PatternTimeoutAgree : Wolverine.TimeoutMessage;
        public record PatternTranslated(string Note);

        public static class PatternEndpoints
        {
            [Wolverine.Http.WolverinePost("/pattern"), SlicePattern(SlicePattern.View)]
            public static void Post(PatternHttpPost command, Marten.IDocumentSession session) => session.Events.Append(System.Guid.NewGuid(), new Happened(command.Note));

            [Wolverine.Http.WolverineGet("/pattern-get"), SlicePattern(SlicePattern.Command)]
            public static Reply Get() => new("x");

            [Wolverine.Http.WolverineHead("/pattern-head"), SlicePattern(SlicePattern.Automation)]
            public static Reply Head() => new("x");

            [Wolverine.Http.WolverinePost("/pattern-agree"), SlicePattern(SlicePattern.Command)]
            public static void Agree(PatternAgreeing command, Marten.IDocumentSession session) => session.Events.Append(System.Guid.NewGuid(), new Another("a"));
        }

        public static class PatternTimeoutHandler
        {
            [SlicePattern(SlicePattern.Translation)]
            public static void Handle(PatternTimeoutMessage command, Marten.IDocumentSession session) => session.Events.Append(System.Guid.NewGuid(), new Happened("t"));
        }

        public static class PatternTimeoutAgreeHandler
        {
            [SlicePattern(SlicePattern.Automation)]
            public static void Handle(PatternTimeoutAgree command, Marten.IDocumentSession session) => session.Events.Append(System.Guid.NewGuid(), new Another("t"));
        }

        [SlicePattern(SlicePattern.Translation)]
        public static class PatternTranslatedHandler
        {
            public static void Handle(PatternTranslated command, Marten.IDocumentSession session) => session.Events.Append(System.Guid.NewGuid(), new Third(command.Note));
        }

        public static class PatternUnplacedHandler
        {
            [SlicePattern(SlicePattern.Automation)]
            public static void Handle(PatternUnplaced command) { }
        }

        public static class PatternMissingHandler
        {
            [SlicePattern((SlicePattern)42)]
            public static void Handle(PatternMissing command, Marten.IDocumentSession session) => session.Events.Append(System.Guid.NewGuid(), new Another("m"));
        }
        """;

    static readonly IReadOnlyList<MetadataReference> _references =
    [
        .. ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES"))
            .Split(Path.PathSeparator)
            .Select(_ => MetadataReference.CreateFromFile(_))
    ];

    protected DotNetProjectCompilation Project = null!;
    protected AdapterContribution Contribution = null!;

    void Establish()
    {
        var compilation = CSharpCompilation.Create(
            "Variants",
            [
                CSharpSyntaxTree.ParseText(FrameworkSource, path: "/workspace/Variants/Framework.cs"),
                CSharpSyntaxTree.ParseText(EventsSource, path: "/workspace/Variants/Events.cs"),
                CSharpSyntaxTree.ParseText(LinksSource, path: "/workspace/Variants/Links.cs"),
                CSharpSyntaxTree.ParseText(YieldsSource, path: "/workspace/Variants/Yields.cs"),
                CSharpSyntaxTree.ParseText(ReturnsSource, path: "/workspace/Variants/Returns.cs"),
                CSharpSyntaxTree.ParseText(EndpointsSource, path: "/workspace/Variants/Endpoints.cs"),
                CSharpSyntaxTree.ParseText(PatternsSource, path: "/workspace/Variants/Patterns.cs")
            ],
            _references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, nullableContextOptions: NullableContextOptions.Enable));
        Project = new()
        {
            Name = "Variants",
            ProjectPath = "/workspace/Variants/Variants.csproj",
            SourceRoot = "/workspace",
            Compilation = compilation,
            AuthoredSyntaxTrees = compilation.SyntaxTrees.ToHashSet()
        };
    }

    protected void Analyze() =>
        Contribution = new CritterStackScreenplayAdapter().Analyze(
            new DotNetAnalysisContext([Project]),
            new DotNetAdapterOptions());
}
