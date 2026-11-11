// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Cratis.CritterStack.Screenplay.given;

/// <summary>
/// Marten projection registrations and event-projection operations in alternative syntactic forms: constructor
/// and factory conventions, wrapped and bare event parameters, plain, Task and ValueTask returns, declaration and
/// type-pattern switch labels, repeated operations across files, and valid operations followed by unresolvable ones.
/// </summary>
public class a_marten_projection_variants_application : Specification
{
    const string FrameworkSource =
        """
        namespace JasperFx.Events
        {
            public interface IEvent
            {
                object Data { get; }
            }
            public interface IEvent<T> : IEvent;
        }

        namespace JasperFx.Events.Projections
        {
            public enum ProjectionLifecycle { Inline = 0, Async = 1, Live = 2 }
            public enum SnapshotLifecycle { Inline = 0, Async = 1 }
            public interface IProjection;
            public class AsyncOptions
            {
                public void DeleteViewTypeOnTeardown<T>() { }
            }
        }

        namespace Marten.Events.Daemon
        {
            public class AsyncOptions
            {
                public void DeleteViewTypeOnTeardown<T>() { }
            }
        }

        namespace Marten
        {
            public interface IDocumentStore;
            public interface IDocumentOperations
            {
                void Store<T>(params T[] documents);
                void Insert<T>(params T[] documents);
                void Update<T>(params T[] documents);
                void Delete<T>(System.Guid id);
                void DeleteWhere<T>(System.Linq.Expressions.Expression<System.Func<T, bool>> predicate);
            }
            public class StoreOptions
            {
                public Marten.Events.Projections.ProjectionOptions Projections { get; } = new();
            }
            public class MartenConfigurationExpression
            {
                public MartenConfigurationExpression AddProjectionWithServices<T>(
                    JasperFx.Events.Projections.ProjectionLifecycle lifecycle,
                    int lifetime) => this;
            }
        }

        namespace Marten.Events.Projections
        {
            public class ProjectionOptions
            {
                public void Add<T>(JasperFx.Events.Projections.ProjectionLifecycle lifecycle) where T : JasperFx.Events.Projections.IProjection { }
                public void Add(JasperFx.Events.Projections.IProjection projection, JasperFx.Events.Projections.ProjectionLifecycle lifecycle) { }
                public void Snapshot<T>(JasperFx.Events.Projections.SnapshotLifecycle lifecycle) { }
                public void LiveStreamAggregation<T>() { }
            }
            public abstract class MultiStreamProjection<T, TId> : JasperFx.Events.Projections.IProjection;
            public abstract class EventProjection : JasperFx.Events.Projections.IProjection
            {
                public JasperFx.Events.Projections.AsyncOptions Options { get; } = new();
                public virtual System.Threading.Tasks.ValueTask ApplyAsync(
                    Marten.IDocumentOperations operations,
                    JasperFx.Events.IEvent e,
                    System.Threading.CancellationToken cancellation) => default;
            }
        }

        namespace Marten.Events.Aggregation
        {
            public abstract class SingleStreamProjection<T> : JasperFx.Events.Projections.IProjection;
            public abstract class SingleStreamProjection<T, TId> : JasperFx.Events.Projections.IProjection;
        }

        namespace Lookalike
        {
            public class ProjectionOptions
            {
                public void Add<T>(int lifecycle) { }
            }
        }
        """;

    const string FirstSource =
        """
        namespace Variants;

        public record Opened(System.Guid Id);
        public record Closed(System.Guid Id);
        public record Renamed(System.Guid Id);
        public record Archived(System.Guid Id);
        public record Restored(System.Guid Id);
        public record Pinged(System.Guid Id);

        public class OpenedView { public System.Guid Id { get; set; } }
        public class ClosedView { public System.Guid Id { get; set; } }
        public class RenamedView { public System.Guid Id { get; set; } }
        public class ArchivedView { public System.Guid Id { get; set; } }
        public class RestoredView { public System.Guid Id { get; set; } }
        public class PingedView { public System.Guid Id { get; set; } }
        public class WrappedView { public System.Guid Id { get; set; } }
        public class AsyncView { public System.Guid Id { get; set; } }
        public class ValueAsyncView { public System.Guid Id { get; set; } }
        public class TransformView { public System.Guid Id { get; set; } }
        public class SharedView { public System.Guid Id { get; set; } }
        public class SecondTeardownView { public System.Guid Id { get; set; } }
        public class MixedLabelView { public System.Guid Id { get; set; } }
        public class UnresolvedLabelView { public System.Guid Id { get; set; } }

        public class Aggregate { public System.Guid Id { get; set; } }
        public class Folded { public int Id { get; set; } }
        public class Snap { public System.Guid Id { get; set; } }
        public class Live { public System.Guid Id { get; set; } }
        public class Registered { public System.Guid Id { get; set; } }

        public partial class ConventionalProjection : Marten.Events.Projections.EventProjection
        {
            public ConventionalProjection()
            {
                Options.DeleteViewTypeOnTeardown<OpenedView>();
            }

            public OpenedView Create(Opened e) => new();
            public WrappedView Create(JasperFx.Events.IEvent<Closed> e) => new();
            public System.Threading.Tasks.Task<AsyncView> Create(Renamed e) => System.Threading.Tasks.Task.FromResult(new AsyncView());
            public System.Threading.Tasks.ValueTask<ValueAsyncView> Create(Archived e) => new(new ValueAsyncView());
            public TransformView Transform(Restored e, JasperFx.Events.IEvent metadata) => new();
            public int Create(Pinged e) => 0;
            public OpenedView Create(Marten.IDocumentOperations operations, System.Threading.CancellationToken token, Closed e) => new();
            public SharedView Create(Opened e, int extra) => new();
            public void Project(Closed e, Marten.IDocumentOperations operations)
            {
                operations.Store(new ClosedView());
                operations.Delete<ClosedView>(e.Id);
                operations.Update(new SharedView());
            }
        }

        public partial class SwitchProjection : Marten.Events.Projections.EventProjection
        {
            public override System.Threading.Tasks.ValueTask ApplyAsync(
                Marten.IDocumentOperations operations,
                JasperFx.Events.IEvent e,
                System.Threading.CancellationToken cancellation)
            {
                switch (e.Data)
                {
                    case Opened opened:
                        operations.Insert(new OpenedView());
                        break;
                    case Closed:
                        operations.Store(new ClosedView());
                        break;
                    case Renamed or Archived:
                        operations.Update(new RenamedView());
                        break;
                    case Restored restored when restored.Id != System.Guid.Empty:
                        operations.Store(new RestoredView());
                        operations.Store(new SharedView());
                        break;
                    case string text:
                        operations.Store(new UnresolvedLabelView());
                        break;
                    case Pinged pinged:
                    case Restored again:
                        operations.Store(new MixedLabelView());
                        break;
                }

                return default;
            }
        }
        """;

