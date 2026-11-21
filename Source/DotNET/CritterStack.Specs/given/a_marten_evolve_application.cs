// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Cratis.CritterStack.Screenplay.given;

public class a_marten_evolve_application : Specification
{
    const string FrameworkSource =
        """
        namespace JasperFx.Events
        {
            public interface IEvent;
            public interface IEvent<T> : IEvent;
        }
        namespace JasperFx.Events.Daemon
        {
            public interface IAggregationSteps<TDoc, out TQuerySession>
            {
                IAggregationSteps<TDoc, TQuerySession> DeleteEvent<TEvent>() where TEvent : class;
                IAggregationSteps<TDoc, TQuerySession> TransformsEvent<TEvent>() where TEvent : class;
            }
        }
        namespace JasperFx.Events.Aggregation
        {
            public enum ActionType { Nothing, Store, Delete }
            public interface IIdentitySetter<TDoc, TId>;
            public abstract class JasperFxAggregationProjectionBase<TDoc, TId, TOperations, TQuerySession> : JasperFx.Events.Daemon.IAggregationSteps<TDoc, TQuerySession>
                where TDoc : class
            {
                public virtual TDoc? Evolve(TDoc? snapshot, TId id, JasperFx.Events.IEvent e) => snapshot;
                public virtual System.Threading.Tasks.ValueTask<TDoc?> EvolveAsync(TDoc? snapshot, TId id, TQuerySession session, JasperFx.Events.IEvent e, System.Threading.CancellationToken cancellation) => new(snapshot);
                public virtual (TDoc?, ActionType) DetermineAction(TDoc? snapshot, TId identity, System.Collections.Generic.IReadOnlyList<JasperFx.Events.IEvent> events) => (snapshot, ActionType.Nothing);
                public virtual System.Threading.Tasks.ValueTask<(TDoc?, ActionType)> DetermineActionAsync(TQuerySession session, TDoc? snapshot, TId identity, IIdentitySetter<TDoc, TId> identitySetter, System.Collections.Generic.IReadOnlyList<JasperFx.Events.IEvent> events, System.Threading.CancellationToken cancellation) => new((snapshot, ActionType.Nothing));
                public JasperFx.Events.Daemon.IAggregationSteps<TDoc, TQuerySession> DeleteEvent<TEvent>() where TEvent : class => this;
                public JasperFx.Events.Daemon.IAggregationSteps<TDoc, TQuerySession> TransformsEvent<TEvent>() where TEvent : class => this;
            }
        }
        namespace Marten
        {
            public interface IQuerySession;
            public interface IDocumentOperations : IQuerySession;
            public class StoreOptions;
        }
        namespace Marten.Events.Aggregation
        {
            public abstract class SingleStreamProjection<TDoc, TId> : JasperFx.Events.Aggregation.JasperFxAggregationProjectionBase<TDoc, TId, Marten.IDocumentOperations, Marten.IQuerySession> where TDoc : class;
        }
        """;

