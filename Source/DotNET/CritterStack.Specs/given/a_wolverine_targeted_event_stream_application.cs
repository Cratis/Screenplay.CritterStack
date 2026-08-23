// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Cratis.CritterStack.Screenplay.given;

public class a_wolverine_targeted_event_stream_application : Specification
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
        }

        namespace Wolverine.Http
        {
            public abstract class WolverineHttpMethodAttribute(string route) : System.Attribute;
            public class WolverinePostAttribute(string route) : WolverineHttpMethodAttribute(route);
        }

        namespace Wolverine.Persistence.EventSourcing
        {
            public enum ModelConcurrencyStyle { Optimistic, Exclusive }

            public class WriteModelAttribute : System.Attribute
            {
                public WriteModelAttribute() { }
                public WriteModelAttribute(string? routeOrParameterName) => RouteOrParameterName = routeOrParameterName;
                public string? RouteOrParameterName { get; }
                public string? VersionSource { get; set; }
                public ModelConcurrencyStyle LoadStyle { get; set; } = ModelConcurrencyStyle.Optimistic;
                public bool AlwaysEnforceConsistency { get; set; }
            }
        }

        namespace Wolverine.Marten
        {
            public enum ConcurrencyStyle { Optimistic, Exclusive }

            public class WriteAggregateAttribute : System.Attribute
            {
                public WriteAggregateAttribute() { }
                public WriteAggregateAttribute(string? routeOrParameterName) => RouteOrParameterName = routeOrParameterName;
                public string? RouteOrParameterName { get; }
                public string? VersionSource { get; set; }
                public ConcurrencyStyle LoadStyle { get; set; } = ConcurrencyStyle.Optimistic;
                public bool AlwaysEnforceConsistency { get; set; }
            }
        }

        namespace Wolverine.Http.Marten
        {
            public class AggregateAttribute : System.Attribute
            {
                public AggregateAttribute() { }
                public AggregateAttribute(string? routeOrParameterName) => RouteOrParameterName = routeOrParameterName;
                public string? RouteOrParameterName { get; }
                public string? VersionSource { get; set; }
            }
        }

        namespace JasperFx.Events
        {
            public interface IEventStream<T>
            {
                T? Aggregate { get; }
                void AppendOne(object @event);
                void AppendMany(params object[] events);
                void AppendMany(System.Collections.Generic.IEnumerable<object> events);
            }
        }

        namespace Marten.Events.Aggregation
        {
            public interface IEventStream<T>
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
        namespace Transfers;

        public record Transfer(
            System.Guid AccountId,
            System.Guid OrderId,
            long AccountVersion,
            int OrderVersion);
        public record MoveFunds(
            System.Guid FromId,
            System.Guid ToId,
            long FromVersion,
            long ToVersion);
        public record UnmarkedAppend(System.Guid Id);
        public record SameNamedAttributeAppend(System.Guid Id);
        public record DerivedStreamAppend(System.Guid Id);
        public record UnresolvedAppend(System.Guid Id);
        public record BoundaryCommand(System.Guid Id);
        public partial record GeneratedMemberCommand(System.Guid ActualId);
        public record UnrelatedCommand(System.Guid Id);
        public record SagaOnlyTrigger(System.Guid Id);
        public record SagaMixedTrigger(System.Guid Id);
        public record GeneratedSagaTrigger(System.Guid Id);
        public record InspectAccount(System.Guid AccountId);
        public record ConventionalVersionCommand(System.Guid FromId, System.Guid ToId, long Version);
        public record FalseIdentityCommand([property: Transfers.Identity] System.Guid Candidate);
        public record LegacyAppend(System.Guid AccountId);

        public class Account
        {
            public System.Guid Id { get; set; }
            public decimal Balance { get; set; }
        }

        public class Order
        {
            public System.Guid Id { get; set; }
            public string Status { get; set; } = string.Empty;
        }

        public record AccountDebited(decimal Amount);
        public record AccountAdjusted(decimal Amount);
        public record AccountClosed(string Reason);
        public record AccountReviewed(string Reviewer);
        public record AccountApproved(string Approver);
        public record OrderCredited(decimal Amount);
        public record OrderConfirmed(System.Guid OrderId);
        public record OrderPacked(System.Guid OrderId);
        public record OrderShipped(System.Guid OrderId);
        public record FundsWithdrawn(decimal Amount);
        public record FundsDeposited(decimal Amount);
        public record FundsMoved(decimal Amount);
        public record UnmarkedEvent(System.Guid Id);
        public record SameNamedAttributeEvent(System.Guid Id);
        public record DerivedStreamEvent(System.Guid Id);
        public record BoundaryEvent(System.Guid Id);
        public record GeneratedMemberEvent(System.Guid Id);
        public record AliasedEvent(System.Guid Id);
        public record OpaqueLeadingEvent(System.Guid Id);
        public record OpaqueObjectEvent(System.Guid Id);
        public record DynamicEvent(System.Guid Id);
        public record VariableEvent(System.Guid Id);
        public record HelperEvent(System.Guid Id);
        public record NestedContainerEvent(System.Guid Id);
        public record UnrelatedEvent(System.Guid Id);
        public record TransferFollowUp(System.Guid AccountId);
        public record BoundaryCascade(System.Guid Id);
        public record UnrelatedCascade(System.Guid Id);
        public record SagaFollowUp(System.Guid Id);
        public record RouteFollowUp(System.Guid Id);
        public record RouteAppended(System.Guid Id);
        public record LegacyAppended(System.Guid Id);

        public sealed class BoundaryResponse : Wolverine.IResponseAware;
        public sealed class BoundaryEffect : Wolverine.ISideEffect;
        public sealed class WriteModelAttribute : System.Attribute;
        public sealed class IdentityAttribute : System.Attribute;
        public sealed class TransferSaga : Wolverine.Saga;
        public sealed class BoundarySaga : Wolverine.Saga;
        public partial class GeneratedBaseSaga;

        public interface IAccountEventStream : JasperFx.Events.IEventStream<Account>;

        public sealed class UnrelatedStream<T>
        {
            public void AppendOne(object @event) { }
            public void AppendMany(params object[] events) { }
        }

        public static class TransferHandler
        {
            public static TransferFollowUp Handle(
                Transfer command,
                [Wolverine.Persistence.EventSourcing.WriteModel(
                    nameof(Transfer.AccountId),
                    VersionSource = nameof(Transfer.AccountVersion),
                    LoadStyle = Wolverine.Persistence.EventSourcing.ModelConcurrencyStyle.Exclusive,
                    AlwaysEnforceConsistency = true)]
                JasperFx.Events.IEventStream<Account> accountStream,
                [Wolverine.Marten.WriteAggregate(
                    nameof(Transfer.OrderId),
                    VersionSource = nameof(Transfer.OrderVersion),
                    LoadStyle = Wolverine.Marten.ConcurrencyStyle.Exclusive)]
                JasperFx.Events.IEventStream<Order> orderStream)
            {
                (accountStream!).AppendOne(new AccountDebited(10));
                orderStream.AppendMany(new OrderCredited(10), new OrderConfirmed(command.OrderId));
                accountStream.AppendMany(new object[]
                {
                    new AccountAdjusted(1),
                    new AccountClosed("complete")
                });
                orderStream.AppendMany(
                [
                    new OrderPacked(command.OrderId),
                    new OrderShipped(command.OrderId)
                ]);
                accountStream.AppendMany(new System.Collections.Generic.List<object>
                {
                    new AccountReviewed("reviewer"),
                    new AccountApproved("approver")
                });
                return new(command.AccountId);
            }
        }

        public static class MoveFundsHandler
        {
            public static void Handle(
                MoveFunds command,
                [Wolverine.Marten.WriteAggregate(
                    nameof(MoveFunds.FromId),
                    VersionSource = nameof(MoveFunds.FromVersion))]
                JasperFx.Events.IEventStream<Account> source,
                [Wolverine.Persistence.EventSourcing.WriteModel(
                    nameof(MoveFunds.ToId),
                    VersionSource = nameof(MoveFunds.ToVersion))]
                JasperFx.Events.IEventStream<Account> destination)
            {
                source.AppendOne(new FundsWithdrawn(5));
                source.AppendOne(new FundsMoved(-5));
                destination.AppendOne(new FundsDeposited(5));
                destination.AppendOne(new FundsMoved(5));
            }
        }

        public static class InspectAccountHandler
        {
            public static void Handle(
                InspectAccount command,
                [Wolverine.Persistence.EventSourcing.WriteModel(nameof(InspectAccount.AccountId))]
                JasperFx.Events.IEventStream<Account> stream)
            {
                _ = command;
                _ = stream;
            }
        }

        public static class ConventionalVersionHandler
        {
            public static void Handle(
                ConventionalVersionCommand command,
                [Wolverine.Persistence.EventSourcing.WriteModel(nameof(ConventionalVersionCommand.FromId))]
                JasperFx.Events.IEventStream<Account> source,
                [Wolverine.Persistence.EventSourcing.WriteModel(nameof(ConventionalVersionCommand.ToId))]
                JasperFx.Events.IEventStream<Account> destination)
            {
                _ = command;
                _ = source;
                _ = destination;
            }
        }

        public static class FalseIdentityHandler
        {
            public static void Handle(
                FalseIdentityCommand command,
                [Wolverine.Persistence.EventSourcing.WriteModel]
                JasperFx.Events.IEventStream<Account> stream)
            {
                _ = command;
                _ = stream;
            }
        }

        public static class LegacyAppendHandler
        {
            public static void Handle(
                LegacyAppend command,
                [Wolverine.Marten.WriteAggregate(nameof(LegacyAppend.AccountId))]
                Marten.Events.Aggregation.IEventStream<Account> stream) =>
                stream.AppendOne(new LegacyAppended(command.AccountId));
        }

        public static class RouteEndpoints
        {
            [Wolverine.Http.WolverinePost("/accounts/{accountId}")]
            public static RouteFollowUp RouteAppend(
                System.Guid accountId,
                long version,
                [Wolverine.Persistence.EventSourcing.WriteModel("accountId", VersionSource = "version")]
                JasperFx.Events.IEventStream<Account> stream)
            {
                stream.AppendOne(new RouteAppended(accountId));
                return new(accountId);
            }
        }

        public static class UnmarkedAppendHandler
        {
            public static TransferFollowUp Handle(
                UnmarkedAppend command,
                JasperFx.Events.IEventStream<Account> stream)
            {
                stream.AppendMany();
                stream.AppendOne(new UnmarkedEvent(command.Id));
                return new(command.Id);
            }
        }

        public static class SameNamedAttributeAppendHandler
        {
            public static void Handle(
                SameNamedAttributeAppend command,
                [Transfers.WriteModel] JasperFx.Events.IEventStream<Account> stream) =>
                stream.AppendOne(new SameNamedAttributeEvent(command.Id));
        }

        public static class DerivedStreamAppendHandler
        {
            public static void Handle(DerivedStreamAppend command, IAccountEventStream stream) =>
                ((JasperFx.Events.IEventStream<Account>)stream).AppendOne(new DerivedStreamEvent(command.Id));
        }

        public static class UnresolvedAppendHandler
        {
            public static void Handle(
                UnresolvedAppend command,
                JasperFx.Events.IEventStream<Account> stream,
                System.Collections.Generic.IEnumerable<object> events,
                object opaque,
                dynamic dynamicEvent)
            {
                var alias = stream;
                var localEvent = new VariableEvent(command.Id);
                alias.AppendOne(new AliasedEvent(command.Id));
                stream.AppendMany(events);
                stream.AppendMany([new OpaqueLeadingEvent(command.Id), .. events]);
                stream.AppendOne(opaque);
                stream.AppendOne(dynamicEvent);
                stream.AppendOne(localEvent);
                stream.AppendMany(new object[] { new object[] { new NestedContainerEvent(command.Id) } });

                void AppendHelper() => stream.AppendOne(new HelperEvent(command.Id));
                AppendHelper();
            }
        }

        public static class GeneratedMemberHandler
        {
            public static void Handle(
                GeneratedMemberCommand command,
                [Wolverine.Persistence.EventSourcing.WriteModel]
                JasperFx.Events.IEventStream<Account> stream) =>
                stream.AppendOne(new GeneratedMemberEvent(command.ActualId));
        }

        public static class BoundaryHandler
        {
            public static (BoundaryResponse, BoundaryEffect, BoundaryCascade, BoundarySaga) Handle(
                BoundaryCommand command,
                JasperFx.Events.IEventStream<Account> stream)
            {
                stream.AppendOne(new BoundaryEvent(command.Id));
                return (new(), new(), new(command.Id), new());
            }
        }

        public static class UnrelatedHandler
        {
            public static UnrelatedCascade Handle(
                UnrelatedCommand command,
                UnrelatedStream<Account> stream)
            {
                stream.AppendOne(new UnrelatedEvent(command.Id));
                return new(command.Id);
            }
        }

        public static class SagaOnlyHandler
        {
            public static TransferSaga Handle(SagaOnlyTrigger command) => new();
        }

        public static class GeneratedSagaHandler
        {
            public static GeneratedBaseSaga Handle(GeneratedSagaTrigger command) => new();
        }

        public static class SagaMixedHandler
        {
            public static (TransferSaga, SagaFollowUp) Handle(SagaMixedTrigger command) =>
                (new(), new(command.Id));
        }
        """;

    const string GeneratedApplicationSource =
        """
        namespace Transfers;

        public partial record GeneratedMemberCommand
        {
            public System.Guid AccountId => ActualId;
            public long Version => 1;
        }

        public partial class GeneratedBaseSaga : Wolverine.Saga;

        public record GeneratedCommand(System.Guid Id);
        public record GeneratedEvent(System.Guid Id);

        public static class GeneratedHandler
        {
            public static void Handle(
                GeneratedCommand command,
                [Wolverine.Persistence.EventSourcing.WriteModel(nameof(GeneratedCommand.Id))]
                JasperFx.Events.IEventStream<Account> first,
                [Wolverine.Persistence.EventSourcing.WriteModel(nameof(GeneratedCommand.Id))]
                JasperFx.Events.IEventStream<Order> second,
                System.Collections.Generic.IEnumerable<object> events)
            {
                first.AppendOne(new GeneratedEvent(command.Id));
                second.AppendMany(events);
            }
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
        var applicationTree = CSharpSyntaxTree.ParseText(ApplicationSource, path: "/workspace/Transfers/Handlers.cs");
        var generatedTree = CSharpSyntaxTree.ParseText(GeneratedApplicationSource, path: "/workspace/Transfers/GeneratedButNotNamed.cs");
        var compilation = CSharpCompilation.Create(
            "Transfers",
            [frameworkTree, applicationTree, generatedTree],
            _references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, nullableContextOptions: NullableContextOptions.Enable));
        Project = new DotNetProjectCompilation
        {
            Name = "Transfers",
            ProjectPath = "/workspace/Transfers/Transfers.csproj",
            SourceRoot = "/workspace",
            Compilation = compilation,
            AuthoredSyntaxTrees = new HashSet<SyntaxTree> { frameworkTree, applicationTree }
        };
    }
}
