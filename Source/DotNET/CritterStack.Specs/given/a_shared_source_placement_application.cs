// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Cratis.CritterStack.Screenplay.given;

public class a_shared_source_placement_application : Specification
{
    const string FrameworkSource =
        """
        namespace Marten
        {
            public class StoreOptions
            {
                public Marten.Events.Projections.ProjectionOptions Projections { get; } = new();
            }
        }

        namespace Marten.Events.Projections
        {
            public interface IProjection;
            public enum SnapshotLifecycle { Inline }
            public enum ProjectionLifecycle { Inline }
            public class ProjectionOptions
            {
                public void Snapshot<T>(SnapshotLifecycle lifecycle) { }
                public void Add<T>(ProjectionLifecycle lifecycle) { }
            }
        }

        namespace Marten.Events.Aggregation
        {
            public abstract class SingleStreamProjection<T>;
        }

        namespace Wolverine
        {
            public class WolverineOptions;
        }

        namespace Wolverine.Http
        {
            [System.AttributeUsage(System.AttributeTargets.Method)]
            public sealed class WolverineGetAttribute(string route) : System.Attribute;

            [System.AttributeUsage(System.AttributeTargets.Method)]
            public sealed class WolverinePostAttribute(string route) : System.Attribute;
        }
        """;

    const string StateChangeSource =
        """
        namespace Application.Orders.Submit;

        public partial record SubmitOrder(System.Guid OrderId);
        public record OrderSubmitted(System.Guid OrderId);
        public record OrderCancelled(System.Guid OrderId);
        public class Order;

        public static class OrderAggregateHandler
        {
            public static OrderSubmitted Handle(SubmitOrder command, Order order) => new(command.OrderId);
        }

        public static class OrderCommandEndpoints
        {
            [Wolverine.Http.WolverinePost("/orders/{id}/cancel")]
            public static OrderCancelled Cancel(System.Guid id) => new(id);
        }
        """;

    const string FlatStateChangeSource =
        """
        namespace Application;

        public partial record SubmitOrder(System.Guid OrderId);
        public record OrderSubmitted(System.Guid OrderId);
        public class Order;

        public static class OrderAggregateHandler
        {
            public static OrderSubmitted Handle(SubmitOrder command, Order order) => new(command.OrderId);
        }
        """;

    const string ReadModelSource =
        """
        namespace Application.Orders.Summary;

        public record OrderSummarized(System.Guid OrderId);

        public class OrderSummary
        {
            public System.Guid Id { get; set; }
            public void Apply(OrderSummarized @event) { }
        }
        """;

    const string QuerySource =
        """
        namespace Application.Orders.Summary;

        public static class OrderEndpoints
        {
            [Wolverine.Http.WolverineGet("/orders/{id}")]
            public static OrderSummary GetOrder(System.Guid id) => new();
        }
        """;

    const string AggregateProjectionSource =
        """
        namespace Application.Orders.Projections;

        public sealed class OrderSummaryProjection : Marten.Events.Aggregation.SingleStreamProjection<Application.Orders.Summary.OrderSummary>;
        """;

    const string AutomationSource =
        """
        namespace Application.Orders.Notify;

        public record NotifyOrder(System.Guid OrderId);
        public record Notification(System.Guid OrderId);

        public static class NotificationHandler
        {
            public static Notification Handle(NotifyOrder message) => new(message.OrderId);
        }
        """;

    const string CustomProjectionSource =
        """
        namespace Application.Orders.Custom;

        public sealed class AuditProjection : Marten.Events.Projections.IProjection;
        """;

    const string ConfigurationSource =
        """
        namespace Application.Orders.Configuration;

        public static class MartenConfiguration
        {
            public static void Configure(Marten.StoreOptions options)
            {
                options.Projections.Snapshot<Application.Orders.Summary.OrderSummary>(Marten.Events.Projections.SnapshotLifecycle.Inline);
                options.Projections.Add<Application.Orders.Custom.AuditProjection>(Marten.Events.Projections.ProjectionLifecycle.Inline);
            }
        }
        """;

    const string ConflictingPartialSource =
        """
        namespace Application.Orders.Submit;

        public partial record SubmitOrder;
        """;

    static readonly IReadOnlyList<MetadataReference> _references =
    [
        .. ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")!)
            .Split(Path.PathSeparator)
            .Select(_ => MetadataReference.CreateFromFile(_))
    ];

    protected CritterStackScreenplayAdapter Adapter = null!;
    protected DotNetAdapterOptions AdapterOptions = null!;
    protected DotNetProjectCompilation Project = null!;

    void Establish()
    {
        Adapter = new();
        AdapterOptions = new DotNetAdapterOptions
        {
            FeatureRoot = "Source",
            NamespaceSegmentsToSkip = 1
        };
        Project = CreateProject();
    }

