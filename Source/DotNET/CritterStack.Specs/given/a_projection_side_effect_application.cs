// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Cratis.CritterStack.Screenplay.given;

public class a_projection_side_effect_application : Specification
{
    const string FrameworkSource =
        """
        namespace Marten
        {
            public class StoreOptions;
        }

        namespace Marten.Events
        {
            public interface IEventStoreOptions
            {
                bool EnableSideEffectsOnInlineProjections { get; set; }
            }
        }

        namespace Marten.Events.Aggregation
        {
            public abstract class SingleStreamProjection<T>;
        }

        namespace Marten.Events.Projections
        {
            public enum ProjectionLifecycle
            {
                Inline,
                Async
            }

            public class ProjectionOptions
            {
                public void Add<T>(T projection, ProjectionLifecycle lifecycle) { }
            }
        }

        namespace JasperFx.Events
        {
            public sealed class MessageMetadata;

            public interface IEventSlice<T>
            {
                void PublishMessage(object message);
                void PublishMessage(object message, MessageMetadata metadata);
            }
        }
        """;

    const string ApplicationSource =
        """
        namespace ProjectionSideEffects;

        public record Summary(string Id);
        public record ShipmentUpdated(string Id);
        public record SummaryChanged(string Id);

        public class SummaryProjection : Marten.Events.Aggregation.SingleStreamProjection<Summary>
        {
            public Summary Apply(ShipmentUpdated updated, Summary current, JasperFx.Events.IEventSlice<Summary> slice)
            {
                slice.PublishMessage(new SummaryChanged(updated.Id), new JasperFx.Events.MessageMetadata());
                object unresolved = new SummaryChanged(updated.Id);
                slice.PublishMessage(unresolved);
                return current with { Id = updated.Id };
            }
        }

        """;

    const string ConfigurationSource =
        """
        namespace ProjectionSideEffects;

        public static class Configuration
        {
            public static void Configure(
                Marten.Events.IEventStoreOptions events,
                Marten.Events.Projections.ProjectionOptions projections)
            {
                events.EnableSideEffectsOnInlineProjections = true;
                projections.Add(new SummaryProjection(), Marten.Events.Projections.ProjectionLifecycle.Inline);
                projections.Add(new SummaryProjection(), Marten.Events.Projections.ProjectionLifecycle.Inline);
            }
        }
        """;

    static readonly IReadOnlyList<MetadataReference> _references =
    [
        .. ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")!)
            .Split(Path.PathSeparator)
            .Select(_ => MetadataReference.CreateFromFile(_))
    ];

    protected DotNetProjectCompilation Project = null!;
    protected DotNetProjectCompilation UnconfiguredProject = null!;
    protected AdapterContribution Contribution = null!;
    protected AdapterContribution UnconfiguredContribution = null!;

    void Establish()
    {
        Project = CreateProject(includeConfiguration: true);
        UnconfiguredProject = CreateProject(includeConfiguration: false);
        var adapter = new CritterStackScreenplayAdapter();
        Contribution = adapter.Analyze(new DotNetAnalysisContext([Project]), new DotNetAdapterOptions());
        UnconfiguredContribution = adapter.Analyze(new DotNetAnalysisContext([UnconfiguredProject]), new DotNetAdapterOptions());
    }

    static DotNetProjectCompilation CreateProject(bool includeConfiguration)
    {
        var trees = new List<SyntaxTree>
        {
            CSharpSyntaxTree.ParseText(FrameworkSource, path: "/workspace/Framework.cs"),
            CSharpSyntaxTree.ParseText(ApplicationSource, path: "/workspace/ProjectionSideEffects/Projection.cs")
        };
        if (includeConfiguration)
        {
            trees.Add(CSharpSyntaxTree.ParseText(ConfigurationSource, path: "/workspace/ProjectionSideEffects/Configuration.cs"));
        }

        var compilation = CSharpCompilation.Create(
            "ProjectionSideEffects",
            trees,
            _references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, nullableContextOptions: NullableContextOptions.Enable));
        return new()
        {
            Name = "ProjectionSideEffects",
            ProjectPath = "/workspace/ProjectionSideEffects/ProjectionSideEffects.csproj",
            SourceRoot = "/workspace",
            Compilation = compilation,
            AuthoredSyntaxTrees = compilation.SyntaxTrees.ToHashSet()
        };
    }
}
