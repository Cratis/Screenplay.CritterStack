// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Cratis.CritterStack.Screenplay.given;

/// <summary>
/// Marten projection metadata, projection registrations, subscriptions, daemon modes and registered value types in
/// alternative syntactic forms across two authored files (declared in reverse path order), a generated file and a
/// non-authored file: constructors that are direct, expression-bodied, conditional, nested, chained and split over
/// partial declarations; constant, computed, named and positional registration arguments; lambda shapes; method
/// calls that are valid first and unresolved later; repeated registrations of the same subscription.
/// </summary>
public class a_marten_configuration_variants_application : Specification
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

        namespace Marten.Events.Daemon
        {
            public class EventRange;
            public interface ISubscriptionController;

            public class DaemonSettings
            {
                public JasperFx.Events.Daemon.DaemonMode AsyncMode { get; set; }
            }
        }

        namespace JasperFx.Events.Daemon
        {
            public class DaemonSettings
            {
                public DaemonMode AsyncMode { get; set; }
            }
        }

        namespace JasperFx.Events.Projections
        {
            public enum ProjectionLifecycle { Inline, Async, Live }

            public class AsyncOptions
            {
                public void SubscribeFromPresent(string? database = null) { }
                public void SubscribeFromSequence(long sequence, string? database = null) { }
                public void SubscribeFromTime(System.DateTimeOffset time, string? database = null) { }
                public void SubscribeAsInlineToAsync() { }
            }

            public interface IEventFilterable
            {
                bool IncludeArchivedEvents { get; set; }
                void IncludeType<T>();
                void IncludeType(System.Type type);
                void FilterIncomingEventsOnStreamType(System.Type streamType);
            }

            public class EventFilterable : IEventFilterable
            {
                public bool IncludeArchivedEvents { get; set; }
                public void IncludeType<T>() { }
                public void IncludeType(System.Type type) { }
                public void FilterIncomingEventsOnStreamType(System.Type streamType) { }
            }

            public abstract class ProjectionBase : EventFilterable
            {
                public string Name { get; set; } = string.Empty;
                public uint Version { get; set; } = 1;
                public string Label { get; set; } = string.Empty;
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
                void Store<T>(params T[] documents);
            }

            public class StoreOptions
            {
                public Marten.Events.Projections.ProjectionOptions Projections { get; } = new();
                public Marten.Events.IEventStoreOptions Events { get; } = null!;
                public void RegisterValueType<T>() { }
                public void RegisterValueType(System.Type type) { }
            }

            public class MartenConfigurationExpression
            {
                public MartenConfigurationExpression AddAsyncDaemon(JasperFx.Events.Daemon.DaemonMode mode) => this;
                public MartenConfigurationExpression AddAsyncDaemon() => this;
                public MartenConfigurationExpression AddProjectionWithServices<T>(
                    JasperFx.Events.Projections.ProjectionLifecycle lifecycle,
                    Microsoft.Extensions.DependencyInjection.ServiceLifetime lifetime,
                    System.Action<JasperFx.Events.Projections.ProjectionBase>? configure = null) => this;
                public MartenConfigurationExpression AddSubscriptionWithServices<T>(
                    Microsoft.Extensions.DependencyInjection.ServiceLifetime lifetime,
                    System.Action<JasperFx.Events.Subscriptions.ISubscriptionOptions>? configure = null) => this;
            }
        }

        namespace Marten.Events
        {
            public interface IEventStoreOptions
            {
                bool EnableSideEffectsOnInlineProjections { get; set; }
                void Subscribe(
                    Marten.Subscriptions.ISubscription subscription,
                    System.Action<JasperFx.Events.Subscriptions.ISubscriptionOptions>? configure = null);
            }
        }

        namespace Marten.Events.Projections
        {
            public interface IProjection;

            public class ProjectionOptions
            {
                public void Add(
                    IProjection projection,
                    JasperFx.Events.Projections.ProjectionLifecycle lifecycle,
                    string? projectionName = null) { }
                public void Add<T>(
                    JasperFx.Events.Projections.ProjectionLifecycle lifecycle,
                    string? projectionName = null) where T : IProjection, new() { }
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
                public string SubscriptionName { get; set; } = string.Empty;
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

    const string LaterFileSource =
        """
        namespace Orders;

        public record OrderOpened(System.Guid Id);
        public record StreamMarker;
        public record Money(decimal Amount);
        public record Other(int Value);

        public class Summary
        {
            public System.Guid Id { get; set; }
        }

        public sealed class DirectProjection : Marten.Events.Aggregation.SingleStreamProjection<Summary, System.Guid>
        {
            const string Prefix = "p";

            public DirectProjection()
            {
                Name = "direct";
                Name = Prefix + "-x";
                Version = 2;
                Version = 4u;
                Version = (uint)3;
                Label = "ignored";
            }

            public void Apply(OrderOpened opened, Summary summary) { }
        }

        public sealed class ValidThenInvalidProjection : Marten.Events.Aggregation.SingleStreamProjection<Summary, System.Guid>
        {
            public ValidThenInvalidProjection(bool flag = false)
            {
                Name = "valid";
                if (flag)
                {
                    Version = 9;
                }

                Name = Build();
                Version = (uint)System.Environment.ProcessorCount;
            }

            public void Apply(OrderOpened opened, Summary summary) { }

            static string Build() => "computed";
        }

        public sealed class TwoConstructorProjection : Marten.Events.Aggregation.SingleStreamProjection<Summary, System.Guid>
        {
            public TwoConstructorProjection()
            {
                Name = "one";
            }

            public TwoConstructorProjection(int version)
            {
                Name = "two";
                Version = (uint)version;
            }

            public void Apply(OrderOpened opened, Summary summary) { }
        }

        public sealed class ExpressionBodiedProjection : Marten.Events.Aggregation.SingleStreamProjection<Summary, System.Guid>
        {
            public ExpressionBodiedProjection() => Name = "expression";

            public void Apply(OrderOpened opened, Summary summary) { }
        }

        public sealed class ChainedProjection : Marten.Events.Aggregation.SingleStreamProjection<Summary, System.Guid>
        {
            public ChainedProjection()
            {
                Version = 5;
            }

            public ChainedProjection(bool flag)
                : this()
            {
                Name = flag ? "a" : "b";
            }

            public void Apply(OrderOpened opened, Summary summary) { }
        }

        public sealed class NestedProjection : Marten.Events.Aggregation.SingleStreamProjection<Summary, System.Guid>
        {
            public NestedProjection()
            {
                System.Action set = () => { Name = "in-lambda"; };
                set();
                void Local() => Version = 6;
                Local();
                { Label = "block"; Name = "in-block"; }
                for (var i = 0; i < 1; i++)
                {
                    Version = 7;
                }
            }

            public void Apply(OrderOpened opened, Summary summary) { }
        }

        public sealed partial class SplitProjection : Marten.Events.Aggregation.SingleStreamProjection<Summary, System.Guid>
        {
            public SplitProjection()
            {
                Name = "later-part";
            }

            public void Apply(OrderOpened opened, Summary summary) { }
        }

        public sealed class GenericProjection : Marten.Events.Aggregation.SingleStreamProjection<Summary, System.Guid>
        {
            public GenericProjection()
            {
                Version = 11;
            }

            public void Apply(OrderOpened opened, Summary summary) { }
        }

        public sealed class RawProjection : Marten.Events.Projections.IProjection
        {
            public void ApplyAsync(Marten.IDocumentOperations operations) => operations.Store(new Money(1));
            public void Apply(Marten.IDocumentOperations operations) => operations.Store(new Money(2));
        }

        public sealed class SingleConstructorSubscription : Marten.Subscriptions.SubscriptionBase
        {
            public SingleConstructorSubscription(bool flag = false)
            {
                Name = "single";
                SubscriptionName = "single-alias";
                Version = 4;
                IncludeArchivedEvents = true;
                IncludeArchivedEvents = flag;
                IncludeType<OrderOpened>();
                IncludeType(typeof(OrderOpened));
                IncludeType(RuntimeType());
                FilterIncomingEventsOnStreamType(typeof(StreamMarker));
                FilterIncomingEventsOnStreamType(RuntimeType());
                Options.SubscribeFromPresent();
                Options.SubscribeFromPresent("blue");
                Options.SubscribeFromPresent(Database());
                Options.SubscribeFromSequence(7);
                Options.SubscribeFromSequence(8, "green");
                Options.SubscribeFromSequence(Sequence());
                Options.SubscribeFromSequence(9, Database());
                Options.SubscribeFromTime(System.DateTimeOffset.UtcNow);
                Options.SubscribeAsInlineToAsync();
                Name = Database();
                Version = (uint)System.Environment.ProcessorCount;
            }

            public override System.Threading.Tasks.Task ProcessEventsAsync(
                Marten.Events.Daemon.EventRange page,
                Marten.Events.Daemon.ISubscriptionController controller,
                Marten.IDocumentOperations operations,
                System.Threading.CancellationToken cancellationToken)
            {
                operations.Store(new Money(3));
                return System.Threading.Tasks.Task.CompletedTask;
            }

            static System.Type RuntimeType() => typeof(Other);

            static string Database() => "computed";

            static long Sequence() => 3;
        }

        public sealed class TwoConstructorSubscription : Marten.Subscriptions.SubscriptionBase
        {
            public TwoConstructorSubscription()
            {
                Name = "two-constructors";
                IncludeType<OrderOpened>();
            }

            public TwoConstructorSubscription(int version)
            {
                Version = (uint)version;
                Options.SubscribeFromSequence(version);
            }

            public override System.Threading.Tasks.Task ProcessEventsAsync(
                Marten.Events.Daemon.EventRange page,
                Marten.Events.Daemon.ISubscriptionController controller,
                Marten.IDocumentOperations operations,
                System.Threading.CancellationToken cancellationToken) => System.Threading.Tasks.Task.CompletedTask;
        }

        public sealed class ExpressionBodiedSubscription : Marten.Subscriptions.SubscriptionBase
        {
            public ExpressionBodiedSubscription() => Name = "expression";

            public override System.Threading.Tasks.Task ProcessEventsAsync(
                Marten.Events.Daemon.EventRange page,
                Marten.Events.Daemon.ISubscriptionController controller,
                Marten.IDocumentOperations operations,
                System.Threading.CancellationToken cancellationToken) => System.Threading.Tasks.Task.CompletedTask;
        }

        public sealed class NestedSubscription : Marten.Subscriptions.SubscriptionBase
        {
            public NestedSubscription(bool flag)
            {
                if (flag)
                {
                    IncludeType<OrderOpened>();
                    Name = "conditional";
                }

                System.Action configure = () =>
                {
                    Version = 3;
                    Options.SubscribeFromPresent();
                };
                configure();
                void Local() => IncludeArchivedEvents = true;
                Local();
                while (flag)
                {
                    FilterIncomingEventsOnStreamType(typeof(StreamMarker));
                    flag = false;
                }
            }

            public override System.Threading.Tasks.Task ProcessEventsAsync(
                Marten.Events.Daemon.EventRange page,
                Marten.Events.Daemon.ISubscriptionController controller,
                Marten.IDocumentOperations operations,
                System.Threading.CancellationToken cancellationToken) => System.Threading.Tasks.Task.CompletedTask;
        }

        public sealed partial class SplitSubscription : Marten.Subscriptions.SubscriptionBase
        {
            public SplitSubscription()
            {
                Name = "later-part";
            }

            public override System.Threading.Tasks.Task ProcessEventsAsync(
                Marten.Events.Daemon.EventRange page,
                Marten.Events.Daemon.ISubscriptionController controller,
                Marten.IDocumentOperations operations,
                System.Threading.CancellationToken cancellationToken) => System.Threading.Tasks.Task.CompletedTask;
        }

        public sealed class PlainRawSubscription : Marten.Subscriptions.ISubscription
        {
            public System.Threading.Tasks.Task ProcessEventsAsync(
                Marten.Events.Daemon.EventRange page,
                Marten.Events.Daemon.ISubscriptionController controller,
                Marten.IDocumentOperations operations,
                System.Threading.CancellationToken cancellationToken) => System.Threading.Tasks.Task.CompletedTask;
        }

        public static class LaterConfiguration
        {
            public static bool Flag => true;

            public static void Configure(
                Marten.StoreOptions options,
                Marten.MartenConfigurationExpression services,
                JasperFx.Events.Daemon.DaemonSettings jasperSettings,
                Marten.Events.Daemon.DaemonSettings martenSettings,
                JasperFx.Events.Projections.ProjectionLifecycle runtimeLifecycle,
                Marten.Subscriptions.ISubscription interfaceSubscription)
            {
                const JasperFx.Events.Projections.ProjectionLifecycle constantLifecycle = JasperFx.Events.Projections.ProjectionLifecycle.Async;
                const JasperFx.Events.Daemon.DaemonMode constantMode = JasperFx.Events.Daemon.DaemonMode.Solo;
                options.Projections.Add(new DirectProjection(), constantLifecycle, "named");
                options.Projections.Add(new DirectProjection(), lifecycle: constantLifecycle, projectionName: "named-argument");
                options.Projections.Add(new ValidThenInvalidProjection(), JasperFx.Events.Projections.ProjectionLifecycle.Live, projectionName: Name());
                options.Projections.Add(new TwoConstructorProjection(), runtimeLifecycle);
                options.Projections.Add(projection: new ExpressionBodiedProjection(), lifecycle: Lifecycle());
                options.Projections.Add(new ChainedProjection(), JasperFx.Events.Projections.ProjectionLifecycle.Inline);
                options.Projections.Add(new NestedProjection(), (JasperFx.Events.Projections.ProjectionLifecycle)1);
                options.Projections.Add(new SplitProjection(), JasperFx.Events.Projections.ProjectionLifecycle.Inline);
                options.Projections.Add<GenericProjection>(JasperFx.Events.Projections.ProjectionLifecycle.Async);
                options.Projections.Add<GenericProjection>(runtimeLifecycle, "generic-name");
                options.Projections.Add(new RawProjection(), JasperFx.Events.Projections.ProjectionLifecycle.Inline, "raw");
                options.Projections.Add(new RawProjection(), Lifecycle());

                services.AddProjectionWithServices<DirectProjection>(
                    constantLifecycle,
                    Microsoft.Extensions.DependencyInjection.ServiceLifetime.Scoped,
                    projection => projection.Name = "expression-lambda");
                services.AddProjectionWithServices<ValidThenInvalidProjection>(
                    constantLifecycle,
                    Microsoft.Extensions.DependencyInjection.ServiceLifetime.Scoped,
                    (projection) =>
                    {
                        projection.Name = "block-lambda";
                        if (Flag)
                        {
                            projection.Version = 3;
                        }

                        projection.Label = "ignored";
                    });
                services.AddProjectionWithServices<TwoConstructorProjection>(
                    constantLifecycle,
                    Microsoft.Extensions.DependencyInjection.ServiceLifetime.Singleton,
                    configure: projection =>
                    {
                        void Local() => projection.Version = 8;
                        Local();
                    });
                services.AddProjectionWithServices<ExpressionBodiedProjection>(
                    constantLifecycle,
                    Microsoft.Extensions.DependencyInjection.ServiceLifetime.Transient,
                    projection =>
                    {
                        projection.Name = Name();
                        projection.Version = 7;
                    });
                services.AddProjectionWithServices<ChainedProjection>(
                    constantLifecycle,
                    Microsoft.Extensions.DependencyInjection.ServiceLifetime.Scoped,
                    projection => Use(projection.Name = "wrapped"));
                services.AddProjectionWithServices<NestedProjection>(
                    Lifecycle(),
                    Microsoft.Extensions.DependencyInjection.ServiceLifetime.Scoped);
                services.AddProjectionWithServices<SplitProjection>(
                    runtimeLifecycle,
                    Microsoft.Extensions.DependencyInjection.ServiceLifetime.Scoped,
                    Configure);

                services.AddAsyncDaemon(constantMode);
                services.AddAsyncDaemon(JasperFx.Events.Daemon.DaemonMode.HotCold);
                services.AddAsyncDaemon((JasperFx.Events.Daemon.DaemonMode)3);
                services.AddAsyncDaemon(default);
                services.AddAsyncDaemon(Mode());
                services.AddAsyncDaemon();
                jasperSettings.AsyncMode = JasperFx.Events.Daemon.DaemonMode.Solo;
                jasperSettings.AsyncMode = Mode();
                jasperSettings.AsyncMode = (JasperFx.Events.Daemon.DaemonMode)1;
                martenSettings.AsyncMode = constantMode;
                martenSettings.AsyncMode = Flag ? constantMode : JasperFx.Events.Daemon.DaemonMode.HotCold;

                options.RegisterValueType<Money>();
                options.RegisterValueType(typeof(Money));
                options.RegisterValueType(typeof(Other));
                options.RegisterValueType(ValueType());
                RegisterGeneric<Money>(options);
                options.Events.EnableSideEffectsOnInlineProjections = true;

                options.Events.Subscribe(new SingleConstructorSubscription(), subscription => subscription.Name = "override");
                options.Events.Subscribe(new SingleConstructorSubscription());
                options.Events.Subscribe(
                    configure: subscription =>
                    {
                        subscription.IncludeArchivedEvents = true;
                        subscription.Options.SubscribeFromSequence(1);
                        subscription.Options.SubscribeFromSequence(Sequence());
                        if (Flag)
                        {
                            subscription.Version = 2;
                        }
                    },
                    subscription: new TwoConstructorSubscription());
                options.Projections.Subscribe(new ExpressionBodiedSubscription(), (subscription) => Use(subscription.Name = "wrapped"));
                options.Projections.Subscribe(new NestedSubscription(true), subscription =>
                {
                    subscription.Name = Name();
                    subscription.IncludeType<OrderOpened>();
                    subscription.IncludeType(typeof(Other));
                    subscription.Options.SubscribeFromTime(System.DateTimeOffset.UtcNow);
                    subscription.FilterIncomingEventsOnStreamType(typeof(StreamMarker));
                });
                options.Events.Subscribe(new SplitSubscription());
                options.Events.Subscribe(new PlainRawSubscription());
                options.Events.Subscribe(interfaceSubscription);
                options.Events.Subscribe(new NonAuthoredSubscription());
                services.AddSubscriptionWithServices<SingleConstructorSubscription>(
                    Microsoft.Extensions.DependencyInjection.ServiceLifetime.Singleton,
                    subscription => subscription.Options.SubscribeFromPresent("lambda"));
                services.AddSubscriptionWithServices<ExpressionBodiedSubscription>(Microsoft.Extensions.DependencyInjection.ServiceLifetime.Scoped);
                services.AddSubscriptionWithServices<NonAuthoredSubscription>(
                    Microsoft.Extensions.DependencyInjection.ServiceLifetime.Scoped,
                    subscription => subscription.Name = "non-authored");
            }

            static void Configure(JasperFx.Events.Projections.ProjectionBase projection) => projection.Name = "method-group";

            static void Use(string value) { }

            static string Name() => "computed";

            static long Sequence() => 3;

            static System.Type ValueType() => typeof(Money);

            static JasperFx.Events.Projections.ProjectionLifecycle Lifecycle() => JasperFx.Events.Projections.ProjectionLifecycle.Live;

            static JasperFx.Events.Daemon.DaemonMode Mode() => JasperFx.Events.Daemon.DaemonMode.Solo;

            static void RegisterGeneric<T>(Marten.StoreOptions options) => options.RegisterValueType<T>();
        }
        """;

    const string EarlierFileSource =
        """
        namespace Orders;

        public sealed partial class SplitProjection
        {
            public SplitProjection(int version)
            {
                Version = (uint)version;
            }
        }

        public sealed partial class SplitSubscription
        {
            public SplitSubscription(int version)
            {
                Version = (uint)version;
            }
        }

        public static class EarlierConfiguration
        {
            public static void Configure(
                Marten.StoreOptions options,
                Marten.MartenConfigurationExpression services,
                JasperFx.Events.Daemon.DaemonSettings settings)
            {
                options.Projections.Add(new DirectProjection(), JasperFx.Events.Projections.ProjectionLifecycle.Async, "earlier");
                options.Events.Subscribe(new SingleConstructorSubscription(), subscription => subscription.Version = 12);
                services.AddAsyncDaemon(JasperFx.Events.Daemon.DaemonMode.ExternallyManaged);
                settings.AsyncMode = JasperFx.Events.Daemon.DaemonMode.Disabled;
                options.RegisterValueType<Other>();
            }
        }
        """;

    const string GeneratedFileSource =
        """
        // <auto-generated/>
        namespace Orders;

        public sealed class GeneratedSubscription : Marten.Subscriptions.SubscriptionBase
        {
            public GeneratedSubscription()
            {
                Name = "generated";
            }

            public override System.Threading.Tasks.Task ProcessEventsAsync(
                Marten.Events.Daemon.EventRange page,
                Marten.Events.Daemon.ISubscriptionController controller,
                Marten.IDocumentOperations operations,
                System.Threading.CancellationToken cancellationToken) => System.Threading.Tasks.Task.CompletedTask;
        }

        public static class GeneratedConfiguration
        {
            public static void Configure(Marten.StoreOptions options, Marten.MartenConfigurationExpression services)
            {
                options.Events.Subscribe(new GeneratedSubscription());
                services.AddAsyncDaemon(JasperFx.Events.Daemon.DaemonMode.Solo);
            }
        }
        """;

    const string NonAuthoredFileSource =
        """
        namespace Orders;

        public sealed class NonAuthoredSubscription : Marten.Subscriptions.SubscriptionBase
        {
            public NonAuthoredSubscription()
            {
                Name = "non-authored";
                Version = 2;
            }

            public override System.Threading.Tasks.Task ProcessEventsAsync(
                Marten.Events.Daemon.EventRange page,
                Marten.Events.Daemon.ISubscriptionController controller,
                Marten.IDocumentOperations operations,
                System.Threading.CancellationToken cancellationToken) => System.Threading.Tasks.Task.CompletedTask;
        }

        public static class NonAuthoredConfiguration
        {
            public static void Configure(Marten.MartenConfigurationExpression services)
            {
                services.AddAsyncDaemon(JasperFx.Events.Daemon.DaemonMode.HotCold);
            }
        }
        """;

    static readonly IReadOnlyList<MetadataReference> _references =
    [
        .. ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES"))
            .Split(Path.PathSeparator)
            .Select(_ => MetadataReference.CreateFromFile(_))
    ];

    protected AdapterContribution VariantContribution = null!;

    void Establish()
    {
        var frameworkTree = CSharpSyntaxTree.ParseText(FrameworkSource, path: "/workspace/Framework.cs");
        var laterTree = CSharpSyntaxTree.ParseText(LaterFileSource, path: "/workspace/Orders/Later.cs");
        var earlierTree = CSharpSyntaxTree.ParseText(EarlierFileSource, path: "/workspace/Orders/Earlier.cs");
        var generatedTree = CSharpSyntaxTree.ParseText(GeneratedFileSource, path: "/workspace/Orders/Generated.g.cs");
        var nonAuthoredTree = CSharpSyntaxTree.ParseText(NonAuthoredFileSource, path: "/workspace/Orders/NonAuthored.cs");
        var compilation = CSharpCompilation.Create(
            "Orders",
            [frameworkTree, laterTree, earlierTree, generatedTree, nonAuthoredTree],
            _references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, nullableContextOptions: NullableContextOptions.Enable));
        compilation.GetDiagnostics().Where(_ => _.Severity == DiagnosticSeverity.Error).ShouldBeEmpty();
        var project = new DotNetProjectCompilation
        {
            Name = "Orders",
            ProjectPath = "/workspace/Orders/Orders.csproj",
            SourceRoot = "/workspace",
            Compilation = compilation,
            AuthoredSyntaxTrees = new HashSet<SyntaxTree> { frameworkTree, laterTree, earlierTree, generatedTree }
        };
        VariantContribution = new CritterStackScreenplayAdapter().Analyze(
            new DotNetAnalysisContext([project]),
            new DotNetAdapterOptions());
    }
}
