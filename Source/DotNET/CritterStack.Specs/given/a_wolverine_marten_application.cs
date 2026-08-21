// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Cratis.CritterStack.Screenplay.given;

public class a_wolverine_marten_application : Specification
{
    const string FrameworkSource =
        """
        namespace Wolverine
        {
            public class WolverineOptions;
            public class OutgoingMessages : System.Collections.Generic.List<object>;
            public interface ISideEffect;
        }

        namespace Wolverine.Http
        {
            public abstract class WolverineHttpMethodAttribute(string route) : System.Attribute;
            public class WolverinePostAttribute(string route) : WolverineHttpMethodAttribute(route);
            public class WolverineGetAttribute(string route) : WolverineHttpMethodAttribute(route);
            public class EmptyResponseAttribute : System.Attribute;
            public interface IWolverineReturnType;
            public interface IResponseAware;
            public class CreationResponse<T>(string location, T value);
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
            public class Events : System.Collections.Generic.List<object>, Wolverine.Http.IWolverineReturnType;
            public class UpdatedAggregate : Wolverine.Http.IResponseAware;
            public interface IStartStream : Wolverine.ISideEffect;
            public static class MartenOps
            {
                public static IStartStream StartStream<T>(object @event) => default!;
            }
        }

        namespace JasperFx.Events
        {
            public record Archived(string Reason);
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
        namespace IncidentService;

        public record IncidentLogged(System.Guid CustomerId, string Description);
        public record IncidentCategorised(System.Guid IncidentId, string Category);
        public record IncidentClosed(System.Guid ClosedBy);
        public record LogIncident(System.Guid CustomerId, string Description);
        public record CategoriseIncident(string Category, int Version);
        public record CloseIncident(System.Guid ClosedBy, int Version);
        public record ArchiveIncident(System.Guid IncidentId);

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
            public static (Wolverine.Marten.UpdatedAggregate, Wolverine.Marten.Events, Wolverine.OutgoingMessages) Handle(
                CloseIncident command,
                [Wolverine.Http.Marten.Aggregate] Incident incident) =>
                (new(), [new IncidentClosed(command.ClosedBy)], [new ArchiveIncident(incident.Id)]);
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
