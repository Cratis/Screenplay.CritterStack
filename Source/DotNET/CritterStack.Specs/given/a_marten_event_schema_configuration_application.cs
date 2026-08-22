// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Cratis.CritterStack.Screenplay.given;

public class a_marten_event_schema_configuration_application : Specification
{
    const string FrameworkSource =
        """
        namespace JasperFx.Events
        {
            public enum EventNamingStyle { ClassicTypeName, SmarterTypeName, FullTypeName, Unsupported }
        }

        namespace Marten.Schema
        {
            public sealed class MartenEventAttribute : System.Attribute
            {
                public string? Alias { get; set; }
            }
        }

        namespace Marten.Services.Json.Transformations
        {
            public delegate object JsonTransformation(object json);
            public interface IEventUpcaster;
            public abstract class EventUpcaster<TEvent> : IEventUpcaster;
            public abstract class EventUpcaster<TOldEvent, TEvent> : IEventUpcaster;
            public abstract class AsyncOnlyEventUpcaster<TOldEvent, TEvent> : IEventUpcaster;
        }

        namespace Marten.Services.Json.Transformations.SystemTextJson
        {
            public abstract class EventUpcaster<TEvent> : Marten.Services.Json.Transformations.IEventUpcaster;
            public abstract class AsyncOnlyEventUpcaster<TEvent> : Marten.Services.Json.Transformations.IEventUpcaster;
        }

        namespace Marten.Services.Json.Transformations.JsonNet
        {
            public abstract class EventUpcaster<TEvent> : Marten.Services.Json.Transformations.IEventUpcaster;
            public abstract class AsyncOnlyEventUpcaster<TEvent> : Marten.Services.Json.Transformations.IEventUpcaster;
        }

        namespace Marten.Events
        {
            public interface IEventStoreOptions
            {
                JasperFx.Events.EventNamingStyle EventNamingStyle { get; set; }
                void AddEventType<TEvent>();
                void AddEventType(System.Type eventType);
                void MapEventType<TEvent>(string eventTypeName) where TEvent : class;
                void MapEventType(System.Type eventType, string eventTypeName);
                IEventStoreOptions Upcast<TEvent>(string eventTypeName, Marten.Services.Json.Transformations.JsonTransformation jsonTransformation) where TEvent : class;
                IEventStoreOptions Upcast(System.Type eventType, string eventTypeName, Marten.Services.Json.Transformations.JsonTransformation jsonTransformation);
                IEventStoreOptions Upcast<TOldEvent, TEvent>(string eventTypeName, System.Func<TOldEvent, TEvent> upcast) where TOldEvent : class where TEvent : class;
                IEventStoreOptions Upcast<TOldEvent, TEvent>(System.Func<TOldEvent, TEvent> upcast) where TOldEvent : class where TEvent : class;
                IEventStoreOptions Upcast<TOldEvent, TEvent>(string eventTypeName, System.Func<TOldEvent, System.Threading.CancellationToken, System.Threading.Tasks.Task<TEvent>> upcastAsync) where TOldEvent : class where TEvent : class;
                IEventStoreOptions Upcast<TOldEvent, TEvent>(System.Func<TOldEvent, System.Threading.CancellationToken, System.Threading.Tasks.Task<TEvent>> upcastAsync) where TOldEvent : class where TEvent : class;
                IEventStoreOptions Upcast(params Marten.Services.Json.Transformations.IEventUpcaster[] upcasters);
                IEventStoreOptions Upcast<TUpcaster>() where TUpcaster : Marten.Services.Json.Transformations.IEventUpcaster, new();
            }

        }

        public static class EventStoreOptionsExtensions
        {
            public static Marten.Events.IEventStoreOptions MapEventTypeWithNameSuffix<TEvent>(this Marten.Events.IEventStoreOptions options, string eventTypeName, string suffix) where TEvent : class => options;
            public static Marten.Events.IEventStoreOptions MapEventTypeWithNameSuffix<TEvent>(this Marten.Events.IEventStoreOptions options, string suffix) where TEvent : class => options;
            public static Marten.Events.IEventStoreOptions MapEventTypeWithSchemaVersion<TEvent>(this Marten.Events.IEventStoreOptions options, uint schemaVersion) where TEvent : class => options;
            public static Marten.Events.IEventStoreOptions MapEventTypeWithSchemaVersion<TEvent>(this Marten.Events.IEventStoreOptions options, string eventTypeName, uint schemaVersion) where TEvent : class => options;
            public static Marten.Events.IEventStoreOptions Upcast<TEvent>(this Marten.Events.IEventStoreOptions options, Marten.Services.Json.Transformations.JsonTransformation jsonTransformation) where TEvent : class => options;
            public static Marten.Events.IEventStoreOptions Upcast(this Marten.Events.IEventStoreOptions options, System.Type eventType, Marten.Services.Json.Transformations.JsonTransformation jsonTransformation) => options;
            public static Marten.Events.IEventStoreOptions Upcast<TEvent>(this Marten.Events.IEventStoreOptions options, uint schemaVersion, Marten.Services.Json.Transformations.JsonTransformation jsonTransformation) where TEvent : class => options;
            public static Marten.Events.IEventStoreOptions Upcast(this Marten.Events.IEventStoreOptions options, System.Type eventType, uint schemaVersion, Marten.Services.Json.Transformations.JsonTransformation jsonTransformation) => options;
            public static Marten.Events.IEventStoreOptions Upcast<TOldEvent, TEvent>(this Marten.Events.IEventStoreOptions options, uint schemaVersion, System.Func<TOldEvent, TEvent> upcast) where TOldEvent : class where TEvent : class => options;
            public static Marten.Events.IEventStoreOptions Upcast<TOldEvent, TEvent>(this Marten.Events.IEventStoreOptions options, uint schemaVersion, System.Func<TOldEvent, System.Threading.CancellationToken, System.Threading.Tasks.Task<TEvent>> upcastAsync) where TOldEvent : class where TEvent : class => options;
        }

        namespace Marten.Events
        {
            public sealed class EventMapping
            {
                public string EventTypeName { get; set; } = string.Empty;
            }

            public sealed class EventGraph
            {
                public EventMapping EventMappingFor<TEvent>() => new();
            }
        }

        namespace Marten.Events.Projections
        {
            public enum SnapshotLifecycle { Inline, Async }
            public sealed class ProjectionOptions
            {
                public void Snapshot<T>(SnapshotLifecycle lifecycle) { }
            }
        }

        namespace Marten
        {
            public interface IDocumentStore;
            public sealed class StoreOptions
            {
                public Marten.Events.IEventStoreOptions Events { get; } = null!;
                public Marten.Events.Projections.ProjectionOptions Projections { get; } = new();
            }
        }
        """;

