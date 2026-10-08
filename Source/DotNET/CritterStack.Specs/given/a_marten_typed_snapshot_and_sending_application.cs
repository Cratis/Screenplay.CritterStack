// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Cratis.CritterStack.Screenplay.given;

public class a_marten_typed_snapshot_and_sending_application : Specification
{
    const string FrameworkSource =
        """
        namespace Wolverine
        {
            public interface IMessageContext;
            public interface ISendMyself { System.Threading.Tasks.ValueTask ApplyAsync(IMessageContext context); }
            public abstract record TimeoutMessage : ISendMyself
            {
                public System.Threading.Tasks.ValueTask ApplyAsync(IMessageContext context) => default;
            }
        }
        namespace Marten { public class StoreOptions; }
        namespace JasperFx.Events
        {
            public interface IEventSlice<T> { void PublishMessage(object message); }
        }
        namespace JasperFx.Events.Projections
        {
            public class ProjectionBase;
            public class AsyncOptions;
            public enum SnapshotLifecycle { Inline, Async }
        }
        namespace Marten.Events.Projections
        {
            public class ProjectionOptions
            {
                public void Snapshot<T, TId>(JasperFx.Events.Projections.SnapshotLifecycle lifecycle, System.Action<JasperFx.Events.Projections.ProjectionBase>? configure = null, System.Action<JasperFx.Events.Projections.AsyncOptions>? asyncConfiguration = null) { }
                public void LiveStreamAggregation<T, TId>(System.Action<JasperFx.Events.Projections.AsyncOptions>? asyncConfiguration = null) { }
            }
        }
        namespace Marten.Events.Aggregation { public abstract class SingleStreamProjection<T>; }
        """;

    const string ApplicationSource =
        """
        namespace NewConventions;
        public record NaturalId(string Value);
        public record Opened(string Name);
        public class SnapshotModel
        {
            public NaturalId Id { get; set; } = new("");
            public void Apply(Opened value) { }
        }
        public class LiveModel
        {
            public NaturalId Id { get; set; } = new("");
            public void Apply(Opened value) { }
        }
        public static class Configuration
        {
            public static void Configure(Marten.Events.Projections.ProjectionOptions projections)
            {
                projections.Snapshot<SnapshotModel, NaturalId>(JasperFx.Events.Projections.SnapshotLifecycle.Async, configure: _ => { }, asyncConfiguration: _ => { });
                projections.LiveStreamAggregation<LiveModel, NaturalId>(asyncConfiguration: _ => { });
            }
        }
        public record Changed;
        public record Reminder : Wolverine.TimeoutMessage;
        public interface ISendMyself;
        public record Impostor : ISendMyself;
        public record Delivery : Wolverine.ISendMyself
        {
            public System.Threading.Tasks.ValueTask ApplyAsync(Wolverine.IMessageContext context) => default;
        }
        public class Projection : Marten.Events.Aggregation.SingleStreamProjection<SnapshotModel>
        {
            public void Apply(Opened value, SnapshotModel model, JasperFx.Events.IEventSlice<SnapshotModel> slice)
            {
                slice.PublishMessage(new Changed());
                slice.PublishMessage(new Reminder());
                slice.PublishMessage(new Delivery());
                slice.PublishMessage(new Impostor());
            }
        }
        """;

    protected DotNetProjectCompilation Project = null!;

    void Establish()
    {
        var framework = CSharpSyntaxTree.ParseText(FrameworkSource, path: "/workspace/Framework.cs");
        var application = CSharpSyntaxTree.ParseText(ApplicationSource, path: "/workspace/NewConventions/Projections.cs");
        var references = ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")!).Split(Path.PathSeparator).Select(path => MetadataReference.CreateFromFile(path));
        Project = new DotNetProjectCompilation
        {
            Name = "NewConventions",
            ProjectPath = "/workspace/NewConventions/NewConventions.csproj",
            SourceRoot = "/workspace",
            Compilation = CSharpCompilation.Create("NewConventions", [framework, application], references, new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, nullableContextOptions: NullableContextOptions.Enable)),
            AuthoredSyntaxTrees = new HashSet<SyntaxTree> { framework, application }
        };
    }
}
