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
        public class UnrelatedBus
        {
            public System.Threading.Tasks.ValueTask SendAsync<T>(T message) => default;
        }
        public class AuditEffect : Wolverine.ISideEffect;

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
                CSharpSyntaxTree.ParseText(ApplicationSource, path: "/workspace/IncidentService/Incidents.cs")
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
