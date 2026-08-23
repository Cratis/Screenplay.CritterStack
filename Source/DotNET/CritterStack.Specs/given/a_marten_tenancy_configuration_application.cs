// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Cratis.CritterStack.Screenplay.given;

public class a_marten_tenancy_configuration_application : Specification
{
    const string CurrentFrameworkSource =
        """
        namespace JasperFx.MultiTenancy
        {
            public enum TenancyStyle { Single, Conjoined }
        }

        namespace Marten.Schema
        {
            public enum PrimaryKeyTenancyOrdering { Id_Then_TenantId, TenantId_Then_Id }
            public sealed class MultiTenantedAttribute : System.Attribute;
            public sealed class SingleTenantedAttribute : System.Attribute;
        }

        namespace Marten.Events
        {
            public interface IEventStoreOptions
            {
                JasperFx.MultiTenancy.TenancyStyle TenancyStyle { get; set; }
            }
        }

        namespace Marten
        {
            public interface IDocumentStore;

            public sealed class PartitioningExpression
            {
                public void ByHash(params string[] partitions) { }
            }

            public sealed class MartenRegistry
            {
                public DocumentMappingExpression<T> For<T>() => new();

                public sealed class DocumentMappingExpression<T>
                {
                    public DocumentMappingExpression<T> MultiTenanted() => this;
                    public DocumentMappingExpression<T> SingleTenanted() => this;
                    public DocumentMappingExpression<T> MultiTenantedWithPartitioning(System.Action<Marten.PartitioningExpression> configure) => this;
                }
            }

            public sealed class StoreOptions
            {
                public Marten.Events.IEventStoreOptions Events { get; } = null!;
                public MartenRegistry Schema { get; } = new();
                public PoliciesExpression Policies { get; } = new();

                public sealed class PoliciesExpression
                {
                    public PoliciesExpression AllDocumentsAreMultiTenanted() => this;
                    public PoliciesExpression AllDocumentsAreMultiTenantedWithPartitioning(System.Action<Marten.PartitioningExpression> configure) => this;
                    public PoliciesExpression AllDocumentsAreMultiTenantedWithPartitioning(System.Action<Marten.PartitioningExpression> configure, Marten.Schema.PrimaryKeyTenancyOrdering ordering) => this;
                }
            }
        }
        """;

    const string CurrentApplicationSource =
        """
        namespace Orders;

        public sealed class MultiDocument { public System.Guid Id { get; set; } }
        public sealed class SingleDocument { public System.Guid Id { get; set; } }
        public sealed class ConflictingDocument { public System.Guid Id { get; set; } }
        public sealed class PartitionedDocument { public System.Guid Id { get; set; } }

        [Marten.Schema.MultiTenanted]
        public sealed class AttributedMultiDocument { public System.Guid Id { get; set; } }

        [Marten.Schema.SingleTenanted]
        public sealed class AttributedSingleDocument { public System.Guid Id { get; set; } }

        public static class Configuration
        {
            public static void Configure(Marten.StoreOptions options)
            {
                const JasperFx.MultiTenancy.TenancyStyle conjoined = JasperFx.MultiTenancy.TenancyStyle.Conjoined;
                options.Events.TenancyStyle = JasperFx.MultiTenancy.TenancyStyle.Single;
                options.Events.TenancyStyle = conjoined;
                options.Events.TenancyStyle = JasperFx.MultiTenancy.TenancyStyle.Conjoined;
                options.Events.TenancyStyle = (JasperFx.MultiTenancy.TenancyStyle)99;
                var computed = BuildStyle();
                options.Events.TenancyStyle = computed;

                options.Schema.For<MultiDocument>().MultiTenanted();
                options.Schema.For<MultiDocument>().MultiTenanted();
                options.Schema.For<SingleDocument>().SingleTenanted();
                options.Schema.For<ConflictingDocument>().MultiTenanted();
                options.Schema.For<ConflictingDocument>().SingleTenanted();
                options.Schema.For<PartitionedDocument>().MultiTenantedWithPartitioning(partitioning =>
                    partitioning.ByHash("north", "south"));

                options.Policies.AllDocumentsAreMultiTenanted();
                options.Policies.AllDocumentsAreMultiTenanted();
                options.Policies.AllDocumentsAreMultiTenantedWithPartitioning(partitioning =>
                    partitioning.ByHash("east", "west"));
                options.Policies.AllDocumentsAreMultiTenantedWithPartitioning(
                    partitioning => partitioning.ByHash("one", "two"),
                    Marten.Schema.PrimaryKeyTenancyOrdering.TenantId_Then_Id);
            }

            static JasperFx.MultiTenancy.TenancyStyle BuildStyle() => JasperFx.MultiTenancy.TenancyStyle.Conjoined;
        }
        """;

