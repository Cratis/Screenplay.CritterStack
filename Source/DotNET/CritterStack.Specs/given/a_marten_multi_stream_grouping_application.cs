// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Cratis.CritterStack.Screenplay.given;

public class a_marten_multi_stream_grouping_application : Specification
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
            }
        }

        namespace JasperFx.Events.Grouping
        {
            public interface IEventSlicer<TDoc, TId>;
        }

        namespace JasperFx.Events.Projections
        {
            public interface IProjection;
            public enum FanoutMode { BeforeGrouping, AfterGrouping }
        }

        namespace JasperFx.Events.Aggregation
        {
            public enum TenancyGrouping { RespectTenant, AcrossTenants, RollUpByTenant }

            public abstract class JasperFxMultiStreamProjectionBase<TDoc, TId, TOperations, TQuerySession> : JasperFx.Events.Projections.IProjection
            {
                public TenancyGrouping TenancyGrouping { get; set; }

                protected void Identity<TEvent>(System.Func<TEvent, TId> selector) { }
                protected void Identities<TEvent>(System.Func<TEvent, System.Collections.Generic.IReadOnlyList<TId>> selector) { }
                protected void FanOut<TEvent, TChild>(System.Func<TEvent, System.Collections.Generic.IEnumerable<TChild>> selector, JasperFx.Events.Projections.FanoutMode mode = JasperFx.Events.Projections.FanoutMode.AfterGrouping) { }
                protected void FanOut<TEvent, TChild>(System.Func<JasperFx.Events.IEvent<TEvent>, System.Collections.Generic.IEnumerable<TChild>> selector, JasperFx.Events.Projections.FanoutMode mode = JasperFx.Events.Projections.FanoutMode.AfterGrouping) { }
                protected void CustomGrouping(object grouper) { }
                protected void RollUpByTenant() { }
            }
        }

        namespace Marten.Events.Aggregation
        {
            public interface IAggregateGrouper<TId>;
        }

        namespace Marten.Events.Projections
        {
            public abstract class MultiStreamProjection<T, TId> : JasperFx.Events.Aggregation.JasperFxMultiStreamProjectionBase<T, TId, object, object>
            {
                protected void CustomGrouping(Marten.Events.Aggregation.IAggregateGrouper<TId> grouper) { }
                protected void CustomGrouping(JasperFx.Events.Grouping.IEventSlicer<T, TId> slicer) { }
            }
        }
        """;

    const string ApplicationSource =
        """
        using System.Linq;

        namespace Orders;

        public record CustomerAssigned(System.Guid CustomerId);
        public record CustomersShared(System.Collections.Generic.IReadOnlyList<System.Guid> CustomerIds);
        public record OrderImported(LineImported[] Lines);
        public record RouteImported(StopImported[] Stops);
        public record LineImported(string Sku);
        public record StopImported(string Name);
        public record ComputedIdentity(System.Guid CustomerId, bool IsPrimary, System.Guid AlternateId);
        public record ComputedFanOut(LineImported[] Lines);
        public record ConditionalIdentity(System.Guid CustomerId);
        public record TenantEvent(System.Guid TenantId);

        public class CustomerOrders
        {
            public System.Guid Id { get; set; }
        }

        public partial class CustomerOrdersProjection : Marten.Events.Projections.MultiStreamProjection<CustomerOrders, System.Guid>
        {
            public CustomerOrdersProjection()
            {
                Identity<CustomerAssigned>(x => x.CustomerId);
                Identities<CustomersShared>(x => x.CustomerIds);
                FanOut<OrderImported, LineImported>(x => x.Lines);
                FanOut<RouteImported, StopImported>(x => x.Data.Stops, JasperFx.Events.Projections.FanoutMode.BeforeGrouping);
            }

            public void Apply(CustomerOrders orders, CustomerAssigned e) { }
            public void Apply(CustomerOrders orders, CustomersShared e) { }
            public void Apply(CustomerOrders orders, LineImported e) { }
            public void Apply(CustomerOrders orders, StopImported e) { }
        }

        public partial class UnsafeSelectorsProjection : Marten.Events.Projections.MultiStreamProjection<CustomerOrders, System.Guid>
        {
            public UnsafeSelectorsProjection()
            {
                Identity<ComputedIdentity>(x => Normalize(x.CustomerId));
                Identities<CustomersShared>(x => [.. x.CustomerIds]);
                FanOut<ComputedFanOut, LineImported>(x => x.Lines.Where(_ => true));
                Identity<ComputedIdentity>(x => x.IsPrimary ? x.CustomerId : x.AlternateId);
                Identity<ComputedIdentity>(x => { return x.CustomerId; });
            }

            static System.Guid Normalize(System.Guid value) => value;
        }

        public class Grouper : Marten.Events.Aggregation.IAggregateGrouper<System.Guid>;
        public class Slicer : JasperFx.Events.Grouping.IEventSlicer<CustomerOrders, System.Guid>;

        public partial class ArbitraryGroupingProjection : Marten.Events.Projections.MultiStreamProjection<CustomerOrders, System.Guid>
        {
            public ArbitraryGroupingProjection()
            {
                CustomGrouping(new Grouper());
                CustomGrouping(new Slicer());
            }
        }

        public partial class TenantGroupingProjection : Marten.Events.Projections.MultiStreamProjection<CustomerOrders, System.Guid>
        {
            public TenantGroupingProjection()
            {
                TenancyGrouping = JasperFx.Events.Aggregation.TenancyGrouping.AcrossTenants;
                RollUpByTenant();
                Identity<TenantEvent>(x => x.TenantId);
            }
        }

        public partial class ConditionalGroupingProjection : Marten.Events.Projections.MultiStreamProjection<CustomerOrders, System.Guid>
        {
            public ConditionalGroupingProjection(bool enabled)
            {
                if (enabled)
                {
                    Identity<ConditionalIdentity>(x => x.CustomerId);
                }
            }
        }

        public partial class UnrelatedIdentityProjection : Marten.Events.Projections.MultiStreamProjection<CustomerOrders, System.Guid>
        {
            public UnrelatedIdentityProjection()
            {
                Identity<ConditionalIdentity>(x => x.CustomerId);
            }

            new void Identity<TEvent>(System.Func<TEvent, System.Guid> selector) { }
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
        var compilation = CSharpCompilation.Create(
            "Orders",
            [
                CSharpSyntaxTree.ParseText(FrameworkSource, path: "/workspace/Framework.cs"),
                CSharpSyntaxTree.ParseText(ApplicationSource, path: "/workspace/Orders/Projections.cs")
            ],
            _references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, nullableContextOptions: NullableContextOptions.Enable));
        var errors = compilation.GetDiagnostics().Where(_ => _.Severity == DiagnosticSeverity.Error).ToArray();
        errors.ShouldBeEmpty();
        var project = new DotNetProjectCompilation
        {
            Name = "Orders",
            ProjectPath = "/workspace/Orders/Orders.csproj",
            SourceRoot = "/workspace",
            Compilation = compilation
        };
        Contribution = new CritterStackScreenplayAdapter().Analyze(
            new DotNetAnalysisContext([project]),
            new DotNetAdapterOptions());
        Graph = new GenerationResolver().Resolve([Contribution]);
    }
}
