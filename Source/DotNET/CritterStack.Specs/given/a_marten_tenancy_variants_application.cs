// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Cratis.CritterStack.Screenplay.given;

/// <summary>
/// Tenancy declarations in alternative syntactic forms across two authored files (declared in reverse path order), a
/// generated file and a non-authored file: event tenancy styles (named, aliased, constant chains, conditional,
/// compound), document tenancy methods and rejected overloads, project-wide policies and attributes on several targets.
/// </summary>
public class a_marten_tenancy_variants_application : Specification
{
    const string FrameworkSource =
        """
        namespace JasperFx.MultiTenancy
        {
            public enum TenancyStyle { Single, Conjoined, Separate }
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

            public sealed class ConcreteEventOptions : IEventStoreOptions
            {
                public JasperFx.MultiTenancy.TenancyStyle TenancyStyle { get; set; }
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
                    public DocumentMappingExpression<T> MultiTenanted(int ignored) => this;
                    public DocumentMappingExpression<T> MultiTenanted<TOther>() => this;
                    public DocumentMappingExpression<T> SingleTenanted() => this;
                    public DocumentMappingExpression<T> MultiTenantedWithPartitioning(System.Action<Marten.PartitioningExpression> configure) => this;
                    public DocumentMappingExpression<T> MultiTenantedWithPartitioning(System.Action<string> configure) => this;
                    public DocumentMappingExpression<T> MultiTenantedWithPartitioning(System.Action<Marten.PartitioningExpression> configure, int extra) => this;
                    public DocumentMappingExpression<T> Identity() => this;
                }

                public sealed class OtherMappingExpression<T>
                {
                    public OtherMappingExpression<T> MultiTenanted() => this;
                }

                public OtherMappingExpression<T> Other<T>() => new();
            }

            public sealed class StoreOptions
            {
                public Marten.Events.IEventStoreOptions Events { get; } = null!;
                public Marten.Events.ConcreteEventOptions ConcreteEvents { get; } = new();
                public MartenRegistry Schema { get; } = new();
                public PoliciesExpression Policies { get; } = new();

                public sealed class PoliciesExpression
                {
                    public PoliciesExpression AllDocumentsAreMultiTenanted() => this;
                    public PoliciesExpression AllDocumentsAreMultiTenanted(int ignored) => this;
                    public PoliciesExpression AllDocumentsAreMultiTenantedWithPartitioning(System.Action<Marten.PartitioningExpression> configure) => this;
                    public PoliciesExpression AllDocumentsAreMultiTenantedWithPartitioning(System.Action<Marten.PartitioningExpression> configure, Marten.Schema.PrimaryKeyTenancyOrdering ordering) => this;
                    public PoliciesExpression AllDocumentsAreMultiTenantedWithPartitioning(System.Action<Marten.PartitioningExpression> configure, int ordering) => this;
                    public PoliciesExpression AllDocumentsAreMultiTenantedWithPartitioning(System.Action<string> configure) => this;
                    public PoliciesExpression Other() => this;
                }
            }
        }
        """;