    const string ExactApplicationSource =
        """
        using Marten.Events;
        using Marten.Services.Json.Transformations;

        namespace Orders;

        public sealed record OrderRegistered;
        public sealed record AliasOnly;
        public sealed record DirectAliasOnly;
        public sealed record ControlledAliasOnly;
        public sealed record ExplicitSuffixOnly;
        public sealed record ConventionSuffixOnly;
        public sealed record ExplicitVersionOnly;
        public sealed record ConventionVersionOnly;
        [Marten.Schema.MartenEvent(Alias = "attribute-order")]
        public sealed record AttributeAliasOnly;
        [Marten.Schema.MartenEvent]
        public sealed record AttributeWithoutAlias;
        public sealed record LegacyOne;
        public sealed record CurrentOne;
        public sealed record LegacyTwo;
        public sealed record CurrentTwo;
        public sealed record LegacyThree;
        public sealed record CurrentThree;
        public sealed record LegacyFour;
        public sealed record CurrentFour;
        public sealed record RawTargetOne;
        public sealed record RawTargetTwo;
        public sealed record RawTargetThree;
        public sealed record RawTargetFour;
        public sealed record RawTargetFive;
        public sealed record RawTargetSix;
        public sealed record RootRawTarget;
        public sealed record StaticAliasOnly;
        public sealed record StaticRawTarget;
        public sealed record StaticLegacy;
        public sealed record StaticCurrent;
        public sealed record ClassLegacy;
        public sealed record ClassCurrent;
        public sealed record ClrAsyncLegacy;
        public sealed record ClrAsyncCurrent;
        public sealed record StjTarget;
        public sealed record StjAsyncTarget;
        public sealed record JsonNetTarget;
        public sealed record JsonNetAsyncTarget;
        public sealed record ConditionalAliasOnly;
        public sealed record DeferredLegacy;
        public sealed record DeferredCurrent;
        public sealed record AddedOnly;
        public sealed record LegacyExcluded;

        public sealed class Order
        {
            public System.Guid Id { get; set; }
            public void Apply(OrderRegistered registered) { }
        }

        public abstract class ClrSyncUpcasterBase<TOldEvent, TEvent> : EventUpcaster<TOldEvent, TEvent>;
        public sealed class ClrSyncUpcaster : ClrSyncUpcasterBase<ClassLegacy, ClassCurrent>;
        public sealed class RootRawUpcaster : EventUpcaster<RootRawTarget>;
        public sealed class ClrAsyncUpcaster : AsyncOnlyEventUpcaster<ClrAsyncLegacy, ClrAsyncCurrent>;
        public sealed class StjSyncUpcaster : Marten.Services.Json.Transformations.SystemTextJson.EventUpcaster<StjTarget>;
        public sealed class StjAsyncUpcaster : Marten.Services.Json.Transformations.SystemTextJson.AsyncOnlyEventUpcaster<StjAsyncTarget>;
        public sealed class JsonNetSyncUpcaster : Marten.Services.Json.Transformations.JsonNet.EventUpcaster<JsonNetTarget>;
        public sealed class JsonNetAsyncUpcaster : Marten.Services.Json.Transformations.JsonNet.AsyncOnlyEventUpcaster<JsonNetAsyncTarget>;
        public sealed class ArbitraryUpcaster : IEventUpcaster;

        public static class Configuration
        {
            public static void Configure(Marten.StoreOptions options)
            {
                var events = options.Events;
                const string directAlias = "order-registered";
                const uint schemaVersion = 7;
                const JasperFx.Events.EventNamingStyle smarter = JasperFx.Events.EventNamingStyle.SmarterTypeName;
                events.MapEventType<OrderRegistered>(directAlias);
                events.MapEventType<AliasOnly>("alias-only");
                events.MapEventType(typeof(DirectAliasOnly), "direct-alias-only");
                events.MapEventType<ControlledAliasOnly>("line\nnext\u001b");
                events.MapEventTypeWithNameSuffix<ExplicitSuffixOnly>("explicit-base", "legacy");
                events.MapEventTypeWithNameSuffix<ConventionSuffixOnly>("legacy");
                events.MapEventTypeWithSchemaVersion<ExplicitVersionOnly>("version-base", schemaVersion);
                events.MapEventTypeWithSchemaVersion<ConventionVersionOnly>(3);
                events.EventNamingStyle = JasperFx.Events.EventNamingStyle.ClassicTypeName;
                events.EventNamingStyle = smarter;
                events.EventNamingStyle = JasperFx.Events.EventNamingStyle.FullTypeName;
                events.Upcast<LegacyOne, CurrentOne>("legacy-one", old => new CurrentOne());
                events.Upcast<LegacyTwo, CurrentTwo>(old => new CurrentTwo());
                events.Upcast<LegacyThree, CurrentThree>("legacy-three", (old, cancellationToken) => System.Threading.Tasks.Task.FromResult(new CurrentThree()));
                events.Upcast<LegacyFour, CurrentFour>((old, cancellationToken) => System.Threading.Tasks.Task.FromResult(new CurrentFour()));
                events.Upcast<LegacyOne, CurrentOne>(2, old => new CurrentOne());
                events.Upcast<LegacyThree, CurrentThree>(4, (old, cancellationToken) => System.Threading.Tasks.Task.FromResult(new CurrentThree()));
                events.Upcast<RawTargetOne>("raw-one", json => json);
                events.Upcast(typeof(RawTargetTwo), "raw-two", json => json);
                events.Upcast<RawTargetThree>(5, json => json);
                events.Upcast(typeof(RawTargetFour), 6, json => json);
                events.Upcast<RawTargetFive>(json => json);
                events.Upcast(typeof(RawTargetSix), json => json);
                events.Upcast<RootRawUpcaster>();
                global::EventStoreOptionsExtensions.MapEventTypeWithNameSuffix<StaticAliasOnly>(events, "static-base", "legacy");
                global::EventStoreOptionsExtensions.Upcast<StaticRawTarget>(events, json => json);
                global::EventStoreOptionsExtensions.Upcast<StaticLegacy, StaticCurrent>(events, 8, old => new StaticCurrent());
                events.Upcast<ClrSyncUpcaster>();
                events.Upcast<ClrAsyncUpcaster>();
                events.Upcast(new StjSyncUpcaster(), new StjAsyncUpcaster(), new JsonNetSyncUpcaster(), new JsonNetAsyncUpcaster());
                if (System.DateTimeOffset.UtcNow.Year > 2000)
                {
                    events.MapEventType<ConditionalAliasOnly>("conditional-alias");
                }
                System.Action deferred = () => events.Upcast<DeferredLegacy, DeferredCurrent>("deferred-old", old => new DeferredCurrent());
                events.AddEventType<AddedOnly>();
                events.AddEventType(typeof(AddedOnly));
                var legacyGraph = new Marten.Events.EventGraph();
                legacyGraph.EventMappingFor<LegacyExcluded>().EventTypeName = "legacy-excluded";
                options.Projections.Snapshot<Order>(Marten.Events.Projections.SnapshotLifecycle.Inline);
                _ = deferred;
            }
        }
        """;

