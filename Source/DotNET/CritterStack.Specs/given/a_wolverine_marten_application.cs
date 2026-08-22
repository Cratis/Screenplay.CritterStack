// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Cratis.CritterStack.Screenplay.given;

public class a_wolverine_marten_application : Specification
{
    const string FrameworkSource =
        """
        namespace Wolverine.Attributes
        {
            public class WolverineHandlerAttribute : System.Attribute;
            public class WolverineIgnoreAttribute : System.Attribute;
        }

        namespace Wolverine.Configuration
        {
            public interface IWolverineReturnType;
        }

        namespace Wolverine
        {
            public class WolverineHandlerAttribute : System.Attribute;
            public interface IWolverineHandler;
            public class WolverineOptions;
            public class DeliveryOptions
            {
                public System.TimeSpan? ScheduleDelay { get; set; }
                public System.DateTimeOffset? ScheduledTime { get; set; }
            }
            public interface ICommandBus
            {
                System.Threading.Tasks.Task<T> InvokeAsync<T>(object message);
            }
            public interface IMessageBus : ICommandBus
            {
                System.Threading.Tasks.ValueTask SendAsync<T>(T message, DeliveryOptions? options = null);
                System.Threading.Tasks.ValueTask PublishAsync<T>(T message, DeliveryOptions? options = null);
                System.Threading.Tasks.ValueTask BroadcastToTopicAsync(string topicName, object message, DeliveryOptions? options = null);
            }
            public static class MessageBusExtensions
            {
                public static System.Threading.Tasks.ValueTask ScheduleAsync<T>(this IMessageBus bus, T message, System.TimeSpan delay, DeliveryOptions? options = null) => default;
            }
            public interface IResponseAware : Wolverine.Configuration.IWolverineReturnType;
            public class OutgoingMessages : System.Collections.Generic.List<object>, Wolverine.Configuration.IWolverineReturnType;
            public interface ISideEffect : Wolverine.Configuration.IWolverineReturnType;
            public record DeliveryMessage<T>(T Message);
            public static class DeliveryExtensions
            {
                public static DeliveryMessage<T> DelayedFor<T>(this T message) => new(message);
            }
        }

        namespace Wolverine.Http
        {
            public interface IWolverineReturnType;
            public interface IResponseAware : IWolverineReturnType;
            public abstract class WolverineHttpMethodAttribute(string route) : System.Attribute;
            public class WolverinePostAttribute(string route) : WolverineHttpMethodAttribute(route);
            public class WolverineGetAttribute(string route) : WolverineHttpMethodAttribute(route);
            public class EmptyResponseAttribute : System.Attribute;
            public class CreationResponse<T>(string location, T value) : Wolverine.IResponseAware;
        }

        namespace Wolverine.Persistence.EventSourcing
        {
            public class DeciderFunctionAttribute : System.Attribute;
            public class WriteModelAttribute : System.Attribute;
            public class EventsToAppend : System.Collections.Generic.List<object>;
        }

        namespace Wolverine.Http.Marten
        {
            public class AggregateAttribute : Wolverine.Persistence.EventSourcing.WriteModelAttribute
            {
                public AggregateAttribute() { }
                public AggregateAttribute(string routeName) { }
            }
        }

        namespace Wolverine.Marten
        {
            public class AggregateHandlerAttribute : Wolverine.Persistence.EventSourcing.DeciderFunctionAttribute;
            public class Events : System.Collections.Generic.List<object>, Wolverine.Configuration.IWolverineReturnType;
            public class UpdatedAggregate : Wolverine.IResponseAware;
            public interface IStartStream : Wolverine.ISideEffect;
            public static class MartenOps
            {
                public static IStartStream StartStream<T>(object @event) => default!;
            }
        }

        namespace JasperFx.Events
        {
            public record Archived(string Reason);
            public interface IEventStream<T>
            {
                void AppendOne(object @event);
            }
        }

        namespace Marten
        {
            public interface IDocumentStore;
            public class StoreOptions
            {
                public Marten.Events.Projections.ProjectionOptions Projections { get; } = new();
            }
            public interface IDocumentSession
            {
                EventOperations Events { get; }
                void Delete<T>(System.Guid id);
                void Store<T>(T document);
            }
            public class EventOperations
            {
                public void Append(System.Guid id, object @event) { }
            }
        }

        namespace Marten.Events.Projections
        {
            public enum SnapshotLifecycle { Inline, Async }
            public class ProjectionOptions
            {
                public void Snapshot<T>(SnapshotLifecycle lifecycle) { }
            }
        }
        """;