    const string LaterFileSource =
        """
        using Style = JasperFx.MultiTenancy.TenancyStyle;

        namespace Orders;

        public sealed class Zed { public System.Guid Id { get; set; } }
        public sealed class Yankee { public System.Guid Id { get; set; } }

        [Marten.Schema.MultiTenanted]
        public sealed class AttributedClass { }

        [Marten.Schema.SingleTenanted]
        public record AttributedRecord;

        [Marten.Schema.MultiTenanted]
        public struct AttributedStruct { }

        [Marten.Schema.SingleTenanted]
        public interface IAttributedInterface { }

        [Marten.Schema.MultiTenanted]
        public delegate void AttributedDelegate();

        [Marten.Schema.MultiTenanted]
        public enum AttributedEnum { One }

        public sealed class Holder
        {
            [Marten.Schema.SingleTenanted]
            public void Method() { }

            [Marten.Schema.MultiTenanted]
            public int Property { get; set; }

            [Marten.Schema.SingleTenanted]
            public enum NestedEnum { One }

            [Marten.Schema.MultiTenanted]
            public sealed class Nested { }
        }

        public static class LaterConfiguration
        {
            public const Style ConstantField = Style.Conjoined;
            public static readonly Style ReadonlyField = Style.Single;

            public static void Configure(Marten.StoreOptions options, bool flag, Style computedStyle)
            {
                const Style first = Style.Conjoined;
                const Style second = first;
                const Style third = second;
                options.Events.TenancyStyle = third;
                options.Events.TenancyStyle = ConstantField;
                options.Events.TenancyStyle = ReadonlyField;
                options.Events.TenancyStyle = flag ? Style.Single : Style.Conjoined;
                options.Events.TenancyStyle = (Style.Conjoined);
                options.Events.TenancyStyle = Style.Separate;
                options.Events.TenancyStyle = computedStyle;
                options.ConcreteEvents.TenancyStyle = Style.Conjoined;
                ((Marten.Events.IEventStoreOptions)options.ConcreteEvents).TenancyStyle = Style.Single;

                options.Schema.For<Zed>().MultiTenanted().SingleTenanted();
                options.Schema.For<Yankee>().MultiTenantedWithPartitioning(p => p.ByHash("a"));
                options.Schema.For<Yankee>().MultiTenantedWithPartitioning(p => { p.ByHash("b"); });
                options.Schema.For<Yankee>().MultiTenantedWithPartitioning((System.Action<Marten.PartitioningExpression>)(p => p.ByHash("c")));
                options.Schema.For<Yankee>().MultiTenantedWithPartitioning((string p) => { });
                options.Schema.For<Yankee>().MultiTenantedWithPartitioning(p => p.ByHash("d"), 1);
                options.Schema.For<Yankee>().MultiTenanted(1);
                options.Schema.For<Yankee>().MultiTenanted<int>();
                options.Schema.For<Yankee>().Identity();
                options.Schema.Other<Yankee>().MultiTenanted();

                options.Policies.AllDocumentsAreMultiTenanted(1);
                options.Policies.AllDocumentsAreMultiTenantedWithPartitioning(p => p.ByHash("e"), 1);
                options.Policies.AllDocumentsAreMultiTenantedWithPartitioning(
                    p => p.ByHash("f"),
                    Marten.Schema.PrimaryKeyTenancyOrdering.Id_Then_TenantId);
                options.Policies.AllDocumentsAreMultiTenantedWithPartitioning((string p) => { });
                options.Policies.Other();
            }
        }
        """;

    const string EarlierFileSource =
        """
        [assembly: Marten.Schema.MultiTenanted]

        namespace Orders;

        public static class EarlierConfiguration
        {
            public static void Configure(Marten.StoreOptions options)
            {
                options.Events.TenancyStyle = JasperFx.MultiTenancy.TenancyStyle.Conjoined;
                options.Schema.For<Yankee>().SingleTenanted();
                options.Policies.AllDocumentsAreMultiTenanted();
                options.Schema.For<Yankee>().MultiTenanted();
            }

            public static Marten.StoreOptions.PoliciesExpression Expression(Marten.StoreOptions options) =>
                options.Policies.AllDocumentsAreMultiTenantedWithPartitioning(p => p.ByHash("g"));
        }
        """;

    const string GeneratedFileSource =
        """
        // <auto-generated/>
        namespace Orders;

        [Marten.Schema.MultiTenanted]
        public sealed class GeneratedDocument { }

        public static class GeneratedConfiguration
        {
            public static void Configure(Marten.StoreOptions options)
            {
                options.Events.TenancyStyle = JasperFx.MultiTenancy.TenancyStyle.Separate;
                options.Policies.AllDocumentsAreMultiTenanted();
            }
        }
        """;

    const string NonAuthoredFileSource =
        """
        namespace Orders;

        public static class NonAuthoredConfiguration
        {
            public static void Configure(Marten.StoreOptions options)
            {
                options.Events.TenancyStyle = JasperFx.MultiTenancy.TenancyStyle.Single;
                options.Policies.AllDocumentsAreMultiTenanted();
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