    const string UnresolvedApplicationSource =
        """
        using Marten.Events;
        using Marten.Services.Json.Transformations;

        namespace Orders.Unresolved;

        public sealed record ComputedAliasOnly;
        public sealed record ComputedSuffixOnly;
        public sealed record ComputedVersionOnly;
        public sealed record ComputedStyleOnly;
        public sealed record ComputedLegacy;
        public sealed record ComputedCurrent;
        public sealed record ComputedRawTarget;

        public static class Configuration
        {
            public static void Configure(Marten.Events.IEventStoreOptions events, IEventUpcaster unresolvedUpcaster)
            {
                events.MapEventType<ComputedAliasOnly>(BuildAlias());
                events.MapEventTypeWithNameSuffix<ComputedSuffixOnly>("base", BuildAlias());
                events.MapEventTypeWithSchemaVersion<ComputedVersionOnly>(BuildVersion());
                var style = JasperFx.Events.EventNamingStyle.FullTypeName;
                events.EventNamingStyle = style;
                events.Upcast<ComputedLegacy, ComputedCurrent>(BuildAlias(), old => new ComputedCurrent());
                System.Type target = typeof(ComputedRawTarget);
                events.Upcast(target, "raw-computed-target", json => json);
                events.Upcast(new Orders.ClrSyncUpcaster(), unresolvedUpcaster);
                events.Upcast<Orders.Generated.GeneratedClrUpcaster>();
                RegisterGenericUpcaster<Orders.ClrSyncUpcaster>(events);
            }

            static void RegisterGenericUpcaster<TUpcaster>(Marten.Events.IEventStoreOptions events)
                where TUpcaster : IEventUpcaster, new() => events.Upcast<TUpcaster>();

            static string BuildAlias() => "must-not-be-guessed";
            static uint BuildVersion() => 99;
        }
        """;

