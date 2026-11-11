// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Cratis.CritterStack.Screenplay.given;

public class a_wolverine_dcb_competition_application : Specification
{
    const string FrameworkSource =
        """
        namespace Wolverine.Configuration
        {
            public interface IWolverineReturnType;
        }

        namespace Wolverine
        {
            public class WolverineOptions;
            public abstract class Saga;
            public sealed class OutgoingMessages : Wolverine.Configuration.IWolverineReturnType;
        }

        namespace Wolverine.Persistence.EventSourcing
        {
            public class DcbModelAttribute : System.Attribute;

            public class EventsToAppend : System.Collections.Generic.List<object>, Wolverine.Configuration.IWolverineReturnType
            {
                public EventsToAppend() { }
                public EventsToAppend(System.Collections.Generic.IEnumerable<object> events) : base(events) { }
            }
        }

        namespace Wolverine.Marten
        {
            public class Events : System.Collections.Generic.List<object>, Wolverine.Configuration.IWolverineReturnType
            {
                public Events() { }
                public Events(System.Collections.Generic.IEnumerable<object> events) : base(events) { }
            }
        }

        namespace JasperFx.Events.Tags
        {
            public sealed class EventTagQuery
            {
                public static EventTagQuery For<TTag>(TTag value) => new();
                public EventTagQuery Or<TTag>(TTag value) => this;
                public EventTagQuery Or<TEvent, TTag>(TTag value) => this;
                public EventTagQuery AndEventsOfType<T1>() => this;
                public EventTagQuery AndEventsOfType<T1, T2>() => this;
            }

            public interface IEventBoundary<out T> where T : class
            {
                T? Aggregate { get; }
                void AppendOne(object @event);
                void AppendMany(params object[] events);
                void AppendMany(System.Collections.Generic.IEnumerable<object> events);
            }
        }
        """;

