// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Cratis.CritterStack.Screenplay.given;

/// <summary>
/// A small Marten and Wolverine kitchen: explicit session appends, an aggregate handler, HTTP endpoints that forward to
/// handlers, and an inline projection. The same source can be laid out in feature folders or one flat namespace.
/// </summary>
public class a_community_kitchen_application : Specification
{
    const string FrameworkSource =
        """
        namespace Marten
        {
            public interface IQuerySession
            {
                System.Threading.Tasks.Task<T?> LoadAsync<T>(System.Guid id);
            }
            public interface IDocumentSession : IQuerySession
            {
                Marten.Events.IEventStoreOperations Events { get; }
                System.Threading.Tasks.Task SaveChangesAsync();
            }
            public class StoreOptions
            {
                public Marten.Events.Projections.ProjectionOptions Projections { get; } = new();
            }
        }

        namespace Marten.Events
        {
            public class StreamAction;
            public interface IEventStoreOperations
            {
                StreamAction Append(System.Guid stream, params object[] events);
                StreamAction StartStream<TAggregate>(System.Guid id, params object[] events) where TAggregate : class;
                StreamAction StartStream<TAggregate>(params object[] events) where TAggregate : class;
            }
        }

        namespace Marten.Events.Projections
        {
            public class ProjectionOptions
            {
                public void Add<T>(JasperFx.Events.Projections.ProjectionLifecycle lifecycle) { }
            }
        }

        namespace Marten.Events.Aggregation
        {
            public abstract class SingleStreamProjection<TDoc, TId>;
        }

        namespace JasperFx.Events.Projections
        {
            public enum ProjectionLifecycle { Inline, Async }
        }

        namespace Wolverine
        {
            public class WolverineOptions;
            public interface ICommandBus
            {
                System.Threading.Tasks.Task InvokeAsync(object message);
            }
            public interface IMessageBus : ICommandBus
            {
                System.Threading.Tasks.ValueTask PublishAsync<T>(T message);
            }
            public class OutgoingMessages : System.Collections.Generic.List<object>;
        }

        namespace Wolverine.Http
        {
            public abstract class WolverineHttpMethodAttribute(string route) : System.Attribute;
            public class WolverinePostAttribute(string route) : WolverineHttpMethodAttribute(route);
            public class WolverineGetAttribute(string route) : WolverineHttpMethodAttribute(route);
        }

        namespace Wolverine.Persistence.EventSourcing
        {
            public class DeciderFunctionAttribute : System.Attribute;
        }

        namespace Wolverine.Marten
        {
            public class AggregateHandlerAttribute : Wolverine.Persistence.EventSourcing.DeciderFunctionAttribute;
        }
        """;

    const string EventsSource =
        """
        namespace Kitchen;

        public record BatchPlanned(string Recipe, int TargetPortions);
        public record PortionsPrepared(int Portions);
        public record BatchServed(int Portions);
        public record BatchClosed(string Reason);
        public record TargetAdjusted(int TargetPortions);
        public record TastingNoted(string Note);

        public class SoupBatch
        {
            public System.Guid Id { get; set; }
            public int TargetPortions { get; private set; }
            public int PreparedPortions { get; private set; }
            public bool Served { get; private set; }
            public void Apply(BatchPlanned @event) => TargetPortions = @event.TargetPortions;
            public void Apply(PortionsPrepared @event) => PreparedPortions += @event.Portions;
            public void Apply(BatchServed @event) => Served = true;
        }
        """;

    const string PlanningSource =
        """
        namespace Kitchen.Planning;

        public record PlanBatch(System.Guid Id, string Recipe, int TargetPortions);

        public static class PlanBatchHandler
        {
            public static async System.Threading.Tasks.Task Handle(PlanBatch command, Marten.IDocumentSession session)
            {
                session.Events.StartStream<Kitchen.SoupBatch>(command.Id, new Kitchen.BatchPlanned(command.Recipe, command.TargetPortions));
                await session.SaveChangesAsync();
            }
        }
        """;

    const string PlanningEndpointSource =
        """
        namespace Kitchen.Planning;

        public static class PlanBatchEndpoint
        {
            [Wolverine.Http.WolverinePost("/batches")]
            public static System.Threading.Tasks.Task Plan(PlanBatch command, Wolverine.IMessageBus bus) => bus.InvokeAsync(command);
        }
        """;

    const string PreparationSource =
        """
        namespace Kitchen.Preparation;

        public record PreparePortions(System.Guid BatchId, int Portions);
        public record AdjustTarget(string Slot, int TargetPortions);
        public record NoteTasting(System.Guid Id, string Note, bool Share);

        public static class PreparePortionsHandler
        {
            public static async System.Threading.Tasks.Task Handle(PreparePortions command, Marten.IDocumentSession session)
            {
                session.Events.Append(command.BatchId, new Kitchen.PortionsPrepared(command.Portions));
                await session.SaveChangesAsync();
            }
        }

        public static class AdjustTargetHandler
        {
            public static async System.Threading.Tasks.Task Handle(AdjustTarget command, Marten.IDocumentSession session)
            {
                session.Events.Append(BatchFor(command.Slot), new Kitchen.TargetAdjusted(command.TargetPortions));
                await session.SaveChangesAsync();
            }

            static System.Guid BatchFor(string slot) => System.Guid.Parse(slot);
        }

        public static class NoteTastingHandler
        {
            public static async System.Threading.Tasks.Task Handle(NoteTasting command, Marten.IDocumentSession session)
            {
                if (command.Share)
                {
                    session.Events.Append(command.Id, new Kitchen.TastingNoted(command.Note));
                }

                await session.SaveChangesAsync();
            }
        }
        """;

