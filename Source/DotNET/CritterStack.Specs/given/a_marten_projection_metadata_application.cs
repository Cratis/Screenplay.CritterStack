// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Cratis.CritterStack.Screenplay.given;

public class a_marten_projection_metadata_application : Specification
{
    const string FrameworkSource =
        """
        namespace Microsoft.Extensions.DependencyInjection
        {
            public enum ServiceLifetime { Singleton, Scoped, Transient }
        }

        namespace JasperFx.Events.Daemon
        {
            public enum DaemonMode { Disabled, Solo, HotCold, ExternallyManaged }
        }

        namespace JasperFx.Events.Projections
        {
            public enum ProjectionLifecycle { Inline, Async, Live }

            public class AsyncOptions
            {
                public void SubscribeFromPresent(string? database = null) { }
                public void SubscribeFromSequence(long sequence, string? database = null) { }
                public void SubscribeFromTime(System.DateTimeOffset time, string? database = null) { }
            }

            public interface IEventFilterable
            {
                bool IncludeArchivedEvents { get; set; }
                void IncludeType<T>();
                void FilterIncomingEventsOnStreamType(System.Type streamType);
            }

            public class EventFilterable : IEventFilterable
            {
                public bool IncludeArchivedEvents { get; set; }
                public void IncludeType<T>() { }
                public void FilterIncomingEventsOnStreamType(System.Type streamType) { }
            }

            public abstract class ProjectionBase : EventFilterable
            {
                public string Name { get; set; } = string.Empty;
                public uint Version { get; set; } = 1;
            }
        }

        namespace JasperFx.Events.Subscriptions
        {
            public interface ISubscriptionOptions : JasperFx.Events.Projections.IEventFilterable
            {
                string Name { get; set; }
                uint Version { get; set; }
                JasperFx.Events.Projections.AsyncOptions Options { get; }
            }
        }

        namespace Marten
        {
            public interface IDocumentStore;
            public interface IDocumentOperations
            {
                T? Load<T>(System.Guid id);
                void Store<T>(params T[] documents);
            }

            public class StoreOptions
            {
                public Marten.Events.Projections.ProjectionOptions Projections { get; } = new();
                public Marten.Events.EventStoreOptions Events { get; } = new();
            }

            public class MartenConfigurationExpression
            {
                public MartenConfigurationExpression AddAsyncDaemon(JasperFx.Events.Daemon.DaemonMode mode) => this;
                public MartenConfigurationExpression AddProjectionWithServices<T>(
                    JasperFx.Events.Projections.ProjectionLifecycle lifecycle,
                    Microsoft.Extensions.DependencyInjection.ServiceLifetime lifetime,
                    System.Action<JasperFx.Events.Projections.ProjectionBase>? configure = null) => this;
                public MartenConfigurationExpression AddSubscriptionWithServices<T>(
                    Microsoft.Extensions.DependencyInjection.ServiceLifetime lifetime,
                    System.Action<JasperFx.Events.Subscriptions.ISubscriptionOptions>? configure = null) => this;
            }
        }

        namespace Marten.Events.Daemon
        {
            public class EventRange;
            public interface ISubscriptionController;
        }

        namespace Marten.Events
        {
            public class EventStoreOptions
            {
                public void Subscribe(
                    Marten.Subscriptions.ISubscription subscription,
                    System.Action<JasperFx.Events.Subscriptions.ISubscriptionOptions>? configure = null) { }
            }
        }

        namespace Marten.Events.Projections
        {
            public interface IProjection;

            public class ProjectionOptions
            {
                public JasperFx.Events.Daemon.DaemonMode AsyncMode { get; set; }
                public void Add(
                    IProjection projection,
                    JasperFx.Events.Projections.ProjectionLifecycle lifecycle,
                    string? projectionName = null) { }
                public void Subscribe(
                    Marten.Subscriptions.ISubscription subscription,
                    System.Action<JasperFx.Events.Subscriptions.ISubscriptionOptions>? configure = null) { }
            }
        }

        namespace Marten.Events.Aggregation
        {
            public abstract class SingleStreamProjection<T, TId> : JasperFx.Events.Projections.ProjectionBase, Marten.Events.Projections.IProjection;
        }

        namespace Marten.Subscriptions
        {
            public interface ISubscription
            {
                System.Threading.Tasks.Task ProcessEventsAsync(
                    Marten.Events.Daemon.EventRange page,
                    Marten.Events.Daemon.ISubscriptionController controller,
                    Marten.IDocumentOperations operations,
                    System.Threading.CancellationToken cancellationToken);
            }

            public abstract class SubscriptionBase : JasperFx.Events.Projections.EventFilterable, ISubscription, JasperFx.Events.Subscriptions.ISubscriptionOptions
            {
                public string Name { get; set; } = string.Empty;
                public uint Version { get; set; } = 1;
                public JasperFx.Events.Projections.AsyncOptions Options { get; } = new();
                public abstract System.Threading.Tasks.Task ProcessEventsAsync(
                    Marten.Events.Daemon.EventRange page,
                    Marten.Events.Daemon.ISubscriptionController controller,
                    Marten.IDocumentOperations operations,
                    System.Threading.CancellationToken cancellationToken);
            }
        }
        """;