    const string SecondSource =
        """
        namespace Variants;

        public partial class ConventionalProjection
        {
            public void Project(Archived e, Marten.IDocumentOperations operations)
            {
                operations.Store(new SharedView());
                operations.Insert(new PingedView());
            }
        }

        public partial class SwitchProjection
        {
            public void Helper(Marten.IDocumentOperations operations)
            {
                operations.Store(new SharedView());
            }
        }

        public class SecondTeardownProjection : Marten.Events.Projections.EventProjection
        {
            public SecondTeardownProjection()
            {
                Options.DeleteViewTypeOnTeardown<SecondTeardownView>();
            }

            public SecondTeardownView Create(Opened e) => new();
        }

        public class EmptyProjection : Marten.Events.Projections.EventProjection;

        public class AggregateProjection : Marten.Events.Aggregation.SingleStreamProjection<Aggregate, System.Guid>;
        public class OneIdProjection : Marten.Events.Aggregation.SingleStreamProjection<Aggregate>;
        public class FoldedProjection : Marten.Events.Projections.MultiStreamProjection<Folded, int>;
        public class UnregisteredProjection : Marten.Events.Aggregation.SingleStreamProjection<Registered, System.Guid>;

        public static class Configuration
        {
            public const JasperFx.Events.Projections.ProjectionLifecycle Fixed = JasperFx.Events.Projections.ProjectionLifecycle.Live;

            public static void Configure(Marten.StoreOptions options, Marten.MartenConfigurationExpression services, Lookalike.ProjectionOptions lookalike)
            {
                options.Projections.Add<AggregateProjection>(JasperFx.Events.Projections.ProjectionLifecycle.Async);
                options.Projections.Add(new FoldedProjection(), (JasperFx.Events.Projections.ProjectionLifecycle)0);
                options.Projections.Add(new ConventionalProjection(), Fixed);
                options.Projections.Add(new ConventionalProjection(), JasperFx.Events.Projections.ProjectionLifecycle.Inline);
                options.Projections.Snapshot<Snap>(JasperFx.Events.Projections.SnapshotLifecycle.Async);
                options.Projections.Snapshot<Snap>((JasperFx.Events.Projections.SnapshotLifecycle)0);
                options.Projections.LiveStreamAggregation<Live>();
                services.AddProjectionWithServices<SwitchProjection>(JasperFx.Events.Projections.ProjectionLifecycle.Async, 1);
                lookalike.Add<UnregisteredProjection>(1);
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

    void Establish()
    {
        var trees = new[]
        {
            CSharpSyntaxTree.ParseText(FrameworkSource, path: "/workspace/Framework.cs"),
            CSharpSyntaxTree.ParseText(FirstSource, path: "/workspace/Variants/A.cs"),
            CSharpSyntaxTree.ParseText(SecondSource, path: "/workspace/Variants/B.cs")
        };
        var compilation = CSharpCompilation.Create(
            "Variants",
            trees,
            _references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, nullableContextOptions: NullableContextOptions.Enable));
        compilation.GetDiagnostics().Where(_ => _.Severity == DiagnosticSeverity.Error).ShouldBeEmpty();
        var project = new DotNetProjectCompilation
        {
            Name = "Variants",
            ProjectPath = "/workspace/Variants/Variants.csproj",
            SourceRoot = "/workspace",
            Compilation = compilation,
            AuthoredSyntaxTrees = new HashSet<SyntaxTree>(trees)
        };
        Contribution = new CritterStackScreenplayAdapter().Analyze(
            new DotNetAnalysisContext([project]),
            new DotNetAdapterOptions());
    }
}
