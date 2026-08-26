// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Cratis.CritterStack.Screenplay.given;

public class a_cross_project_source_placement_application : Specification
{
    const string FrameworkSource =
        """
        namespace Marten
        {
            public class StoreOptions
            {
                public Marten.Events.Projections.ProjectionOptions Projections { get; } = new();
                public MartenRegistry Schema { get; } = new();
            }

            public sealed class MartenRegistry
            {
                public DocumentMappingExpression<T> For<T>() => new();

                public sealed class DocumentMappingExpression<T>
                {
                    public DocumentMappingExpression<T> MultiTenanted() => this;
                }
            }
        }

        namespace Marten.Events.Projections
        {
            public enum ProjectionLifecycle { Inline }
            public class ProjectionOptions
            {
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
        }

        namespace Wolverine.Runtime.Handlers
        {
            public sealed class HandlerChain;
        }

        namespace Wolverine.Configuration
        {
            public interface IWolverinePolicy;
        }
        """;

    const string DomainSource =
        """
        namespace Domain.Orders;

        public record SubmitOrder(System.Guid OrderId);
        public record OrderSubmitted(System.Guid OrderId);
        public record NotifyOrder(System.Guid OrderId);
        public record Notification(System.Guid OrderId);

        public class Order
        {
            public System.Guid Id { get; set; }
        }

        public class OrderSummary
        {
            public System.Guid Id { get; set; }
        }
        """;

    const string ProjectionSource =
        """
        namespace Application.Orders.Configuration;

        public sealed class OrderSummaryProjection : Marten.Events.Aggregation.SingleStreamProjection<Domain.Orders.OrderSummary>
        {
            public void Apply(Domain.Orders.OrderSubmitted @event, Domain.Orders.OrderSummary summary) { }
        }

        public static class MartenConfiguration
        {
            public static void Configure(Marten.StoreOptions options)
            {
                options.Projections.Add<OrderSummaryProjection>(Marten.Events.Projections.ProjectionLifecycle.Inline);
                options.Schema.For<Domain.Orders.OrderSummary>().MultiTenanted();
            }
        }
        """;

    const string HandlerSource =
        """
        namespace Application.Orders.Configuration;

        public sealed class CustomWolverinePolicy : Wolverine.Configuration.IWolverinePolicy;

        public static class OrderAggregateHandler
        {
            public static Domain.Orders.OrderSubmitted Handle(
                Domain.Orders.SubmitOrder command,
                Domain.Orders.Order order) => new(command.OrderId);

            public static void Configure(Wolverine.Runtime.Handlers.HandlerChain chain) { }
        }

        public static class NotificationHandler
        {
            public static Domain.Orders.Notification Handle(Domain.Orders.NotifyOrder message) => new(message.OrderId);
        }
        """;

    const string EndpointSource =
        """
        namespace Application.Orders.Configuration;

        public static class OrderEndpoints
        {
            [Wolverine.Http.WolverineGet("/orders/{id}")]
            public static Domain.Orders.OrderSummary GetOrder(System.Guid id) => new();
        }
        """;

    static readonly IReadOnlyList<MetadataReference> _references =
    [
        .. ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")!)
            .Split(Path.PathSeparator)
            .Select(_ => MetadataReference.CreateFromFile(_))
    ];

    protected CritterStackScreenplayAdapter Adapter = null!;
    protected DotNetAdapterOptions AdapterOptions = null!;

    void Establish()
    {
        Adapter = new();
        AdapterOptions = new DotNetAdapterOptions
        {
            FeatureRoot = "Source",
            NamespaceSegmentsToSkip = 1
        };
    }

    protected static ProjectPair CreateProjects(string physicalRoot = "/workspace", bool includeSourceContexts = true)
    {
        var domainTree = CSharpSyntaxTree.ParseText(
            DomainSource,
            path: $"{physicalRoot}/Domain/Source/Orders/Domain/Domain.cs");
        var domainCompilation = CSharpCompilation.Create(
            "Domain",
            [domainTree],
            _references,
            CompilationOptions());
        domainCompilation.GetDiagnostics().Where(_ => _.Severity == DiagnosticSeverity.Error).ShouldBeEmpty();

        var frameworkTree = CSharpSyntaxTree.ParseText(FrameworkSource, path: $"{physicalRoot}/Application/Framework.cs");
        var projectionTree = CSharpSyntaxTree.ParseText(
            ProjectionSource,
            path: $"{physicalRoot}/Application/Source/Orders/Configuration/Projection.cs");
        var handlerTree = CSharpSyntaxTree.ParseText(
            HandlerSource,
            path: $"{physicalRoot}/Application/Source/Orders/Configuration/Handlers.cs");
        var endpointTree = CSharpSyntaxTree.ParseText(
            EndpointSource,
            path: $"{physicalRoot}/Application/Source/Orders/Configuration/Endpoints.cs");
        SyntaxTree[] applicationTrees = [frameworkTree, projectionTree, handlerTree, endpointTree];
        var applicationCompilation = CSharpCompilation.Create(
            "Application",
            applicationTrees,
            [.. _references, domainCompilation.ToMetadataReference()],
            CompilationOptions());
        applicationCompilation.GetDiagnostics().Where(_ => _.Severity == DiagnosticSeverity.Error).ShouldBeEmpty();

        var domainProject = new DotNetProjectCompilation
        {
            Name = "Domain",
            Role = DotNetProjectRole.Application,
            ProjectPath = $"{physicalRoot}/Domain/Domain.csproj",
            SourceContext = includeSourceContexts
                ? SourceContext(
                    "Domain/Domain",
                    domainTree,
                    "Source/Orders/Domain/Domain.cs",
                    "Domain/Source/Orders/Domain/Domain.cs")
                : null,
            Compilation = domainCompilation,
            AuthoredSyntaxTrees = new[] { domainTree }.ToHashSet<SyntaxTree>()
        };
        var applicationProject = new DotNetProjectCompilation
        {
            Name = "Application",
            Role = DotNetProjectRole.Application,
            ProjectPath = $"{physicalRoot}/Application/Application.csproj",
            SourceContext = includeSourceContexts
                ? DotNetSourcePaths.Create(
                    "Application/Application",
                    SourcePathPolicy(),
                    [
                        Document(projectionTree, "Source/Orders/Configuration/Projection.cs", "Application/Source/Orders/Configuration/Projection.cs"),
                        Document(handlerTree, "Source/Orders/Configuration/Handlers.cs", "Application/Source/Orders/Configuration/Handlers.cs"),
                        Document(endpointTree, "Source/Orders/Configuration/Endpoints.cs", "Application/Source/Orders/Configuration/Endpoints.cs")
                    ])
                : null,
            Compilation = applicationCompilation,
            AuthoredSyntaxTrees = new[] { projectionTree, handlerTree, endpointTree }.ToHashSet<SyntaxTree>()
        };

        return new(applicationProject, domainProject);
    }