    const string ServingSource =
        """
        namespace Kitchen.Serving;

        public record ServeBatch(System.Guid Id);
        public record CloseBatch(System.Guid Id, string Reason);

        public static class ServeBatchHandler
        {
            [Wolverine.Marten.AggregateHandler]
            public static System.Collections.Generic.IEnumerable<object> Handle(ServeBatch command, Kitchen.SoupBatch batch)
            {
                if (!batch.Served && batch.PreparedPortions >= batch.TargetPortions)
                {
                    yield return new Kitchen.BatchServed(batch.PreparedPortions);
                }
            }
        }

        public static class CloseBatchHandler
        {
            [Wolverine.Marten.AggregateHandler]
            public static System.Collections.Generic.IEnumerable<object> Handle(CloseBatch command, Kitchen.SoupBatch batch)
            {
                yield return new Kitchen.BatchClosed(command.Reason);
            }
        }
        """;

    const string ServingEndpointSource =
        """
        namespace Kitchen.Serving;

        public static class ServeBatchEndpoint
        {
            [Wolverine.Http.WolverinePost("/batches/serve")]
            public static System.Threading.Tasks.Task Serve(ServeBatch command, Wolverine.IMessageBus bus) => bus.InvokeAsync(command);
        }
        """;

    const string CardsSource =
        """
        namespace Kitchen.Cards;

        public class BatchCard
        {
            public System.Guid Id { get; set; }
            public string Recipe { get; set; } = string.Empty;
            public int PreparedPortions { get; set; }
            public bool Served { get; set; }
        }

        public class BatchCardProjection : Marten.Events.Aggregation.SingleStreamProjection<BatchCard, System.Guid>
        {
            public static BatchCard Create(Kitchen.BatchPlanned @event) => new() { Recipe = @event.Recipe };
            public static void Apply(Kitchen.PortionsPrepared @event, BatchCard card) => card.PreparedPortions += @event.Portions;
            public static void Apply(Kitchen.BatchServed @event, BatchCard card) => card.Served = true;
        }

        public static class BatchCardEndpoint
        {
            [Wolverine.Http.WolverineGet("/batches/{id}")]
            public static System.Threading.Tasks.Task<BatchCard?> Get(System.Guid id, Marten.IQuerySession session) => session.LoadAsync<BatchCard>(id);
        }

        public static class KitchenConfiguration
        {
            public static void Configure(Marten.StoreOptions options) =>
                options.Projections.Add<BatchCardProjection>(JasperFx.Events.Projections.ProjectionLifecycle.Inline);
        }
        """;

    static readonly IReadOnlyList<MetadataReference> _references =
    [
        .. ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")!)
            .Split(Path.PathSeparator)
            .Select(_ => MetadataReference.CreateFromFile(_))
    ];

    /// <summary>
    /// Creates the kitchen with each feature in its own folder and namespace.
    /// </summary>
    /// <param name="reverse">Whether to supply the source files in reverse order.</param>
    /// <returns>The project compilation.</returns>
    protected static DotNetProjectCompilation CreateFeatureFolderProject(bool reverse = false)
    {
        (string Source, string ProjectPath)[] sources =
        [
            (EventsSource, "Kitchen/Events.cs"),
            (PlanningSource, "Kitchen/Planning/PlanBatch.cs"),
            (PlanningEndpointSource, "Kitchen/Planning/PlanBatchEndpoint.cs"),
            (PreparationSource, "Kitchen/Preparation/PreparePortions.cs"),
            (ServingSource, "Kitchen/Serving/ServeBatch.cs"),
            (ServingEndpointSource, "Kitchen/Serving/ServeBatchEndpoint.cs"),
            (CardsSource, "Kitchen/Cards/BatchCard.cs")
        ];
        return CreateProject(reverse ? [.. sources.AsEnumerable().Reverse()] : sources);
    }

    /// <summary>
    /// Creates the kitchen with every type in the one flat <c>Application.Kitchen</c> namespace and folder.
    /// </summary>
    /// <returns>The project compilation.</returns>
    protected static DotNetProjectCompilation CreateFlatProject() => CreateProject(
    [
        (Flatten(EventsSource), "Kitchen/Events.cs"),
        (Flatten(PlanningSource), "Kitchen/PlanBatch.cs"),
        (Flatten(PlanningEndpointSource), "Kitchen/PlanBatchEndpoint.cs"),
        (Flatten(PreparationSource), "Kitchen/PreparePortions.cs"),
        (Flatten(ServingSource), "Kitchen/ServeBatch.cs"),
        (Flatten(ServingEndpointSource), "Kitchen/ServeBatchEndpoint.cs"),
        (Flatten(CardsSource), "Kitchen/BatchCard.cs")
    ]);

