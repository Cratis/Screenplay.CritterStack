// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Cratis.CritterStack.Screenplay.given;

public class a_legacy_marten_projection_metadata_application : Specification
{
    const string FrameworkSource =
        """
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
            }
            public class MartenConfigurationExpression
            {
                public MartenConfigurationExpression AddAsyncDaemon(Marten.Events.Daemon.Resiliency.DaemonMode mode) => this;
            }
        }

        namespace Marten.Events.Daemon.Resiliency
        {
            public enum DaemonMode { Disabled, Solo, HotCold }
        }

        namespace Marten.Events.Daemon
        {
            public class DaemonSettings
            {
                public Marten.Events.Daemon.Resiliency.DaemonMode AsyncMode { get; set; }
            }
        }

        namespace Marten.Events.Projections
        {
            public enum ProjectionLifecycle { Inline, Async, Live }
            public abstract class ProjectionBase
            {
                public string? ProjectionName { get; set; }
            }
            public interface IProjection
            {
                void Apply(Marten.IDocumentOperations operations, System.Collections.Generic.IReadOnlyList<object> streams);
                System.Threading.Tasks.Task ApplyAsync(
                    Marten.IDocumentOperations operations,
                    System.Collections.Generic.IReadOnlyList<object> streams,
                    System.Threading.CancellationToken cancellation);
            }
            public class ProjectionOptions : Marten.Events.Daemon.DaemonSettings
            {
                public void Add(
                    IProjection projection,
                    ProjectionLifecycle lifecycle,
                    string? projectionName = null) { }
            }
        }
        """;

    const string ApplicationSource =
        """
        namespace Legacy;

        public record HiddenDocument(System.Guid Id);

        public sealed class NamedLegacyProjection : Marten.Events.Projections.ProjectionBase, Marten.Events.Projections.IProjection
        {
            public NamedLegacyProjection() => ProjectionName = "legacy-named";

            public void Apply(Marten.IDocumentOperations operations, System.Collections.Generic.IReadOnlyList<object> streams) { }
            public System.Threading.Tasks.Task ApplyAsync(
                Marten.IDocumentOperations operations,
                System.Collections.Generic.IReadOnlyList<object> streams,
                System.Threading.CancellationToken cancellation)
            {
                operations.Store(new HiddenDocument(System.Guid.Empty));
                return System.Threading.Tasks.Task.CompletedTask;
            }
        }

        public sealed class RawLegacyProjection : Marten.Events.Projections.IProjection
        {
            public void Apply(Marten.IDocumentOperations operations, System.Collections.Generic.IReadOnlyList<object> streams) { }
            public System.Threading.Tasks.Task ApplyAsync(
                Marten.IDocumentOperations operations,
                System.Collections.Generic.IReadOnlyList<object> streams,
                System.Threading.CancellationToken cancellation)
            {
                operations.Store(new HiddenDocument(System.Guid.Empty));
                return System.Threading.Tasks.Task.CompletedTask;
            }
        }

        public static class Configuration
        {
            public static void Configure(
                Marten.StoreOptions options,
                Marten.MartenConfigurationExpression services)
            {
                options.Projections.Add(
                    new NamedLegacyProjection(),
                    Marten.Events.Projections.ProjectionLifecycle.Async);
                options.Projections.Add(
                    new RawLegacyProjection(),
                    Marten.Events.Projections.ProjectionLifecycle.Inline,
                    "legacy-raw");
                options.Projections.AsyncMode = Marten.Events.Daemon.Resiliency.DaemonMode.HotCold;
                services.AddAsyncDaemon(Marten.Events.Daemon.Resiliency.DaemonMode.Solo);
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
        var applicationSyntaxTree = CSharpSyntaxTree.ParseText(ApplicationSource, path: "/workspace/Legacy/Configuration.cs");
        var compilation = CSharpCompilation.Create(
            "Legacy",
            [
                frameworkSyntaxTree,
                applicationSyntaxTree
            ],
            _references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, nullableContextOptions: NullableContextOptions.Enable));
        compilation.GetDiagnostics().Where(_ => _.Severity == DiagnosticSeverity.Error).ShouldBeEmpty();
        var project = new DotNetProjectCompilation
        {
            Name = "Legacy",
            ProjectPath = "/workspace/Legacy/Legacy.csproj",
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
