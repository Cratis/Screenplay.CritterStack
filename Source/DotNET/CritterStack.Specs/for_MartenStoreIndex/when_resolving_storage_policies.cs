// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Cratis.CritterStack.Screenplay.Marten;
using Cratis.CritterStack.Screenplay.Wolverine;

namespace Cratis.CritterStack.Screenplay.for_MartenStoreIndex;

public class when_resolving_storage_policies : given.a_marten_store_scope_application
{
    const string Source =
        """
        using Marten;
        namespace StoreScope;
        public interface IAccounts : IDocumentStore;
        public interface IBilling : IDocumentStore;
        public interface IUnregistered : IDocumentStore;
        public class Handler { public void Handle() { } }
        public static class Configuration
        {
            public static void Configure(Microsoft.Extensions.DependencyInjection.IServiceCollection services, Wolverine.IPolicies policies)
            {
                services.AddMartenStore<IAccounts>(options => options.EventModelName = "Accounts");
                services.AddMartenStore<IBilling>(options => options.EventModelName = "Billing");
                POLICY
            }
        }
        """;

    static BackendOwnership Resolve(string policy)
    {
        var project = ProjectFor(Source.Replace("POLICY", policy, StringComparison.Ordinal));
        project.Compilation.GetDiagnostics(TestContext.Current.CancellationToken).Where(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error).ShouldBeEmpty();
        var context = new DotNetAnalysisContext([project]);
        var stores = MartenStoreIndex.Create(context, new() { Id = "test", Version = "1" }, new(context, context));
        var routing = WolverineStorageRouting.Discover(context, stores);
        return routing.ForHandler(project.Compilation.GetTypeByMetadataName("StoreScope.Handler")) ??
            throw new InvalidOperationException("Expected a matching storage policy");
    }

    [Fact] void should_use_namespace_containing() => Resolve("policies.UseAncillaryStorageFromNamespaceContaining<Handler>(typeof(IAccounts));").Scope.EventModelName.ShouldEqual("Accounts");
    [Fact] void should_use_a_literal_namespace() => Resolve("policies.UseAncillaryStorageFromNamespace(\"StoreScope\", typeof(IBilling));").Scope.EventModelName.ShouldEqual("Billing");
    [Fact] void should_use_assembly_containing() => Resolve("policies.UseAncillaryStorageFromAssemblyContaining<Handler>(typeof(IAccounts));").Scope.EventModelName.ShouldEqual("Accounts");
    [Fact] void should_use_typeof_assembly() => Resolve("policies.UseAncillaryStorageFromAssembly(typeof(Handler).Assembly, typeof(IBilling));").Scope.EventModelName.ShouldEqual("Billing");
    [Fact] void should_honor_first_applicable_policy() => Resolve("policies.UseAncillaryStorageFromNamespaceContaining<Handler>(typeof(IAccounts)); policies.UseAncillaryStorageFromAssemblyContaining<Handler>(typeof(IBilling));").Scope.EventModelName.ShouldEqual("Accounts");
    [Fact] void should_not_order_policies_on_independent_receivers() => Resolve("var second = new Wolverine.IPolicies(); policies.UseAncillaryStorageFromNamespaceContaining<Handler>(typeof(IAccounts)); second.UseAncillaryStorageFromNamespaceContaining<Handler>(typeof(IBilling));").Kind.ShouldEqual(BackendOwnershipKind.Conflict);
    [Fact] void should_reject_unknown_store_ownership_across_configuration_blocks() => Resolve("System.Action<Wolverine.IPolicies> first = p => { p.UseAncillaryStorageFromNamespaceContaining<Handler>(typeof(IUnregistered)); }; System.Action<Wolverine.IPolicies> second = p => { p.UseAncillaryStorageFromNamespaceContaining<Handler>(typeof(IAccounts)); };").Kind.ShouldEqual(BackendOwnershipKind.Unknown);
    [Fact] void should_reject_unknown_store_ownership_regardless_of_block_order() => Resolve("System.Action<Wolverine.IPolicies> first = p => { p.UseAncillaryStorageFromNamespaceContaining<Handler>(typeof(IAccounts)); }; System.Action<Wolverine.IPolicies> second = p => { p.UseAncillaryStorageFromNamespaceContaining<Handler>(typeof(IUnregistered)); };").Kind.ShouldEqual(BackendOwnershipKind.Unknown);
    [Fact] void should_not_override_a_computed_store_marker_with_known_ownership() => Resolve("policies.UseAncillaryStorageFromNamespaceContaining<Handler>(typeof(IAccounts)); policies.UseAncillaryStorageFromNamespaceContaining<Handler>(System.DateTime.Now.Ticks > 0 ? typeof(IAccounts) : typeof(IBilling));").Kind.ShouldEqual(BackendOwnershipKind.Unknown);
    [Fact] void should_not_guess_a_dynamic_namespace() => Resolve("policies.UseAncillaryStorageFromNamespace(System.DateTime.Now.ToString(), typeof(IAccounts));").Kind.ShouldEqual(BackendOwnershipKind.Unknown);
    [Fact] void should_not_trust_conditional_policy_order() => Resolve("if (System.DateTime.Now.Ticks > 0) policies.UseAncillaryStorageFromNamespaceContaining<Handler>(typeof(IAccounts));").Kind.ShouldEqual(BackendOwnershipKind.Unknown);
}