    const string ApplicationSource =
        """
        namespace Evolution;
        public record Applied;
        public record Created;
        public record Deleted;
        public record RegisteredDelete;
        public record ChainedDelete;
        public record NotAnEvent;
        public record IgnoredLambda;
        public class Doc { public System.Guid Id { get; set; } }
        public class IntermediateProjection : Marten.Events.Aggregation.SingleStreamProjection<Doc, System.Guid>
        {
            public override Doc? Evolve(Doc? snapshot, System.Guid id, JasperFx.Events.IEvent e) => snapshot;
        }
        public class SyncProjection : IntermediateProjection
        {
            public override Doc? Evolve(Doc? snapshot, System.Guid id, JasperFx.Events.IEvent e) => snapshot;
            private void Evolve(string value) { }
        }
        public class AsyncProjection : Marten.Events.Aggregation.SingleStreamProjection<Doc, System.Guid>
        {
            public override System.Threading.Tasks.ValueTask<Doc?> EvolveAsync(Doc? snapshot, System.Guid id, Marten.IQuerySession session, JasperFx.Events.IEvent e, System.Threading.CancellationToken cancellation) => new(snapshot);
        }
        public class ActionProjection : Marten.Events.Aggregation.SingleStreamProjection<Doc, System.Guid>
        {
            public override (Doc?, JasperFx.Events.Aggregation.ActionType) DetermineAction(Doc? snapshot, System.Guid identity, System.Collections.Generic.IReadOnlyList<JasperFx.Events.IEvent> events) => (snapshot, JasperFx.Events.Aggregation.ActionType.Nothing);
        }
        public class AsyncActionProjection : Marten.Events.Aggregation.SingleStreamProjection<Doc, System.Guid>
        {
            public override System.Threading.Tasks.ValueTask<(Doc?, JasperFx.Events.Aggregation.ActionType)> DetermineActionAsync(Marten.IQuerySession session, Doc? snapshot, System.Guid identity, JasperFx.Events.Aggregation.IIdentitySetter<Doc, System.Guid> identitySetter, System.Collections.Generic.IReadOnlyList<JasperFx.Events.IEvent> events, System.Threading.CancellationToken cancellation) => new((snapshot, JasperFx.Events.Aggregation.ActionType.Nothing));
        }
        public class ConventionalProjection : Marten.Events.Aggregation.SingleStreamProjection<Doc, System.Guid>
        {
            public ConventionalProjection()
            {
                DeleteEvent<RegisteredDelete>().DeleteEvent<ChainedDelete>();
                DeleteEvent<IgnoredLambda>(_ => { });
                System.Action deferred = () => DeleteEvent<NotAnEvent>();
                void Local() => DeleteEvent<NotAnEvent>();
            }
            public ConventionalProjection(Marten.Events.Aggregation.SingleStreamProjection<Doc, System.Guid> other)
            {
                other.DeleteEvent<NotAnEvent>();
            }
            public void Apply(Applied e, Doc doc) { }
            public Doc Create(JasperFx.Events.IEvent<Created> e) => new();
            public bool ShouldDelete(Deleted e) => true;
            public void DeleteEvent<T>(System.Action<T> configure) { }
            public void Helper() => DeleteEvent<NotAnEvent>();
        }
        public class TypedProjection : Marten.Events.Aggregation.SingleStreamProjection<Doc, System.Guid>
        {
            public Doc? Evolve(Doc? snapshot, System.Guid id, JasperFx.Events.IEvent<NotAnEvent> e) => snapshot;
        }
        public class PlainProjection : Marten.Events.Aggregation.SingleStreamProjection<Doc, System.Guid>
        {
            public Doc Evolve(Doc snapshot, NotAnEvent e) => snapshot;
        }
        public class HidingProjection : Marten.Events.Aggregation.SingleStreamProjection<Doc, System.Guid>
        {
            public new Doc? Evolve(Doc? snapshot, System.Guid id, JasperFx.Events.IEvent e) => snapshot;
        }
        public class HelperProjection : Marten.Events.Aggregation.SingleStreamProjection<Doc, System.Guid>
        {
            private void Evolve(string value) { }
        }
        public class UnrelatedBase
        {
            public virtual Doc? Evolve(Doc? snapshot, System.Guid id, JasperFx.Events.IEvent e) => snapshot;
        }
        public class Unrelated : UnrelatedBase
        {
            public override Doc? Evolve(Doc? snapshot, System.Guid id, JasperFx.Events.IEvent e) => snapshot;
        }
        """;

    protected DotNetAnalysisContext Context = null!;

    static readonly Lazy<DotNetProjectCompilation> _project = new(CreateProject);

    protected DotNetProjectCompilation Project => _project.Value;

    void Establish() => Context = new DotNetAnalysisContext([Project]);

    static DotNetProjectCompilation CreateProject()
    {
        var framework = CSharpSyntaxTree.ParseText(FrameworkSource, path: "/workspace/Framework.cs");
        var application = CSharpSyntaxTree.ParseText(ApplicationSource, path: "/workspace/Evolution/Projections.cs");
        var references = ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")).Split(Path.PathSeparator).Select(path => MetadataReference.CreateFromFile(path));
        return new DotNetProjectCompilation
        {
            Name = "Evolution",
            ProjectPath = "/workspace/Evolution/Evolution.csproj",
            SourceRoot = "/workspace",
            Compilation = CSharpCompilation.Create("Evolution", [framework, application], references, new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, nullableContextOptions: NullableContextOptions.Enable)),
            AuthoredSyntaxTrees = new HashSet<SyntaxTree> { application }
        };
    }
}