    protected static DotNetProjectCompilation CreateFlatProject(string physicalRoot = "/workspace")
    {
        var frameworkTree = CSharpSyntaxTree.ParseText(FrameworkSource, path: $"{physicalRoot}/Framework.cs");
        var authoredTree = CSharpSyntaxTree.ParseText(
            FlatStateChangeSource,
            path: $"{physicalRoot}/Application/Submit.cs");
        var compilation = CSharpCompilation.Create(
            "Application",
            [frameworkTree, authoredTree],
            _references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, nullableContextOptions: NullableContextOptions.Enable));
        compilation.GetDiagnostics().Where(_ => _.Severity == DiagnosticSeverity.Error).ShouldBeEmpty();

        return new()
        {
            Name = "Application",
            Role = DotNetProjectRole.Application,
            ProjectPath = $"{physicalRoot}/Application/Application.csproj",
            SourceContext = DotNetSourcePaths.Create(
                "Application/Application",
                new DotNetSourcePathPolicy
                {
                    Version = 1,
                    DisplayRoot = DotNetSourceDisplayRoot.Workspace,
                    CasePolicy = DotNetSourcePathCasePolicy.Ordinal
                },
                [
                    new DotNetSourceDocument
                    {
                        SyntaxTree = authoredTree,
                        ProjectRelativePath = "Submit.cs",
                        WorkspaceRelativePath = "Application/Submit.cs"
                    }
                ]),
            Compilation = compilation,
            AuthoredSyntaxTrees = new[] { authoredTree }.ToHashSet<SyntaxTree>()
        };
    }

    protected static DotNetProjectCompilation CreateEmptyProject(string physicalRoot = "/workspace")
    {
        var compilation = CSharpCompilation.Create(
            "EmptyApplication",
            references: _references,
            options: new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, nullableContextOptions: NullableContextOptions.Enable));
        compilation.GetDiagnostics().Where(_ => _.Severity == DiagnosticSeverity.Error).ShouldBeEmpty();

        return new()
        {
            Name = "EmptyApplication",
            Role = DotNetProjectRole.Application,
            ProjectPath = $"{physicalRoot}/EmptyApplication/EmptyApplication.csproj",
            SourceContext = DotNetSourcePaths.Create(
                "EmptyApplication/EmptyApplication",
                new DotNetSourcePathPolicy
                {
                    Version = 1,
                    DisplayRoot = DotNetSourceDisplayRoot.Workspace,
                    CasePolicy = DotNetSourcePathCasePolicy.Ordinal
                },
                []),
            Compilation = compilation,
            AuthoredSyntaxTrees = Array.Empty<SyntaxTree>().ToHashSet()
        };
    }

    protected static DotNetProjectCompilation CreateProject(
        bool reverseTrees = false,
        bool conflictingPartial = false,
        string physicalRoot = "/workspace")
    {
        var sources = new List<(string Source, string ProjectPath)>
        {
            (StateChangeSource, "Source/Orders/Submit/Submit.cs"),
            (ReadModelSource, "Source/Orders/Summary/OrderSummary.cs"),
            (QuerySource, "Source/Orders/Summary/OrderEndpoints.cs"),
            (AggregateProjectionSource, "Source/Orders/Projections/OrderSummaryProjection.cs"),
            (AutomationSource, "Source/Orders/Notify/Notify.cs"),
            (CustomProjectionSource, "Source/Orders/Custom/Custom.cs"),
            (ConfigurationSource, "Source/Orders/Configuration/Configuration.cs")
        };
        if (conflictingPartial)
        {
            sources.Add((ConflictingPartialSource, "Source/Orders/Other/Submit.cs"));
        }

        if (reverseTrees)
        {
            sources.Reverse();
        }

        var frameworkTree = CSharpSyntaxTree.ParseText(FrameworkSource, path: $"{physicalRoot}/Framework.cs");
        var authoredTrees = sources
            .Select(source => CSharpSyntaxTree.ParseText(source.Source, path: $"{physicalRoot}/Application/{source.ProjectPath}"))
            .ToArray();
        var compilation = CSharpCompilation.Create(
            "Application",
            [frameworkTree, .. authoredTrees],
            _references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, nullableContextOptions: NullableContextOptions.Enable));
        compilation.GetDiagnostics().Where(_ => _.Severity == DiagnosticSeverity.Error).ShouldBeEmpty();

        var documents = authoredTrees
            .Select((tree, index) => new DotNetSourceDocument
            {
                SyntaxTree = tree,
                ProjectRelativePath = sources[index].ProjectPath,
                WorkspaceRelativePath = $"Application/{sources[index].ProjectPath}"
            })
            .ToArray();
        return new()
        {
            Name = "Application",
            Role = DotNetProjectRole.Application,
            ProjectPath = $"{physicalRoot}/Application/Application.csproj",
            SourceContext = DotNetSourcePaths.Create(
                "Application/Application",
                new DotNetSourcePathPolicy
                {
                    Version = 1,
                    DisplayRoot = DotNetSourceDisplayRoot.Workspace,
                    CasePolicy = DotNetSourcePathCasePolicy.Ordinal
                },
                documents),
            Compilation = compilation,
            AuthoredSyntaxTrees = authoredTrees.ToHashSet<SyntaxTree>()
        };
    }
}