    const string ApplicationSource =
        """
        namespace Orders;

        public record OrderOpened(System.Guid Id);
        public record StreamMarker;
        public record HiddenDocument(System.Guid Id);

        public class OrderSummary
        {
            public System.Guid Id { get; set; }
        }

        public sealed class OrderSummaryProjection : Marten.Events.Aggregation.SingleStreamProjection<OrderSummary, System.Guid>
        {
            public OrderSummaryProjection()
            {
                Name = "orders-summary";
                Version = 3;
            }

            public void Apply(OrderOpened opened, OrderSummary summary) { }
        }

        public sealed class RawProjection : Marten.Events.Projections.IProjection
        {
            public void ApplyAsync(Marten.IDocumentOperations operations) =>
                operations.Store(new HiddenDocument(System.Guid.Empty));
        }

        public sealed class ServiceProjection : JasperFx.Events.Projections.ProjectionBase, Marten.Events.Projections.IProjection
        {
            public void Apply(Marten.IDocumentOperations operations) =>
                operations.Store(new HiddenDocument(System.Guid.Empty));
        }

        public sealed class ComputedProjection : Marten.Events.Aggregation.SingleStreamProjection<OrderSummary, System.Guid>
        {
            public ComputedProjection()
            {
                Name = BuildName();
                if (System.DateTimeOffset.UtcNow.Year > 2000)
                {
                    Version = 9;
                }
            }

            public void Apply(OrderOpened opened, OrderSummary summary) { }
            static string BuildName() => "computed";
        }

        public sealed class InvoiceSubscription : Marten.Subscriptions.SubscriptionBase
        {
            public InvoiceSubscription()
            {
                Name = "invoices";
                Version = 4;
                IncludeArchivedEvents = true;
                IncludeType<OrderOpened>();
                FilterIncomingEventsOnStreamType(typeof(StreamMarker));
                Options.SubscribeFromSequence(42, "blue");
            }

            public override System.Threading.Tasks.Task ProcessEventsAsync(
                Marten.Events.Daemon.EventRange page,
                Marten.Events.Daemon.ISubscriptionController controller,
                Marten.IDocumentOperations operations,
                System.Threading.CancellationToken cancellationToken)
            {
                operations.Store(new HiddenDocument(System.Guid.Empty));
                return System.Threading.Tasks.Task.CompletedTask;
            }
        }

        public sealed class RawSubscription : Marten.Subscriptions.ISubscription
        {
            public System.Threading.Tasks.Task ProcessEventsAsync(
                Marten.Events.Daemon.EventRange page,
                Marten.Events.Daemon.ISubscriptionController controller,
                Marten.IDocumentOperations operations,
                System.Threading.CancellationToken cancellationToken)
            {
                _ = operations.Load<HiddenDocument>(System.Guid.Empty);
                operations.Store(new HiddenDocument(System.Guid.Empty));
                return System.Threading.Tasks.Task.CompletedTask;
            }
        }

        public sealed class ConditionalSubscription : Marten.Subscriptions.SubscriptionBase
        {
            public ConditionalSubscription()
            {
                if (System.DateTimeOffset.UtcNow.Year > 2000)
                {
                    IncludeType<OrderOpened>();
                }
                IncludeArchivedEvents = IsArchived();
                Options.SubscribeFromTime(System.DateTimeOffset.UtcNow);
            }

            public override System.Threading.Tasks.Task ProcessEventsAsync(
                Marten.Events.Daemon.EventRange page,
                Marten.Events.Daemon.ISubscriptionController controller,
                Marten.IDocumentOperations operations,
                System.Threading.CancellationToken cancellationToken) => System.Threading.Tasks.Task.CompletedTask;

            static bool IsArchived() => true;
        }

        public static class Configuration
        {
            public static void Configure(
                Marten.StoreOptions options,
                Marten.MartenConfigurationExpression services)
            {
                const JasperFx.Events.Projections.ProjectionLifecycle summaryLifecycle = JasperFx.Events.Projections.ProjectionLifecycle.Async;
                const JasperFx.Events.Daemon.DaemonMode hostedDaemonMode = JasperFx.Events.Daemon.DaemonMode.Solo;
                options.Projections.Add(
                    new OrderSummaryProjection(),
                    summaryLifecycle);
                options.Projections.Add(
                    new RawProjection(),
                    JasperFx.Events.Projections.ProjectionLifecycle.Inline,
                    "raw-projection");
                var lifecycle = JasperFx.Events.Projections.ProjectionLifecycle.Live;
                options.Projections.Add(new ComputedProjection(), lifecycle);
                options.Events.Subscribe(new InvoiceSubscription(), subscription =>
                {
                    subscription.IncludeArchivedEvents = false;
                    subscription.Options.SubscribeFromPresent();
                });
                services.AddProjectionWithServices<ServiceProjection>(
                    JasperFx.Events.Projections.ProjectionLifecycle.Async,
                    Microsoft.Extensions.DependencyInjection.ServiceLifetime.Scoped,
                    projection =>
                    {
                        projection.Name = "service-projection";
                        projection.Version = 2;
                    });
                services.AddSubscriptionWithServices<RawSubscription>(
                    Microsoft.Extensions.DependencyInjection.ServiceLifetime.Singleton,
                    subscription =>
                    {
                        subscription.Name = "raw-subscription";
                        subscription.Version = 5;
                        subscription.IncludeArchivedEvents = true;
                        subscription.IncludeType<OrderOpened>();
                    });
                options.Projections.Subscribe(new ConditionalSubscription());
                options.Projections.Add(
                    new GeneratedRawProjection(),
                    JasperFx.Events.Projections.ProjectionLifecycle.Inline,
                    "generated-raw");
                options.Events.Subscribe(new GeneratedSubscription());
                services.AddAsyncDaemon(hostedDaemonMode);
                var daemonMode = JasperFx.Events.Daemon.DaemonMode.HotCold;
                services.AddAsyncDaemon(daemonMode);
            }
        }
        """;