    const string TypesSource =
        """
        namespace Accounts;

        public record AccountId(System.Guid Value);
        public record CustomerId(System.Guid Value);

        public record Split(AccountId AccountId);
        public record Ordered(AccountId AccountId);
        public record Reversed(AccountId AccountId);
        public record BlockBody(AccountId AccountId);
        public record HeadlessAnd(AccountId AccountId);
        public record ArrayTag(AccountId AccountId);
        public record ArrayOrTag(AccountId AccountId);
        public record ArrayAndEvent(AccountId AccountId);
        public record LocalQuery(AccountId AccountId);
        public record EmptyWrapper(AccountId AccountId);
        public record NestedWrapper(AccountId AccountId);
        public record OrdinaryOnBoundary(AccountId AccountId);
        public record TupleSiblings(AccountId AccountId);
        public record CollectionWrapper(AccountId AccountId);

        public sealed class AccountState;

        public record AccountOpened(AccountId AccountId);
        public record AccountClosed(AccountId AccountId);
        public record AccountCredited(AccountId AccountId);
        public record AccountDebited(AccountId AccountId);
        public record SplitDone(AccountId AccountId);
        public record OrderedDone(AccountId AccountId);
        public record ReversedDone(AccountId AccountId);
        public record BlockBodyDone(AccountId AccountId);
        public record HeadlessAndDone(AccountId AccountId);
        public record ArrayTagDone(AccountId AccountId);
        public record ArrayOrTagDone(AccountId AccountId);
        public record ArrayAndEventDone(AccountId AccountId);
        public record LocalQueryDone(AccountId AccountId);
        public record EmptyWrapperDone(AccountId AccountId);
        public record NestedWrapperDone(AccountId AccountId);
        public record NestedInnerDone(AccountId AccountId);
        public record BoundaryAppended(AccountId AccountId);
        public record BoundaryAppendedAlso(AccountId AccountId);
        public record BoundaryOrdinaryReturn(AccountId AccountId);
        public record TupleFirst(AccountId AccountId);
        public record TupleSecond(AccountId AccountId);
        public record CollectionDone(AccountId AccountId);

        public static class OrderedHandler
        {
            public static System.Threading.Tasks.Task<JasperFx.Events.Tags.EventTagQuery> LoadAsync(Ordered command) =>
                System.Threading.Tasks.Task.FromResult(
                    JasperFx.Events.Tags.EventTagQuery.For<AccountId>(command.AccountId)
                        .AndEventsOfType<AccountCredited>());

            public static JasperFx.Events.Tags.EventTagQuery Before(Ordered command) =>
                JasperFx.Events.Tags.EventTagQuery.For<AccountId>(command.AccountId)
                    .AndEventsOfType<AccountDebited>();

            public static OrderedDone Handle(
                Ordered command,
                [Wolverine.Persistence.EventSourcing.DcbModel] AccountState state) =>
                new(command.AccountId);
        }

        public static class ReversedHandler
        {
            public static JasperFx.Events.Tags.EventTagQuery Load(Reversed command) =>
                JasperFx.Events.Tags.EventTagQuery.For<AccountId>(command.AccountId)
                    .AndEventsOfType<AccountOpened>();

            public static JasperFx.Events.Tags.EventTagQuery Before(Reversed command) =>
                JasperFx.Events.Tags.EventTagQuery.For<AccountId>(command.AccountId)
                    .AndEventsOfType<AccountClosed>();

            public static ReversedDone Handle(
                Reversed command,
                [Wolverine.Persistence.EventSourcing.DcbModel] AccountState state) =>
                new(command.AccountId);
        }

        public static class BlockBodyHandler
        {
            public static JasperFx.Events.Tags.EventTagQuery Load(BlockBody command)
            {
                if (command.AccountId.Value == System.Guid.Empty)
                {
                    return JasperFx.Events.Tags.EventTagQuery.For<AccountId>(command.AccountId);
                }

                return JasperFx.Events.Tags.EventTagQuery.For<AccountId>(command.AccountId).AndEventsOfType<AccountOpened>();
            }

            public static BlockBodyDone Handle(
                BlockBody command,
                [Wolverine.Persistence.EventSourcing.DcbModel] AccountState state) =>
                new(command.AccountId);
        }

        public static class HeadlessAndHandler
        {
            public static JasperFx.Events.Tags.EventTagQuery Load(HeadlessAnd command) =>
                new JasperFx.Events.Tags.EventTagQuery().AndEventsOfType<AccountOpened>();

            public static HeadlessAndDone Handle(
                HeadlessAnd command,
                [Wolverine.Persistence.EventSourcing.DcbModel] AccountState state) =>
                new(command.AccountId);
        }

        public static class ArrayTagHandler
        {
            public static JasperFx.Events.Tags.EventTagQuery Load(ArrayTag command) =>
                JasperFx.Events.Tags.EventTagQuery.For<AccountId[]>([command.AccountId]);

            public static ArrayTagDone Handle(
                ArrayTag command,
                [Wolverine.Persistence.EventSourcing.DcbModel] AccountState state) =>
                new(command.AccountId);
        }

        public static class ArrayOrTagHandler
        {
            public static JasperFx.Events.Tags.EventTagQuery Load(ArrayOrTag command) =>
                JasperFx.Events.Tags.EventTagQuery.For<AccountId>(command.AccountId)
                    .Or<AccountId[]>([command.AccountId]);

            public static ArrayOrTagDone Handle(
                ArrayOrTag command,
                [Wolverine.Persistence.EventSourcing.DcbModel] AccountState state) =>
                new(command.AccountId);
        }

        public static class ArrayAndEventHandler
        {
            public static JasperFx.Events.Tags.EventTagQuery Load(ArrayAndEvent command) =>
                JasperFx.Events.Tags.EventTagQuery.For<AccountId>(command.AccountId)
                    .AndEventsOfType<AccountOpened[], AccountClosed>();

            public static ArrayAndEventDone Handle(
                ArrayAndEvent command,
                [Wolverine.Persistence.EventSourcing.DcbModel] AccountState state) =>
                new(command.AccountId);
        }

        public static class LocalQueryHandler
        {
            public static JasperFx.Events.Tags.EventTagQuery Load(LocalQuery command)
            {
                var query = JasperFx.Events.Tags.EventTagQuery.For<AccountId>(command.AccountId)
                    .Or<AccountOpened, CustomerId>(new CustomerId(command.AccountId.Value));
                return query;
            }

            public static LocalQueryDone Handle(
                LocalQuery command,
                [Wolverine.Persistence.EventSourcing.DcbModel] AccountState state) =>
                new(command.AccountId);
        }

        public static class EmptyWrapperHandler
        {
            public static JasperFx.Events.Tags.EventTagQuery Load(EmptyWrapper command) =>
                JasperFx.Events.Tags.EventTagQuery.For<AccountId>(command.AccountId);

            public static Wolverine.Marten.Events Handle(
                EmptyWrapper command,
                [Wolverine.Persistence.EventSourcing.DcbModel] AccountState state) =>
                new Wolverine.Marten.Events();
        }

        public static class NestedWrapperHandler
        {
            public static JasperFx.Events.Tags.EventTagQuery Load(NestedWrapper command) =>
                JasperFx.Events.Tags.EventTagQuery.For<AccountId>(command.AccountId);

            public static Wolverine.Marten.Events Handle(
                NestedWrapper command,
                [Wolverine.Persistence.EventSourcing.DcbModel] AccountState state) =>
                new Wolverine.Marten.Events(new Wolverine.Marten.Events { new NestedInnerDone(command.AccountId) });
        }

        public static class OrdinaryOnBoundaryHandler
        {
            public static JasperFx.Events.Tags.EventTagQuery Before(OrdinaryOnBoundary command) =>
                JasperFx.Events.Tags.EventTagQuery.For<AccountId>(command.AccountId);

            public static BoundaryOrdinaryReturn Handle(
                OrdinaryOnBoundary command,
                [Wolverine.Persistence.EventSourcing.DcbModel] JasperFx.Events.Tags.IEventBoundary<AccountState> boundary)
            {
                boundary.AppendOne(new BoundaryAppended(command.AccountId));
                boundary.AppendMany(new BoundaryAppendedAlso(command.AccountId), new BoundaryAppended(command.AccountId));
                return new BoundaryOrdinaryReturn(command.AccountId);
            }
        }

        public static class TupleSiblingsHandler
        {
            public static JasperFx.Events.Tags.EventTagQuery Load(TupleSiblings command) =>
                JasperFx.Events.Tags.EventTagQuery.For<AccountId>(command.AccountId);

            public static (TupleFirst, TupleSecond) Handle(
                TupleSiblings command,
                [Wolverine.Persistence.EventSourcing.DcbModel] AccountState state) =>
                (new(command.AccountId), new(command.AccountId));
        }

        public static class CollectionWrapperHandler
        {
            public static JasperFx.Events.Tags.EventTagQuery Load(CollectionWrapper command) =>
                JasperFx.Events.Tags.EventTagQuery.For<AccountId>(command.AccountId);

            public static Wolverine.Persistence.EventSourcing.EventsToAppend Handle(
                CollectionWrapper command,
                [Wolverine.Persistence.EventSourcing.DcbModel] AccountState state) =>
                [new CollectionDone(command.AccountId)];
        }
        """;

