// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Cratis.CritterStack.Screenplay.given;

public class a_wolverine_dcb_application : Specification
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
            public interface IResponseAware : Wolverine.Configuration.IWolverineReturnType;
            public interface ISideEffect : Wolverine.Configuration.IWolverineReturnType;
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
                public static EventTagQuery FromConditions(System.Collections.Generic.IEnumerable<EventTagQueryCondition> conditions) => new();
                public EventTagQuery Or<TTag>(TTag value) => this;
                public EventTagQuery Or<TEvent, TTag>(TTag value) => this;
                public EventTagQuery AndEventsOfType<T1>() => this;
                public EventTagQuery AndEventsOfType<T1, T2>() => this;
                public EventTagQuery AndEventsOfType<T1, T2, T3>() => this;
                public EventTagQuery AndEventsOfType<T1, T2, T3, T4>() => this;
                public EventTagQuery AndEventsOfType<T1, T2, T3, T4, T5>() => this;
                public EventTagQuery AndEventsOfType<T1, T2, T3, T4, T5, T6>() => this;
            }

            public sealed record EventTagQueryCondition(System.Type? EventType, System.Type TagType, object Value);

            public interface IEventBoundary<out T> where T : class
            {
                T? Aggregate { get; }
                void AppendOne(object @event);
                void AppendMany(params object[] events);
                void AppendMany(System.Collections.Generic.IEnumerable<object> events);
            }
        }
        """;

    const string ApplicationSource =
        """
        namespace Accounts;

        public record AccountId(System.Guid Value);
        public record CustomerId(System.Guid Value);

        public record ChangeAccount(AccountId AccountId, CustomerId CustomerId);
        public record NoteAccount(AccountId AccountId);
        public record ReviewAccount(AccountId AccountId);
        public record BoundaryAccount(AccountId AccountId);
        public record WrappedAccount(AccountId AccountId);
        public record MixedWrappedAccount(AccountId AccountId);
        public record ValueTaskWrappedAccount(AccountId AccountId);
        public record NoRequestCompanion(AccountId AccountId);
        public record CollectedAccount(AccountId AccountId);
        public record OpaqueQuery(AccountId AccountId);
        public record BranchedQuery(AccountId AccountId, bool Active);
        public record FromConditionsQuery(AccountId AccountId);
        public record OpaquePayload(AccountId AccountId);
        public record ForOnly(AccountId AccountId);
        public record DerivedAttribute(AccountId AccountId);
        public record MissingCompanion(AccountId AccountId);
        public record MultipleModels(AccountId AccountId);
        public record InvalidModel(AccountId AccountId);
        public record InvalidBoundary(AccountId AccountId);
        public record UnrelatedAttribute(AccountId AccountId);
        public record NonDcb(AccountId AccountId);
        public record GeneratedCompanion(AccountId AccountId);
        public record MismatchedCompanion(AccountId AccountId);

        public sealed class AccountState
        {
            public decimal Balance { get; set; }
        }

        public record AccountOpened(AccountId AccountId);
        public record AccountCredited(AccountId AccountId);
        public record AccountDebited(AccountId AccountId);
        public record AccountClosed(AccountId AccountId);
        public record AccountChanged(AccountId AccountId);
        public record AccountNoted(AccountId AccountId);
        public record AccountReviewed(AccountId AccountId);
        public record AccountAudited(AccountId AccountId);
        public record AccountFlagged(AccountId AccountId);
        public record AccountEscalated(AccountId AccountId);
        public record AccountWrapped(AccountId AccountId);
        public record AccountWrappedAgain(AccountId AccountId);
        public record AccountWrapperEvent(AccountId AccountId);
        public record AccountSiblingEvent(AccountId AccountId);
        public record AccountValueTaskWrapped(AccountId AccountId);
        public record AccountNoRequestChanged(AccountId AccountId);
        public record AccountCollected(AccountId AccountId);
        public record AccountCollectedAgain(AccountId AccountId);
        public record AccountOpaqueQueryChanged(AccountId AccountId);
        public record AccountBranchedQueryChanged(AccountId AccountId);
        public record AccountFromConditionsChanged(AccountId AccountId);
        public record AccountOpaquePayload(AccountId AccountId);
        public record AccountOpaqueBoundaryPayload(AccountId AccountId);
        public record AccountForOnlyChanged(AccountId AccountId);
        public record AccountDerivedChanged(AccountId AccountId);
        public record AccountMissingChanged(AccountId AccountId);
        public record AccountMultipleChanged(AccountId AccountId);
        public record AccountInvalidChanged(AccountId AccountId);
        public record AccountInvalidBoundaryChanged(AccountId AccountId);
        public record AccountUnrelatedChanged(AccountId AccountId);
        public record AccountNonDcbChanged(AccountId AccountId);
        public record AccountGeneratedCompanionChanged(AccountId AccountId);
        public record AccountMismatchedCompanionChanged(AccountId AccountId);
        public record BoundaryReturn(AccountId AccountId);
        public sealed class AccountResponse : Wolverine.IResponseAware;
        public sealed class AccountEffect : Wolverine.ISideEffect;
        public sealed class AccountSaga : Wolverine.Saga;

        public sealed class CustomDcbModelAttribute : Wolverine.Persistence.EventSourcing.DcbModelAttribute;
        public sealed class DcbModelAttribute : System.Attribute;

        public interface IDerivedBoundary : JasperFx.Events.Tags.IEventBoundary<AccountState>;

        public static class ChangeAccountHandler
        {
            public static JasperFx.Events.Tags.EventTagQuery Load(ChangeAccount command) =>
                new JasperFx.Events.Tags.EventTagQuery()
                    .Or<AccountId>(command.AccountId)
                    .Or<AccountOpened, CustomerId>(command.CustomerId)
                    .AndEventsOfType<AccountCredited, AccountDebited>()
                    .Or<AccountClosed, AccountId>(command.AccountId);

            public static (AccountChanged, AccountResponse, AccountEffect, AccountSaga, Wolverine.OutgoingMessages) Handle(
                ChangeAccount command,
                [Wolverine.Persistence.EventSourcing.DcbModel] AccountState state) =>
                (new(command.AccountId), new(), new(), new(), new());
        }

        public static class NoteAccountHandler
        {
            public static System.Threading.Tasks.Task<JasperFx.Events.Tags.EventTagQuery> LoadAsync(NoteAccount command) =>
                System.Threading.Tasks.Task.FromResult(
                    JasperFx.Events.Tags.EventTagQuery.For<AccountId>(command.AccountId)
                        .AndEventsOfType<AccountOpened>());

            public static System.Threading.Tasks.Task<AccountNoted> Handle(
                NoteAccount command,
                [Wolverine.Persistence.EventSourcing.DcbModel] AccountState? state) =>
                System.Threading.Tasks.Task.FromResult(new AccountNoted(command.AccountId));
        }

        public static class ReviewAccountHandler
        {
            public static System.Threading.Tasks.ValueTask<JasperFx.Events.Tags.EventTagQuery> BeforeAsync(ReviewAccount command)
            {
                var query = JasperFx.Events.Tags.EventTagQuery.For<AccountId>(command.AccountId)
                    .AndEventsOfType<AccountCredited>();
                return System.Threading.Tasks.ValueTask.FromResult(query);
            }

            public static AccountReviewed Handle(
                ReviewAccount command,
                [Wolverine.Persistence.EventSourcing.DcbModel] AccountState state) =>
                new(command.AccountId);
        }

        public static class BoundaryAccountHandler
        {
            public static JasperFx.Events.Tags.EventTagQuery Before(BoundaryAccount command) =>
                JasperFx.Events.Tags.EventTagQuery.For<AccountId>(command.AccountId)
                    .AndEventsOfType<AccountOpened, AccountCredited>();

            public static BoundaryReturn Handle(
                BoundaryAccount command,
                [Wolverine.Persistence.EventSourcing.DcbModel] JasperFx.Events.Tags.IEventBoundary<AccountState> boundary)
            {
                boundary.AppendOne(new AccountFlagged(command.AccountId));
                boundary.AppendMany(
                [
                    new AccountEscalated(command.AccountId),
                    new AccountAudited(command.AccountId)
                ]);
                boundary.AppendMany(new System.Collections.Generic.List<object>
                {
                    new AccountDebited(command.AccountId)
                });
                var opaque = new AccountOpaqueBoundaryPayload(command.AccountId);
                boundary.AppendOne(opaque);
                return new(command.AccountId);
            }
        }

        public static class WrappedAccountHandler
        {
            public static JasperFx.Events.Tags.EventTagQuery Load(WrappedAccount command) =>
                JasperFx.Events.Tags.EventTagQuery.For<AccountId>(command.AccountId)
                    .AndEventsOfType<AccountOpened>();

            public static System.Threading.Tasks.Task<Wolverine.Persistence.EventSourcing.EventsToAppend> Handle(
                WrappedAccount command,
                [Wolverine.Persistence.EventSourcing.DcbModel] JasperFx.Events.Tags.IEventBoundary<AccountState> boundary) =>
                System.Threading.Tasks.Task.FromResult(new Wolverine.Persistence.EventSourcing.EventsToAppend
                {
                    new AccountWrapped(command.AccountId),
                    new AccountWrappedAgain(command.AccountId)
                });
        }

        public static class MixedWrappedAccountHandler
        {
            public static JasperFx.Events.Tags.EventTagQuery Load(MixedWrappedAccount command) =>
                JasperFx.Events.Tags.EventTagQuery.For<AccountId>(command.AccountId)
                    .AndEventsOfType<AccountOpened>();

            public static (Wolverine.Persistence.EventSourcing.EventsToAppend, AccountSiblingEvent) Handle(
                MixedWrappedAccount command,
                [Wolverine.Persistence.EventSourcing.DcbModel] AccountState state) =>
                (new Wolverine.Persistence.EventSourcing.EventsToAppend
                {
                    new AccountWrapperEvent(command.AccountId)
                }, new AccountSiblingEvent(command.AccountId));
        }

        public static class ValueTaskWrappedAccountHandler
        {
            public static JasperFx.Events.Tags.EventTagQuery Load(ValueTaskWrappedAccount command) =>
                JasperFx.Events.Tags.EventTagQuery.For<AccountId>(command.AccountId)
                    .AndEventsOfType<AccountOpened>();

            public static System.Threading.Tasks.ValueTask<Wolverine.Persistence.EventSourcing.EventsToAppend> Handle(
                ValueTaskWrappedAccount command,
                [Wolverine.Persistence.EventSourcing.DcbModel] JasperFx.Events.Tags.IEventBoundary<AccountState> boundary) =>
                System.Threading.Tasks.ValueTask.FromResult(new Wolverine.Persistence.EventSourcing.EventsToAppend
                {
                    new AccountValueTaskWrapped(command.AccountId)
                });
        }

        public static class NoRequestCompanionHandler
        {
            public static JasperFx.Events.Tags.EventTagQuery Load() =>
                new JasperFx.Events.Tags.EventTagQuery().Or<AccountId>(new AccountId(System.Guid.Empty));

            public static AccountNoRequestChanged Handle(
                NoRequestCompanion command,
                [Wolverine.Persistence.EventSourcing.DcbModel] AccountState state) =>
                new(command.AccountId);
        }

        public static class CollectedAccountHandler
        {
            public static JasperFx.Events.Tags.EventTagQuery Load(CollectedAccount command) =>
                new JasperFx.Events.Tags.EventTagQuery().Or<AccountId>(command.AccountId);

            public static object[] Handle(
                CollectedAccount command,
                [Wolverine.Persistence.EventSourcing.DcbModel] AccountState state) =>
                [
                    new AccountCollected(command.AccountId),
                    new AccountCollectedAgain(command.AccountId)
                ];
        }

        public static class ForOnlyHandler
        {
            public static JasperFx.Events.Tags.EventTagQuery Load(ForOnly command) =>
                JasperFx.Events.Tags.EventTagQuery.For<AccountId>(command.AccountId);

            public static AccountForOnlyChanged Handle(
                ForOnly command,
                [Wolverine.Persistence.EventSourcing.DcbModel] AccountState state) =>
                new(command.AccountId);
        }

        public static class DerivedAttributeHandler
        {
            public static JasperFx.Events.Tags.EventTagQuery Load(DerivedAttribute command) =>
                new JasperFx.Events.Tags.EventTagQuery().Or<AccountId>(command.AccountId);

            public static AccountDerivedChanged Handle(
                DerivedAttribute command,
                [CustomDcbModel] AccountState state) =>
                new(command.AccountId);
        }

        public static class OpaqueQueryHandler
        {
            public static JasperFx.Events.Tags.EventTagQuery Build(OpaqueQuery command) =>
                JasperFx.Events.Tags.EventTagQuery.For<AccountId>(command.AccountId);
            public static JasperFx.Events.Tags.EventTagQuery Load(OpaqueQuery command) => Build(command);

            public static AccountOpaqueQueryChanged Handle(
                OpaqueQuery command,
                [Wolverine.Persistence.EventSourcing.DcbModel] AccountState state) =>
                new(command.AccountId);
        }

        public static class BranchedQueryHandler
        {
            public static JasperFx.Events.Tags.EventTagQuery Load(BranchedQuery command)
            {
                if (command.Active)
                {
                    return JasperFx.Events.Tags.EventTagQuery.For<AccountId>(command.AccountId);
                }
                return new JasperFx.Events.Tags.EventTagQuery().Or<AccountId>(command.AccountId);
            }

            public static AccountBranchedQueryChanged Handle(
                BranchedQuery command,
                [Wolverine.Persistence.EventSourcing.DcbModel] AccountState state) =>
                new(command.AccountId);
        }

        public static class FromConditionsQueryHandler
        {
            public static JasperFx.Events.Tags.EventTagQuery Load(FromConditionsQuery command) =>
                JasperFx.Events.Tags.EventTagQuery.FromConditions(
                [
                    new(typeof(AccountOpened), typeof(AccountId), command.AccountId)
                ]);

            public static AccountFromConditionsChanged Handle(
                FromConditionsQuery command,
                [Wolverine.Persistence.EventSourcing.DcbModel] AccountState state) =>
                new(command.AccountId);
        }

        public static class OpaquePayloadHandler
        {
            public static JasperFx.Events.Tags.EventTagQuery Load(OpaquePayload command) =>
                new JasperFx.Events.Tags.EventTagQuery().Or<AccountId>(command.AccountId);
            public static AccountOpaquePayload Build(OpaquePayload command) => new(command.AccountId);

            public static AccountOpaquePayload Handle(
                OpaquePayload command,
                [Wolverine.Persistence.EventSourcing.DcbModel] AccountState state) => Build(command);
        }

        public static class MissingCompanionHandler
        {
            public static AccountMissingChanged Handle(
                MissingCompanion command,
                [Wolverine.Persistence.EventSourcing.DcbModel] AccountState state) =>
                new(command.AccountId);
        }

        public static class MultipleModelsHandler
        {
            public static JasperFx.Events.Tags.EventTagQuery Load(MultipleModels command) =>
                new JasperFx.Events.Tags.EventTagQuery().Or<AccountId>(command.AccountId);

            public static AccountMultipleChanged Handle(
                MultipleModels command,
                [Wolverine.Persistence.EventSourcing.DcbModel] AccountState first,
                [Wolverine.Persistence.EventSourcing.DcbModel] AccountState second) =>
                new(command.AccountId);
        }

        public static class InvalidModelHandler
        {
            public static JasperFx.Events.Tags.EventTagQuery Load(InvalidModel command) =>
                new JasperFx.Events.Tags.EventTagQuery().Or<AccountId>(command.AccountId);

            public static AccountInvalidChanged Handle(
                InvalidModel command,
                [Wolverine.Persistence.EventSourcing.DcbModel] int state) =>
                new(command.AccountId);
        }

        public static class InvalidBoundaryHandler
        {
            public static JasperFx.Events.Tags.EventTagQuery Load(InvalidBoundary command) =>
                new JasperFx.Events.Tags.EventTagQuery().Or<AccountId>(command.AccountId);

            public static AccountInvalidBoundaryChanged Handle(
                InvalidBoundary command,
                [Wolverine.Persistence.EventSourcing.DcbModel] IDerivedBoundary boundary) =>
                new(command.AccountId);
        }

        public static class UnrelatedAttributeHandler
        {
            public static JasperFx.Events.Tags.EventTagQuery Load(UnrelatedAttribute command) =>
                new JasperFx.Events.Tags.EventTagQuery().Or<AccountId>(command.AccountId);

            public static AccountUnrelatedChanged Handle(
                UnrelatedAttribute command,
                [Accounts.DcbModel] AccountState state) =>
                new(command.AccountId);
        }

        public static class NonDcbHandler
        {
            public static AccountNonDcbChanged Handle(NonDcb command) => new(command.AccountId);
        }

        public static class MismatchedCompanionHandler
        {
            public static JasperFx.Events.Tags.EventTagQuery Load(NonDcb command) =>
                JasperFx.Events.Tags.EventTagQuery.For<AccountId>(command.AccountId);

            public static AccountMismatchedCompanionChanged Handle(
                MismatchedCompanion command,
                [Wolverine.Persistence.EventSourcing.DcbModel] AccountState state) =>
                new(command.AccountId);
        }

        public static partial class GeneratedCompanionHandler
        {
            public static AccountGeneratedCompanionChanged Handle(
                GeneratedCompanion command,
                [Wolverine.Persistence.EventSourcing.DcbModel] AccountState state) =>
                new(command.AccountId);
        }
        """;

    const string GeneratedApplicationSource =
        """
        namespace Accounts;

        public static partial class GeneratedCompanionHandler
        {
            public static JasperFx.Events.Tags.EventTagQuery Load(GeneratedCompanion command) =>
                JasperFx.Events.Tags.EventTagQuery.For<AccountId>(command.AccountId)
                    .AndEventsOfType<AccountOpened>();
        }

        public record GeneratedCommand(AccountId AccountId);
        public record GeneratedEvent(AccountId AccountId);

        public static class GeneratedDcbHandler
        {
            public static JasperFx.Events.Tags.EventTagQuery Load(GeneratedCommand command) =>
                JasperFx.Events.Tags.EventTagQuery.For<AccountId>(command.AccountId);

            public static GeneratedEvent Handle(
                GeneratedCommand command,
                [Wolverine.Persistence.EventSourcing.DcbModel] AccountState state) =>
                new(command.AccountId);
        }
        """;

    static readonly IReadOnlyList<MetadataReference> _references =
    [
        .. ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")!)
            .Split(Path.PathSeparator)
            .Select(_ => MetadataReference.CreateFromFile(_))
    ];

    protected DotNetProjectCompilation Project = null!;

    void Establish()
    {
        var frameworkTree = CSharpSyntaxTree.ParseText(FrameworkSource, path: "/workspace/Framework.cs");
        var applicationTree = CSharpSyntaxTree.ParseText(ApplicationSource, path: "/workspace/Accounts/Dcb.cs");
        var generatedTree = CSharpSyntaxTree.ParseText(GeneratedApplicationSource, path: "/workspace/Accounts/Generated.cs");
        var compilation = CSharpCompilation.Create(
            "Accounts",
            [frameworkTree, applicationTree, generatedTree],
            _references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, nullableContextOptions: NullableContextOptions.Enable));
        Project = new DotNetProjectCompilation
        {
            Name = "Accounts",
            ProjectPath = "/workspace/Accounts/Accounts.csproj",
            SourceRoot = "/workspace",
            Compilation = compilation,
            AuthoredSyntaxTrees = new HashSet<SyntaxTree> { frameworkTree, applicationTree }
        };
    }
}
