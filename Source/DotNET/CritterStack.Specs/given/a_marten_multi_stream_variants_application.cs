// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Cratis.CritterStack.Screenplay.given;

/// <summary>
/// Multi-stream grouping declarations in alternative syntactic forms: indirect and JasperFx bases, expression-bodied
/// and multiple (partial) constructors, nested and conditional configuration, lambda shapes, wrapper paths, tenant
/// paths, fan-out modes and competing identities for one event type, spread over two files declared in reverse order.
/// </summary>
public class a_marten_multi_stream_variants_application : Specification
{
    const string FrameworkSource =
        """
        namespace Marten
        {
            public interface IDocumentStore;
            public class StoreOptions;
        }

        namespace JasperFx.Events
        {
            public interface IEvent
            {
                string TenantId { get; }
            }

            public interface IEvent<T>
            {
                T Data { get; }
                string TenantId { get; }
            }
        }

        namespace JasperFx.Events.Projections
        {
            public interface IProjection;
            public enum FanoutMode { BeforeGrouping, AfterGrouping, Sideways }
        }

        namespace JasperFx.Events.Aggregation
        {
            public enum TenancyGrouping { RespectTenant, AcrossTenants, RollUpByTenant }

            public abstract class JasperFxMultiStreamProjectionBase<TDoc, TId, TOperations, TQuerySession> : JasperFx.Events.Projections.IProjection
            {
                public TenancyGrouping TenancyGrouping { get; set; }
                public string Label { get; set; } = string.Empty;

                protected void Identity<TEvent>(System.Func<TEvent, TId> selector) { }
                protected void Identities<TEvent>(System.Func<TEvent, System.Collections.Generic.IReadOnlyList<TId>> selector) { }
                protected void FanOut<TEvent, TChild>(System.Func<TEvent, System.Collections.Generic.IEnumerable<TChild>> selector, JasperFx.Events.Projections.FanoutMode mode = JasperFx.Events.Projections.FanoutMode.AfterGrouping) { }
                protected void CustomGrouping(object grouper) { }
                protected void RollUpByTenant() { }
            }
        }

        namespace Marten.Events.Projections
        {
            public abstract class MultiStreamProjection<T, TId> : JasperFx.Events.Aggregation.JasperFxMultiStreamProjectionBase<T, TId, object, object>
            {
                protected void CustomGrouping(Marten.Events.Aggregation.IAggregateGrouper<TId> grouper) { }
            }
        }

        namespace Marten.Events.Aggregation
        {
            public interface IAggregateGrouper<TId>;
        }
        """;

    const string LaterFileSource =
        """
        using System.Collections.Generic;

        namespace Orders;

        public record Assigned(System.Guid CustomerId, Customer Customer, System.Guid TenantId);
        public record Shared(IReadOnlyList<System.Guid> CustomerIds);
        public record Imported(Line[] Lines, Line[] Other);
        public record Customer(System.Guid Id);
        public record Line(string Sku);
        public record Ignored(System.Guid CustomerId);

        public class CustomerOrders
        {
            public System.Guid Id { get; set; }
        }

        public class IntermediateBase : Marten.Events.Projections.MultiStreamProjection<CustomerOrders, System.Guid>;

        public sealed class IndirectProjection : IntermediateBase
        {
            public IndirectProjection()
            {
                Identity<Assigned>(x => x.CustomerId);
            }
        }

        public sealed class JasperFxDirectProjection : JasperFx.Events.Aggregation.JasperFxMultiStreamProjectionBase<CustomerOrders, System.Guid, object, object>
        {
            public JasperFxDirectProjection()
            {
                Identity<Assigned>(x => x.CustomerId);
                RollUpByTenant();
            }
        }

        public sealed class ExpressionBodiedProjection : Marten.Events.Projections.MultiStreamProjection<CustomerOrders, System.Guid>
        {
            public ExpressionBodiedProjection() => Identity<Assigned>(x => x.CustomerId);
        }

        public sealed class LambdaShapesProjection : Marten.Events.Projections.MultiStreamProjection<CustomerOrders, System.Guid>
        {
            public LambdaShapesProjection()
            {
                Identity<Assigned>((x) => x.CustomerId);
                Identity<Assigned>(x => (x.CustomerId));
                Identity<Assigned>(x => x.Customer.Id);
                Identity<Assigned>((Assigned x) => x.CustomerId);
                Identity<Assigned>(static x => x.CustomerId);
                Identity<Assigned>(x => x.CustomerId);
                Identity<Assigned>(x => x.Customer.Id);
                Identities<Shared>(x => x.CustomerIds);
                Identities<Shared>(x => x.CustomerIds.Count == 0 ? x.CustomerIds : x.CustomerIds);
                Identity<Assigned>(selector: x => x.CustomerId);
                Identity<Assigned>(Pick);
                Identity<Assigned>(x => Pick(x));
                Identity<Assigned>(x => x.Customer.Id.ToString().Length == 0 ? System.Guid.Empty : x.CustomerId);
                Identity<Assigned>(x => this.Fixed);
                Identity<Assigned>(x => Fixed);
            }

            System.Guid Fixed => System.Guid.Empty;

            static System.Guid Pick(Assigned value) => value.CustomerId;
        }

        public sealed class TenantPathProjection : Marten.Events.Projections.MultiStreamProjection<CustomerOrders, System.Guid>
        {
            public TenantPathProjection()
            {
                Identity<Assigned>(x => x.TenantId);
                Identity<Assigned>(x => x.Customer.Id);
                Identity<Wrapped>(x => x.TenantId);
            }
        }

        public record Wrapped(System.Guid CustomerId, System.Guid TenantId);

        public sealed class WrapperProjection : Marten.Events.Projections.MultiStreamProjection<CustomerOrders, System.Guid>
        {
            public WrapperProjection()
            {
                Identity<JasperFx.Events.IEvent<Wrapped>>(x => x.Data.CustomerId);
                Identity<JasperFx.Events.IEvent<Wrapped>>(x => x.Data.TenantId);
                Identity<JasperFx.Events.IEvent<Assigned>>(x => x.Data.Customer.Id);
                FanOut<JasperFx.Events.IEvent<Imported>, Line>(x => x.Data.Lines);
                FanOut<JasperFx.Events.IEvent<Imported>, Line>(x => x.Data.Other, JasperFx.Events.Projections.FanoutMode.BeforeGrouping);
            }
        }

        public sealed class FanOutModesProjection : Marten.Events.Projections.MultiStreamProjection<CustomerOrders, System.Guid>
        {
            public FanOutModesProjection()
            {
                FanOut<Imported, Line>(x => x.Lines, JasperFx.Events.Projections.FanoutMode.AfterGrouping);
                FanOut<Imported, Line>(x => x.Other, JasperFx.Events.Projections.FanoutMode.BeforeGrouping);
                FanOut<Imported, Line>(x => x.Lines, JasperFx.Events.Projections.FanoutMode.Sideways);
                FanOut<Imported, Line>(x => x.Lines, (JasperFx.Events.Projections.FanoutMode)1);
                FanOut<Imported, Line>(x => x.Lines, Computed());
                FanOut<Imported, Line>(x => x.Lines, mode: JasperFx.Events.Projections.FanoutMode.BeforeGrouping);
                FanOut<Imported, Line>(Lines);
                FanOut<Imported, Line>(x => x.Lines.Length == 0 ? x.Lines : x.Other);
                FanOut<Imported, Line>(x => x.Other);
            }

            static JasperFx.Events.Projections.FanoutMode Computed() => JasperFx.Events.Projections.FanoutMode.AfterGrouping;

            static System.Collections.Generic.IEnumerable<Line> Lines(Imported value) => value.Lines;
        }
        """;