    const string UnrelatedApplicationSource =
        """
        namespace Unrelated;

        public interface IEventStoreOptions
        {
            void MapEventType<T>(string alias);
            void Upcast<TOld, TNew>(string alias, System.Func<TOld, TNew> upcast);
        }

        public static class EventStoreOptionsExtensions
        {
            public static IEventStoreOptions MapEventTypeWithSchemaVersion<T>(this IEventStoreOptions options, uint version) => options;
            public static IEventStoreOptions Upcast<TOld, TNew>(this IEventStoreOptions options, uint version, System.Func<TOld, TNew> upcast) => options;
        }

        public sealed record Old;
        public sealed record New;

        public static class Configuration
        {
            public static void Configure(IEventStoreOptions options)
            {
                options.MapEventType<New>("unrelated-alias");
                options.MapEventTypeWithSchemaVersion<New>(123);
                options.Upcast<Old, New>("unrelated-upcast", old => new New());
                options.Upcast<Old, New>(123, old => new New());
            }
        }
        """;

    const string GeneratedApplicationSource =
        """
        // <auto-generated/>
        using Marten.Events;

        namespace Orders.Generated;

        [Marten.Schema.MartenEvent(Alias = "generated-attribute")]
        public sealed record GeneratedAlias;
        public sealed record GeneratedOld;
        public sealed record GeneratedNew;
        public sealed class GeneratedClrUpcaster : Marten.Services.Json.Transformations.EventUpcaster<GeneratedOld, GeneratedNew>;

        public static class GeneratedConfiguration
        {
            public static void Configure(Marten.Events.IEventStoreOptions events)
            {
                events.MapEventType<GeneratedAlias>("generated-alias");
                events.MapEventTypeWithSchemaVersion<GeneratedAlias>(42);
                events.Upcast<GeneratedOld, GeneratedNew>("generated-upcast", old => new GeneratedNew());
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
        var frameworkTree = CSharpSyntaxTree.ParseText(FrameworkSource, path: "/workspace/Framework.cs");
        var exactTree = CSharpSyntaxTree.ParseText(ExactApplicationSource, path: "/workspace/Orders/EventSchemaConfiguration.cs");
        var unresolvedTree = CSharpSyntaxTree.ParseText(UnresolvedApplicationSource, path: "/workspace/Orders/UnresolvedEventSchemaConfiguration.cs");
        var unrelatedTree = CSharpSyntaxTree.ParseText(UnrelatedApplicationSource, path: "/workspace/Unrelated/EventSchemaConfiguration.cs");
        var generatedTree = CSharpSyntaxTree.ParseText(GeneratedApplicationSource, path: "/workspace/Orders/GeneratedEventSchema.g.cs");
        var compilation = CSharpCompilation.Create(
            "Orders",
            [frameworkTree, exactTree, unresolvedTree, unrelatedTree, generatedTree],
            _references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, nullableContextOptions: NullableContextOptions.Enable));
        compilation.GetDiagnostics().Where(_ => _.Severity == DiagnosticSeverity.Error).ShouldBeEmpty();
        var project = new DotNetProjectCompilation
        {
            Name = "Orders",
            ProjectPath = "/workspace/Orders/Orders.csproj",
            SourceRoot = "/workspace",
            Compilation = compilation,
            AuthoredSyntaxTrees = new HashSet<SyntaxTree> { frameworkTree, exactTree, unresolvedTree, unrelatedTree, generatedTree }
        };
        Contribution = new CritterStackScreenplayAdapter().Analyze(
            new DotNetAnalysisContext([project]),
            new DotNetAdapterOptions());
        Graph = new GenerationResolver().Resolve([Contribution]);
    }
}