    const string ApplicationSource =
        """
        using Wolverine;

        namespace IncidentService;

        public record IncidentLogged(System.Guid CustomerId, string Description);
        public record IncidentCategorised(System.Guid IncidentId, string Category);
        public record IncidentClosed(System.Guid ClosedBy);
        public record LogIncident(System.Guid CustomerId, string Description);
        public record CategoriseIncident(string Category, int Version);
        public record CloseIncident(System.Guid ClosedBy, int Version);
        public record ArchiveIncident(System.Guid IncidentId);
        public record AppendIncidentNote(System.Guid IncidentId, string Note);
        public record IncidentNoteAppended(string Note);
        public record NotifyIncidentNote(System.Guid IncidentId);
        public record ExplicitCommand(System.Guid IncidentId);
        public record ExplicitEvent(System.Guid IncidentId);
        public record IgnoredCommand(System.Guid IncidentId);
        public record IgnoredEvent(System.Guid IncidentId);
        public record MethodIgnoredCommand(System.Guid IncidentId);
        public record MethodIgnoredEvent(System.Guid IncidentId);
        public record GenericCommand(System.Guid IncidentId);
        public record GenericEvent(System.Guid IncidentId);
        public record AbstractCommand(System.Guid IncidentId);
        public record AbstractEvent(System.Guid IncidentId);
        public record CheckIncident(System.Guid IncidentId);
        public record CheckIncidentResponse(bool Exists);
        public record SendIncidentNotification(System.Guid IncidentId);
        public record PublishIncidentNotification(System.Guid IncidentId);
        public record RequestIncidentStatus(System.Guid IncidentId);
        public record IncidentStatusResponse(bool Exists);
        public record ScheduleIncidentReview(System.Guid IncidentId);
        public record ScheduledIncidentPublication(System.Guid IncidentId);
        public record TopicIncidentNotification(System.Guid IncidentId);
        public record UnrelatedBusMessage(System.Guid IncidentId);
        public record IncidentEscalated(System.Guid IncidentId);
        public record NotifyEscalation(System.Guid IncidentId);
        public record ReturnOnlyTrigger(System.Guid IncidentId);
        public record ReturnOnlyEventHappened(System.Guid IncidentId);
        public record TupleReturnTrigger(System.Guid IncidentId);
        public record FirstTupleCascade(System.Guid IncidentId);
        public record SecondTupleCascade(System.Guid IncidentId);
        public record OutgoingReturnTrigger(System.Guid IncidentId);
        public record ImmediateOutgoingCascade(System.Guid IncidentId);
        public record DelayedOutgoingCascade(System.Guid IncidentId);
        public record MixedAutomationTrigger(System.Guid IncidentId);
        public record MixedReturnedCascade(System.Guid IncidentId);
        public record MixedPublishedMessage(System.Guid IncidentId);
        public record ExcludedSlotsTrigger(System.Guid IncidentId);
        public record CascadeAfterExcludedSlots(System.Guid IncidentId);
        public record ResponseOnlyTrigger(System.Guid IncidentId);
        public record SideEffectOnlyTrigger(System.Guid IncidentId);
        public record CurrentWrapperOnlyTrigger(System.Guid IncidentId);
        public record LegacyWrapperOnlyTrigger(System.Guid IncidentId);
        public record PersistenceWrapperOnlyTrigger(System.Guid IncidentId);
        public record LegacyPersistenceWrapperOnlyTrigger(System.Guid IncidentId);
        public record StoreAndReturnTrigger(System.Guid IncidentId);
        public record StoreReturnCascade(System.Guid IncidentId);
        public record StoredDocument(System.Guid Id);
        public record CurrentExplicitTrigger(System.Guid IncidentId);
        public record CurrentExplicitCascade(System.Guid IncidentId);
        public record LegacyExplicitTrigger(System.Guid IncidentId);
        public record LegacyExplicitCascade(System.Guid IncidentId);
        public record IgnoredReturnTrigger(System.Guid IncidentId);
        public record IgnoredReturnCascade(System.Guid IncidentId);
        public record GenericReturnTrigger(System.Guid IncidentId);
        public record GenericReturnCascade(System.Guid IncidentId);
        public record AbstractReturnTrigger(System.Guid IncidentId);
        public record AbstractReturnCascade(System.Guid IncidentId);
        public record InternalReturnTrigger(System.Guid IncidentId);
        public record InternalReturnCascade(System.Guid IncidentId);
        public record InvalidReturnCascade(System.Guid IncidentId);
        public record MiddlewareTrigger(System.Guid IncidentId);
        public record MiddlewareCascade(System.Guid IncidentId);
        public record CompoundTrigger(System.Guid IncidentId);
        public record InterfaceTrigger(System.Guid IncidentId);
        public record InterfaceCascade(System.Guid IncidentId);
        public class UnrelatedBus
        {
            public System.Threading.Tasks.ValueTask SendAsync<T>(T message) => default;
        }
        public class AuditEffect : Wolverine.ISideEffect;
        public class CurrentReturnWrapper : Wolverine.Configuration.IWolverineReturnType;
        public class LegacyReturnWrapper : Wolverine.Http.IWolverineReturnType;

        public class Incident
        {
            public System.Guid Id { get; set; }
            public int Version { get; set; }
            public void Apply(IncidentLogged e) { }
            public void Apply(IncidentClosed e) { }
            public bool ShouldDelete(JasperFx.Events.Archived e) => true;
        }

        public static class Configuration
        {
            public static void Configure(Marten.StoreOptions options) =>
                options.Projections.Snapshot<Incident>(Marten.Events.Projections.SnapshotLifecycle.Inline);
        }

        public static class LogIncidentEndpoint
        {
            [Wolverine.Http.WolverinePost("/api/incidents")]
            public static (Wolverine.Http.CreationResponse<System.Guid>, Wolverine.Marten.IStartStream) Post(LogIncident command)
            {
                var logged = new IncidentLogged(command.CustomerId, command.Description);
                var stream = Wolverine.Marten.MartenOps.StartStream<Incident>(logged);
                return (new Wolverine.Http.CreationResponse<System.Guid>("/api/incidents", System.Guid.NewGuid()), stream);
            }
        }

        public static class CategoriseIncidentEndpoint
        {
            [Wolverine.Http.EmptyResponse]
            [Wolverine.Http.WolverinePost("/api/incidents/{incidentId}/category")]
            public static IncidentCategorised Post(
                CategoriseIncident command,
                [Wolverine.Http.Marten.Aggregate("incidentId")] Incident incident) =>
                new(incident.Id, command.Category);
        }

        public static class CloseIncidentEndpoint
        {
            [Wolverine.Http.WolverinePost("/api/incidents/{id}/close")]
            public static (Wolverine.Marten.UpdatedAggregate, Wolverine.Marten.Events, Wolverine.OutgoingMessages, AuditEffect) Handle(
                CloseIncident command,
                [Wolverine.Http.Marten.Aggregate] Incident incident) =>
                (new(), [new IncidentClosed(command.ClosedBy)], [new ArchiveIncident(incident.Id).DelayedFor()], new());
        }

        public static class ArchiveIncidentHandler
        {
            public static void Handle(ArchiveIncident command, Marten.IDocumentSession session)
            {
                session.Events.Append(command.IncidentId, new JasperFx.Events.Archived("Complete"));
                session.Delete<Incident>(command.IncidentId);
            }
        }

        public static class GetIncidentEndpoint
        {
            [Wolverine.Http.WolverineGet("/api/incidents/{id}")]
            public static System.Threading.Tasks.Task<Incident?> Get(System.Guid id) =>
                System.Threading.Tasks.Task.FromResult<Incident?>(null);
        }

        public static class CheckIncidentEndpoint
        {
            [Wolverine.Http.WolverinePost("/api/incidents/check")]
            public static CheckIncidentResponse Post(CheckIncident command) => new(true);
        }

        public static class AppendIncidentNoteHandler
        {
            public static NotifyIncidentNote Handle(
                AppendIncidentNote command,
                JasperFx.Events.IEventStream<Incident> stream,
                Wolverine.IMessageBus bus,
                UnrelatedBus unrelatedBus)
            {
                stream.AppendOne(new IncidentNoteAppended(command.Note));
                _ = bus.SendAsync(new SendIncidentNotification(command.IncidentId));
                _ = bus.PublishAsync(new PublishIncidentNotification(command.IncidentId));
                _ = bus.InvokeAsync<IncidentStatusResponse>(new RequestIncidentStatus(command.IncidentId));
                _ = bus.ScheduleAsync(new ScheduleIncidentReview(command.IncidentId), System.TimeSpan.FromMinutes(5));
                _ = bus.PublishAsync(
                    new ScheduledIncidentPublication(command.IncidentId),
                    new Wolverine.DeliveryOptions { ScheduleDelay = System.TimeSpan.FromMinutes(10) });
                _ = bus.BroadcastToTopicAsync("incidents", new TopicIncidentNotification(command.IncidentId));
                _ = unrelatedBus.SendAsync(new UnrelatedBusMessage(command.IncidentId));
                return new NotifyIncidentNote(command.IncidentId);
            }
        }

        public static class IncidentEscalationHandler
        {
            public static void Handle(IncidentEscalated message, Wolverine.IMessageBus bus) =>
                _ = bus.PublishAsync(new NotifyEscalation(message.IncidentId));
        }

        public static class ReturnOnlyHandler
        {
            public static ReturnOnlyEventHappened Handle(ReturnOnlyTrigger message) =>
                new(message.IncidentId);
        }

        public static class TupleReturnHandler
        {
            public static (FirstTupleCascade, SecondTupleCascade) Handle(TupleReturnTrigger message) =>
                (new(message.IncidentId), new(message.IncidentId));
        }

        public static class OutgoingReturnHandler
        {
            public static Wolverine.OutgoingMessages Handle(OutgoingReturnTrigger message) =>
                [
                    new ImmediateOutgoingCascade(message.IncidentId),
                    new DelayedOutgoingCascade(message.IncidentId).DelayedFor()
                ];
        }

        public static class MixedAutomationHandler
        {
            public static MixedReturnedCascade Handle(MixedAutomationTrigger message, Wolverine.IMessageBus bus)
            {
                _ = bus.PublishAsync(new MixedPublishedMessage(message.IncidentId));
                return new(message.IncidentId);
            }
        }

        public static class ExcludedSlotsHandler
        {
            public static (Wolverine.Http.CreationResponse<System.Guid>, AuditEffect, CascadeAfterExcludedSlots) Handle(ExcludedSlotsTrigger message) =>
                (new("/excluded", message.IncidentId), new(), new(message.IncidentId));
        }

        public static class ResponseOnlyHandler
        {
            public static Wolverine.Http.CreationResponse<System.Guid> Handle(ResponseOnlyTrigger message) =>
                new("/response", message.IncidentId);
        }

        public static class SideEffectOnlyHandler
        {
            public static AuditEffect Handle(SideEffectOnlyTrigger message) => new();
        }

        public static class CurrentWrapperOnlyHandler
        {
            public static CurrentReturnWrapper Handle(CurrentWrapperOnlyTrigger message) => new();
        }

        public static class LegacyWrapperOnlyHandler
        {
            public static LegacyReturnWrapper Handle(LegacyWrapperOnlyTrigger message) => new();
        }

        public static class PersistenceWrapperOnlyHandler
        {
            public static Wolverine.Persistence.EventSourcing.EventsToAppend Handle(PersistenceWrapperOnlyTrigger message) => [];
        }

        public static class LegacyPersistenceWrapperOnlyHandler
        {
            public static Wolverine.Marten.Events Handle(LegacyPersistenceWrapperOnlyTrigger message) => [];
        }

        public static class StoreAndReturnHandler
        {
            public static StoreReturnCascade Handle(StoreAndReturnTrigger message, Marten.IDocumentSession session)
            {
                session.Store(new StoredDocument(message.IncidentId));
                return new(message.IncidentId);
            }
        }

        public static class CurrentExplicitReturnActions
        {
            [Wolverine.Attributes.WolverineHandler]
            public static CurrentExplicitCascade Process(CurrentExplicitTrigger message) => new(message.IncidentId);
        }

        public static class LegacyExplicitActions
        {
            [Wolverine.WolverineHandler]
            public static LegacyExplicitCascade Process(LegacyExplicitTrigger message) => new(message.IncidentId);
        }

        [Wolverine.Attributes.WolverineIgnore]
        public static class IgnoredReturnHandler
        {
            public static IgnoredReturnCascade Handle(IgnoredReturnTrigger message) => new(message.IncidentId);
        }

        public class GenericReturnHandler<T>
        {
            public static GenericReturnCascade Handle(GenericReturnTrigger message) => new(message.IncidentId);
        }

        public abstract class AbstractReturnHandler
        {
            public AbstractReturnCascade Handle(AbstractReturnTrigger message) => new(message.IncidentId);
        }

        internal static class InternalReturnHandler
        {
            public static InternalReturnCascade Handle(InternalReturnTrigger message) => new(message.IncidentId);
        }

        public static class InvalidReturnUtility
        {
            [Wolverine.Attributes.WolverineHandler]
            public static InvalidReturnCascade Process(System.Guid id) => new(id);
        }

        public static class CompoundHandler
        {
            public static MiddlewareCascade Before(MiddlewareTrigger message) => new(message.IncidentId);
            public static void Handle(CompoundTrigger message) { }
        }

        public class InterfaceWorker : Wolverine.IWolverineHandler
        {
            public static InterfaceCascade Handle(InterfaceTrigger message) => new(message.IncidentId);
        }

        public static class ExplicitActions
        {
            [Wolverine.Attributes.WolverineHandler]
            public static void Process(ExplicitCommand command, Marten.IDocumentSession session) =>
                session.Events.Append(command.IncidentId, new ExplicitEvent(command.IncidentId));
        }

        [Wolverine.Attributes.WolverineIgnore]
        public static class IgnoredHandler
        {
            public static void Handle(IgnoredCommand command, Marten.IDocumentSession session) =>
                session.Events.Append(command.IncidentId, new IgnoredEvent(command.IncidentId));
        }

        public static class PartlyIgnoredHandler
        {
            [Wolverine.Attributes.WolverineIgnore]
            public static void Handle(MethodIgnoredCommand command, Marten.IDocumentSession session) =>
                session.Events.Append(command.IncidentId, new MethodIgnoredEvent(command.IncidentId));
        }

        public class GenericHandler<T>
        {
            public static void Handle(GenericCommand command, Marten.IDocumentSession session) =>
                session.Events.Append(command.IncidentId, new GenericEvent(command.IncidentId));
        }

        public abstract class AbstractHandler
        {
            public void Handle(AbstractCommand command, Marten.IDocumentSession session) =>
                session.Events.Append(command.IncidentId, new AbstractEvent(command.IncidentId));
        }
        """;

    const string GeneratedApplicationSource =
        """
        // <auto-generated/>
        namespace IncidentService;

        public record GeneratedTrigger(System.Guid IncidentId);
        public record GeneratedCascade(System.Guid IncidentId);

        public static class GeneratedHandler
        {
            public static GeneratedCascade Handle(GeneratedTrigger message) => new(message.IncidentId);
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
        var compilation = CSharpCompilation.Create(
            "IncidentService",
            [
                CSharpSyntaxTree.ParseText(FrameworkSource, path: "/workspace/Framework.cs"),
                CSharpSyntaxTree.ParseText(ApplicationSource, path: "/workspace/IncidentService/Incidents.cs"),
                CSharpSyntaxTree.ParseText(GeneratedApplicationSource, path: "/workspace/IncidentService/Generated.g.cs")
            ],
            _references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, nullableContextOptions: NullableContextOptions.Enable));
        Project = new DotNetProjectCompilation
        {
            Name = "IncidentService",
            ProjectPath = "/workspace/IncidentService/IncidentService.csproj",
            SourceRoot = "/workspace",
            Compilation = compilation
        };
    }
}