    /// <summary>
    /// Finds an artifact by kind and name.
    /// </summary>
    /// <param name="result">The generation result.</param>
    /// <param name="kind">The artifact kind.</param>
    /// <param name="name">The artifact name.</param>
    /// <returns>The artifact.</returns>
    protected static ResolvedArtifact ArtifactNamed(GeneratedScreenplayDefinition result, ArtifactKind kind, string name) =>
        result.Graph.Artifacts.Single(_ => _.Key.Kind == kind && _.Variants[0].Definition.Name == name);

    /// <summary>
    /// Finds the production relationship from a command to an event.
    /// </summary>
    /// <param name="result">The generation result.</param>
    /// <param name="command">The command name.</param>
    /// <param name="event">The event name.</param>
    /// <returns>The relationship, or <see langword="null"/>.</returns>
    protected static ResolvedRelationship? Production(GeneratedScreenplayDefinition result, string command, string @event)
    {
        var source = ArtifactNamed(result, ArtifactKind.Command, command).Key.Subject;
        var target = ArtifactNamed(result, ArtifactKind.Event, @event).Key.Subject;
        return result.Graph.Relationships.SingleOrDefault(_ =>
            _.Key.Kind == RelationshipKind.Produces && _.Key.Source == source && _.Key.Target == target);
    }

    /// <summary>
    /// Describes where an artifact was placed as <c>Module/Features/Slice:Kind</c>.
    /// </summary>
    /// <param name="result">The generation result.</param>
    /// <param name="kind">The artifact kind.</param>
    /// <param name="name">The artifact name.</param>
    /// <returns>The placement description.</returns>
    protected static string PlacementOf(GeneratedScreenplayDefinition result, ArtifactKind kind, string name)
    {
        var key = ArtifactNamed(result, kind, name).Key;
        var placement = result.Graph.Placements.Single(_ => _.Artifact == key).EffectiveVariants.Single().Placement;
        return $"{string.Join('/', [placement.Module, .. placement.Features, placement.Slice])}:{placement.SliceKind}";
    }

    static string Flatten(string source) => source
        .Replace("namespace Kitchen.Planning;", "namespace Application.Kitchen;", StringComparison.Ordinal)
        .Replace("namespace Kitchen.Preparation;", "namespace Application.Kitchen;", StringComparison.Ordinal)
        .Replace("namespace Kitchen.Serving;", "namespace Application.Kitchen;", StringComparison.Ordinal)
        .Replace("namespace Kitchen.Cards;", "namespace Application.Kitchen;", StringComparison.Ordinal)
        .Replace("namespace Kitchen;", "namespace Application.Kitchen;", StringComparison.Ordinal)
        .Replace("Kitchen.SoupBatch", "SoupBatch", StringComparison.Ordinal)
        .Replace("Kitchen.Batch", "Batch", StringComparison.Ordinal)
        .Replace("Kitchen.Portions", "Portions", StringComparison.Ordinal)
        .Replace("Kitchen.Target", "Target", StringComparison.Ordinal)
        .Replace("Kitchen.Tasting", "Tasting", StringComparison.Ordinal);

    /// <summary>
    /// Creates a kitchen project from authored source files.
    /// </summary>
    /// <param name="sources">The source files and their project-relative paths.</param>
    /// <returns>The project compilation.</returns>
    protected static DotNetProjectCompilation CreateProject(IReadOnlyList<(string Source, string ProjectPath)> sources)
    {
        var frameworkTree = CSharpSyntaxTree.ParseText(FrameworkSource, path: "/workspace/Framework.cs");
        var authoredTrees = sources
            .Select(source => CSharpSyntaxTree.ParseText(source.Source, path: $"/workspace/Application/{source.ProjectPath}"))
            .ToArray();
        var compilation = CSharpCompilation.Create(
            "Application",
            [frameworkTree, .. authoredTrees],
            _references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, nullableContextOptions: NullableContextOptions.Enable));
        compilation.GetDiagnostics().Where(_ => _.Severity == DiagnosticSeverity.Error).ShouldBeEmpty();

        return new()
        {
            Name = "Application",
            Role = DotNetProjectRole.Application,
            ProjectPath = "/workspace/Application/Application.csproj",
            SourceRoot = "/workspace",
            SourceContext = DotNetSourcePaths.Create(
                "Application/Application",
                new DotNetSourcePathPolicy
                {
                    Version = 1,
                    DisplayRoot = DotNetSourceDisplayRoot.Workspace,
                    CasePolicy = DotNetSourcePathCasePolicy.Ordinal
                },
                [
                    .. authoredTrees.Select((tree, index) => new DotNetSourceDocument
                    {
                        SyntaxTree = tree,
                        ProjectRelativePath = sources[index].ProjectPath,
                        WorkspaceRelativePath = $"Application/{sources[index].ProjectPath}"
                    })
                ]),
            Compilation = compilation,
            AuthoredSyntaxTrees = authoredTrees.ToHashSet<SyntaxTree>()
        };
    }
}