    const string EarlierFileSource =
        """
        namespace Orders;

        public sealed partial class CompetingProjection : Marten.Events.Projections.MultiStreamProjection<CustomerOrders, System.Guid>
        {
            public CompetingProjection()
            {
                Identity<Assigned>(x => x.Customer.Id);
                Identity<Assigned>(x => x.CustomerId);
                Identity<Assigned>(x => x.CustomerId);
                Identities<Shared>(x => x.CustomerIds);
                Identity<Shared>(x => x.CustomerIds[0]);
                Identity<Ignored>(x => x.CustomerId);
                FanOut<Imported, Line>(x => x.Other);
                FanOut<Imported, Line>(x => x.Lines);
                Identity<Assigned>(x => x.CustomerId);
            }
        }

        public sealed partial class CompetingProjection
        {
            public CompetingProjection(bool enabled)
            {
                if (enabled)
                {
                    Identity<Ignored>(x => x.CustomerId);
                    RollUpByTenant();
                    TenancyGrouping = JasperFx.Events.Aggregation.TenancyGrouping.AcrossTenants;
                }

                while (enabled)
                {
                    FanOut<Imported, Line>(x => x.Lines);
                    enabled = false;
                }

                var local = new System.Action(() => Identity<Ignored>(x => x.CustomerId));
                local();
                try
                {
                    CustomGrouping(new Grouper());
                }
                catch (System.Exception)
                {
                }

                Label = "competing";
                TenancyGrouping = JasperFx.Events.Aggregation.TenancyGrouping.RespectTenant;
                CustomGrouping(new Grouper());
                RollUpByTenant();
            }
        }

        public sealed class Grouper : Marten.Events.Aggregation.IAggregateGrouper<System.Guid>;

        public sealed class LocalFunctionProjection : Marten.Events.Projections.MultiStreamProjection<CustomerOrders, System.Guid>
        {
            public LocalFunctionProjection()
            {
                void Configure() => Identity<Assigned>(x => x.CustomerId);
                Configure();
                var value = Identity2();
                _ = value;
                System.Guid Identity2() => System.Guid.Empty;
                { Identity<Ignored>(x => x.CustomerId); }
            }

            public void Method() => Identity<Ignored>(x => x.CustomerId);
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
        var compilation = CSharpCompilation.Create(
            "Orders",
            [frameworkTree, laterTree, earlierTree],
            _references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, nullableContextOptions: NullableContextOptions.Enable));
        compilation.GetDiagnostics().Where(_ => _.Severity == DiagnosticSeverity.Error).ShouldBeEmpty();
        var project = new DotNetProjectCompilation
        {
            Name = "Orders",
            ProjectPath = "/workspace/Orders/Orders.csproj",
            SourceRoot = "/workspace",
            Compilation = compilation,
            AuthoredSyntaxTrees = new HashSet<SyntaxTree> { frameworkTree, laterTree, earlierTree }
        };
        VariantContribution = new CritterStackScreenplayAdapter().Analyze(
            new DotNetAnalysisContext([project]),
            new DotNetAdapterOptions());
    }
}