    protected static DotNetProjectCompilation CreateMetadataNameCollisionProject(string physicalRoot = "/workspace")
    {
        var tree = CSharpSyntaxTree.ParseText(
            "[assembly: System.Reflection.AssemblyVersion(\"2.0.0.0\")]\n" + DomainSource,
            path: $"{physicalRoot}/UnrelatedDomain/Source/Orders/Domain/Domain.cs");
        var compilation = CSharpCompilation.Create(
            "Domain",
            [tree],
            _references,
            CompilationOptions());
        compilation.GetDiagnostics().Where(_ => _.Severity == DiagnosticSeverity.Error).ShouldBeEmpty();

        return new()
        {
            Name = "UnrelatedDomain",
            Role = DotNetProjectRole.Application,
            ProjectPath = $"{physicalRoot}/UnrelatedDomain/UnrelatedDomain.csproj",
            SourceContext = SourceContext(
                "UnrelatedDomain/UnrelatedDomain",
                tree,
                "Source/Orders/Domain/Domain.cs",
                "UnrelatedDomain/Source/Orders/Domain/Domain.cs"),
            Compilation = compilation,
            AuthoredSyntaxTrees = new[] { tree }.ToHashSet<SyntaxTree>()
        };
    }

    protected static DotNetProjectCompilation CreateIndependentProject(ProjectPair projects, string physicalRoot = "/workspace")
    {
        var tree = CSharpSyntaxTree.ParseText(
            """
            namespace Payments.Billing;

            public record CapturePayment(System.Guid PaymentId);
            public record PaymentCaptured(System.Guid PaymentId);

            public class Payment
            {
                public System.Guid Id { get; set; }
            }

            public static class PaymentHandler
            {
                public static PaymentCaptured Handle(CapturePayment message) => new(message.PaymentId);
            }
            """,
            path: $"{physicalRoot}/Payments/Source/Billing/Capture.cs");
        var compilation = CSharpCompilation.Create(
            "Payments",
            [tree],
            [.. _references, projects.Application.Compilation.ToMetadataReference()],
            CompilationOptions());
        compilation.GetDiagnostics().Where(_ => _.Severity == DiagnosticSeverity.Error).ShouldBeEmpty();

        return new()
        {
            Name = "Payments",
            Role = DotNetProjectRole.Application,
            ProjectPath = $"{physicalRoot}/Payments/Payments.csproj",
            SourceContext = SourceContext(
                "Payments/Payments",
                tree,
                "Source/Billing/Capture.cs",
                "Payments/Source/Billing/Capture.cs"),
            Compilation = compilation,
            AuthoredSyntaxTrees = new[] { tree }.ToHashSet<SyntaxTree>()
        };
    }

    static CSharpCompilationOptions CompilationOptions() => new(
        OutputKind.DynamicallyLinkedLibrary,
        nullableContextOptions: NullableContextOptions.Enable);

    static DotNetProjectSourceContext SourceContext(
        string projectIdentity,
        SyntaxTree tree,
        string projectRelativePath,
        string workspaceRelativePath) => DotNetSourcePaths.Create(
            projectIdentity,
            SourcePathPolicy(),
            [Document(tree, projectRelativePath, workspaceRelativePath)]);

    static DotNetSourcePathPolicy SourcePathPolicy() => new()
    {
        Version = 1,
        DisplayRoot = DotNetSourceDisplayRoot.Workspace,
        CasePolicy = DotNetSourcePathCasePolicy.Ordinal
    };

    static DotNetSourceDocument Document(
        SyntaxTree tree,
        string projectRelativePath,
        string workspaceRelativePath) => new()
        {
            SyntaxTree = tree,
            ProjectRelativePath = projectRelativePath,
            WorkspaceRelativePath = workspaceRelativePath
        };

    protected sealed record ProjectPair(
        DotNetProjectCompilation Application,
        DotNetProjectCompilation Domain);
}