    const string UnrelatedApplicationSource =
        """
        namespace Unrelated;

        public enum TenancyStyle { Single, Conjoined }

        public sealed class Options
        {
            public EventOptions Events { get; } = new();
            public SchemaExpression Schema { get; } = new();
            public PoliciesExpression Policies { get; } = new();
        }

        public sealed class EventOptions { public TenancyStyle TenancyStyle { get; set; } }
        public sealed class SchemaExpression { public Mapping<T> For<T>() => new(); }
        public sealed class Mapping<T>
        {
            public Mapping<T> MultiTenanted() => this;
            public Mapping<T> SingleTenanted() => this;
            public Mapping<T> MultiTenantedWithPartitioning(System.Action<object> configure) => this;
        }
        public sealed class PoliciesExpression
        {
            public void AllDocumentsAreMultiTenanted() { }
            public void AllDocumentsAreMultiTenantedWithPartitioning(System.Action<object> configure) { }
        }
        public sealed class MultiTenantedAttribute : System.Attribute;
        public sealed class SameNamedDocument { public System.Guid Id { get; set; } }

        public static class Configuration
        {
            public static void Configure(Options options)
            {
                options.Events.TenancyStyle = TenancyStyle.Conjoined;
                options.Schema.For<SameNamedDocument>().MultiTenanted();
                options.Schema.For<SameNamedDocument>().SingleTenanted();
                options.Schema.For<SameNamedDocument>().MultiTenantedWithPartitioning(_ => { });
                options.Policies.AllDocumentsAreMultiTenanted();
                options.Policies.AllDocumentsAreMultiTenantedWithPartitioning(_ => { });
            }
        }
        """;

    const string GeneratedApplicationSource =
        """
        // <auto-generated/>
        namespace Orders.Generated;

        [Marten.Schema.MultiTenanted]
        public sealed class GeneratedAttributedDocument { public System.Guid Id { get; set; } }
        public sealed class GeneratedDocument { public System.Guid Id { get; set; } }

        public static class Configuration
        {
            public static void Configure(Marten.StoreOptions options)
            {
                options.Events.TenancyStyle = JasperFx.MultiTenancy.TenancyStyle.Conjoined;
                options.Schema.For<GeneratedDocument>().MultiTenanted();
                options.Policies.AllDocumentsAreMultiTenanted();
            }
        }
        """;

    const string LegacyFrameworkSource =
        """
        namespace Marten.Storage
        {
            public enum TenancyStyle { Single, Conjoined, Separate }
        }

        namespace Marten.Events
        {
            public interface IEventStoreOptions
            {
                Marten.Storage.TenancyStyle TenancyStyle { get; set; }
            }
        }

        namespace Marten
        {
            public interface IDocumentStore;
            public sealed class StoreOptions
            {
                public Marten.Events.IEventStoreOptions Events { get; } = null!;
            }
        }
        """;

    const string LegacyApplicationSource =
        """
        namespace LegacyOrders;

        public static class Configuration
        {
            public static void Configure(Marten.StoreOptions options)
            {
                const Marten.Storage.TenancyStyle single = Marten.Storage.TenancyStyle.Single;
                options.Events.TenancyStyle = single;
                options.Events.TenancyStyle = Marten.Storage.TenancyStyle.Conjoined;
                options.Events.TenancyStyle = Marten.Storage.TenancyStyle.Separate;
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
        var currentFrameworkTree = CSharpSyntaxTree.ParseText(CurrentFrameworkSource, path: "/workspace/Framework.cs");
        var currentTree = CSharpSyntaxTree.ParseText(CurrentApplicationSource, path: "/workspace/Orders/Tenancy.cs");
        var unrelatedTree = CSharpSyntaxTree.ParseText(UnrelatedApplicationSource, path: "/workspace/Unrelated/Tenancy.cs");
        var generatedTree = CSharpSyntaxTree.ParseText(GeneratedApplicationSource, path: "/workspace/Orders/GeneratedTenancy.g.cs");
        var currentCompilation = CSharpCompilation.Create(
            "Orders",
            [currentFrameworkTree, currentTree, unrelatedTree, generatedTree],
            _references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, nullableContextOptions: NullableContextOptions.Enable));
        currentCompilation.GetDiagnostics().Where(_ => _.Severity == DiagnosticSeverity.Error).ShouldBeEmpty();

        var legacyFrameworkTree = CSharpSyntaxTree.ParseText(LegacyFrameworkSource, path: "/workspace/LegacyFramework.cs");
        var legacyTree = CSharpSyntaxTree.ParseText(LegacyApplicationSource, path: "/workspace/LegacyOrders/Tenancy.cs");
        var legacyCompilation = CSharpCompilation.Create(
            "LegacyOrders",
            [legacyFrameworkTree, legacyTree],
            _references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, nullableContextOptions: NullableContextOptions.Enable));
        legacyCompilation.GetDiagnostics().Where(_ => _.Severity == DiagnosticSeverity.Error).ShouldBeEmpty();

        var currentProject = Project(
            "Orders",
            "/workspace/Orders/Orders.csproj",
            currentCompilation,
            currentFrameworkTree,
            currentTree,
            unrelatedTree,
            generatedTree);
        var legacyProject = Project(
            "LegacyOrders",
            "/workspace/LegacyOrders/LegacyOrders.csproj",
            legacyCompilation,
            legacyFrameworkTree,
            legacyTree);
        Contribution = new CritterStackScreenplayAdapter().Analyze(
            new DotNetAnalysisContext([currentProject, legacyProject]),
            new DotNetAdapterOptions());
        Graph = new GenerationResolver().Resolve([Contribution]);
    }

    static DotNetProjectCompilation Project(
        string name,
        string path,
        Compilation compilation,
        params SyntaxTree[] authoredSyntaxTrees) => new()
    {
        Name = name,
        ProjectPath = path,
        SourceRoot = "/workspace",
        Compilation = compilation,
        AuthoredSyntaxTrees = authoredSyntaxTrees.ToHashSet()
    };
}