    // Path order (CompanionA before CompanionZ) disagrees with companion name order (Before before Load) and the
    // winner sits later in its file than the loser sits in its own, so only the path decides.
    const string SplitCompanionASource =
        """
        // padding so the first companion sits at a larger position than the one in the later file
        // padding so the first companion sits at a larger position than the one in the later file
        // padding so the first companion sits at a larger position than the one in the later file
        namespace Accounts;

        public static partial class SplitHandler
        {
            public static JasperFx.Events.Tags.EventTagQuery Load(Split command) =>
                JasperFx.Events.Tags.EventTagQuery.For<AccountId>(command.AccountId)
                    .AndEventsOfType<AccountOpened>();
        }
        """;

    const string SplitCompanionZSource =
        """
        namespace Accounts;

        public static partial class SplitHandler
        {
            public static JasperFx.Events.Tags.EventTagQuery Before(Split command) =>
                JasperFx.Events.Tags.EventTagQuery.For<AccountId>(command.AccountId)
                    .AndEventsOfType<AccountClosed>();

            public static SplitDone Handle(
                Split command,
                [Wolverine.Persistence.EventSourcing.DcbModel] AccountState state) =>
                new(command.AccountId);
        }
        """;

    static readonly IReadOnlyList<MetadataReference> _references =
    [
        .. ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES"))
            .Split(Path.PathSeparator)
            .Select(_ => MetadataReference.CreateFromFile(_))
    ];

    protected DotNetProjectCompilation Project = null!;

    void Establish()
    {
        var trees = new[]
        {
            CSharpSyntaxTree.ParseText(FrameworkSource, path: "/workspace/Framework.cs"),
            CSharpSyntaxTree.ParseText(TypesSource, path: "/workspace/Accounts/Types.cs"),
            CSharpSyntaxTree.ParseText(SplitCompanionASource, path: "/workspace/Accounts/CompanionA.cs"),
            CSharpSyntaxTree.ParseText(SplitCompanionZSource, path: "/workspace/Accounts/CompanionZ.cs")
        };
        var compilation = CSharpCompilation.Create(
            "Accounts",
            trees,
            _references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, nullableContextOptions: NullableContextOptions.Enable));
        Project = new DotNetProjectCompilation
        {
            Name = "Accounts",
            ProjectPath = "/workspace/Accounts/Accounts.csproj",
            SourceRoot = "/workspace",
            Compilation = compilation,
            AuthoredSyntaxTrees = new HashSet<SyntaxTree>(trees)
        };
    }
}