    const string GeneratedApplicationSource =
        """
        // <auto-generated/>
        namespace Orders;

        public sealed class GeneratedRawProjection : Marten.Events.Projections.IProjection
        {
            public void ApplyAsync(Marten.IDocumentOperations operations) =>
                operations.Store(new HiddenDocument(System.Guid.Empty));
        }

        public sealed class GeneratedSubscription : Marten.Subscriptions.SubscriptionBase
        {
            public GeneratedSubscription()
            {
                Name = "generated-subscription";
                Version = 12;
                IncludeArchivedEvents = true;
                IncludeType<OrderOpened>();
                Options.SubscribeFromSequence(84, "generated");
            }

            public override System.Threading.Tasks.Task ProcessEventsAsync(
                Marten.Events.Daemon.EventRange page,
                Marten.Events.Daemon.ISubscriptionController controller,
                Marten.IDocumentOperations operations,
                System.Threading.CancellationToken cancellationToken)
            {
                operations.Store(new HiddenDocument(System.Guid.Empty));
                return System.Threading.Tasks.Task.CompletedTask;
            }
        }
        """;

    static readonly IReadOnlyList<MetadataReference> _references =
    [
        .. ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES"))
            .Split(Path.PathSeparator)
            .Select(_ => MetadataReference.CreateFromFile(_))
    ];

    protected AdapterContribution Contribution = null!;
    protected ResolvedApplicationGraph Graph = null!;

    void Establish()
    {
        var frameworkSyntaxTree = CSharpSyntaxTree.ParseText(FrameworkSource, path: "/workspace/Framework.cs");
        var applicationSyntaxTree = CSharpSyntaxTree.ParseText(ApplicationSource, path: "/workspace/Orders/Configuration.cs");
        var generatedSyntaxTree = CSharpSyntaxTree.ParseText(GeneratedApplicationSource, path: "/workspace/Orders/Generated.g.cs");
        var compilation = CSharpCompilation.Create(
            "Orders",
            [
                frameworkSyntaxTree,
                applicationSyntaxTree,
                generatedSyntaxTree
            ],
            _references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, nullableContextOptions: NullableContextOptions.Enable));
        compilation.GetDiagnostics().Where(_ => _.Severity == DiagnosticSeverity.Error).ShouldBeEmpty();
        var project = new DotNetProjectCompilation
        {
            Name = "Orders",
            ProjectPath = "/workspace/Orders/Orders.csproj",
            SourceRoot = "/workspace",
            Compilation = compilation,
            AuthoredSyntaxTrees = new HashSet<SyntaxTree> { frameworkSyntaxTree, applicationSyntaxTree }
        };
        Contribution = new CritterStackScreenplayAdapter().Analyze(
            new DotNetAnalysisContext([project]),
            new DotNetAdapterOptions());
        Graph = new GenerationResolver().Resolve([Contribution]);
    }
}
