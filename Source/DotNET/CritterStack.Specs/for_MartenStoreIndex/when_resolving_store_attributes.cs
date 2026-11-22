// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Cratis.CritterStack.Screenplay.Marten;
using Cratis.CritterStack.Screenplay.Wolverine;

namespace Cratis.CritterStack.Screenplay.for_MartenStoreIndex;

public class when_resolving_store_attributes : given.a_marten_store_scope_application
{
    const string Source =
        """
        using Marten;
        namespace StoreScope;
        public interface IAccounts : IDocumentStore;
        public interface IBilling : IDocumentStore;
        public interface IMissing : IDocumentStore;
        public static class Configuration
        {
            public static void Configure(Microsoft.Extensions.DependencyInjection.IServiceCollection services)
            {
                services.AddMartenStore<IAccounts>(options => options.EventModelName = "Accounts");
                services.AddMartenStore<IBilling>(options => options.EventModelName = "Billing");
            }
        }
        [Wolverine.Marten.MartenStore(typeof(IAccounts))]
        public class Handler
        {
            public void Inherited() { }
            [Wolverine.Marten.MartenStore(typeof(IBilling))] public void Override() { }
            [Wolverine.Persistence.Storage(typeof(IBilling))] public void StorageOverride() { }
            [Wolverine.Marten.MartenStore(typeof(IAccounts)), Wolverine.Persistence.Storage(typeof(IBilling))] public void Conflict() { }
            [Wolverine.Persistence.Storage(typeof(IMissing))] public void Missing() { }
        }
        public class Unselected { public void Handle() { } }
        public class DirectSession { public void Handle(Marten.Ancillary.IDocumentSession<IBilling> session) { } }
        """;

    Dictionary<string, BackendOwnership> _ownership = [];

    void Because()
    {
        var project = ProjectFor(Source);
        var context = new DotNetAnalysisContext([project]);
        var subjects = new CritterStackSubjectResolver(context, context);
        var stores = MartenStoreIndex.Create(context, new() { Id = "test", Version = "1" }, subjects);
        var resolver = new MartenBackendOwnershipResolver(stores, WolverineStorageRouting.Discover(context, stores));
        _ownership = new[] { "Handler", "Unselected", "DirectSession" }
            .SelectMany(name => project.Compilation.GetTypeByMetadataName("StoreScope." + name).GetMembers().OfType<IMethodSymbol>())
            .Where(method => method.MethodKind == MethodKind.Ordinary)
            .ToDictionary(method => method.ContainingType.Name + "." + method.Name, method => resolver.ForMethod(project, method));
    }

    [Fact] void should_inherit_the_type_selection() => _ownership["Handler.Inherited"].Scope.EventModelName.ShouldEqual("Accounts");
    [Fact] void should_prefer_the_method_selection() => _ownership["Handler.Override"].Scope.EventModelName.ShouldEqual("Billing");
    [Fact] void should_honor_the_general_storage_attribute() => _ownership["Handler.StorageOverride"].Scope.EventModelName.ShouldEqual("Billing");
    [Fact] void should_not_coalesce_conflicting_selections() => _ownership["Handler.Conflict"].Kind.ShouldEqual(BackendOwnershipKind.Conflict);
    [Fact] void should_not_fall_back_from_an_unregistered_marker() => _ownership["Handler.Missing"].Kind.ShouldEqual(BackendOwnershipKind.Unknown);
    [Fact] void should_not_treat_an_ancillary_registration_as_default() => _ownership["Unselected.Handle"].Kind.ShouldEqual(BackendOwnershipKind.Unknown);
    [Fact] void should_not_move_a_handler_chain_for_an_injected_ancillary_session() => _ownership["DirectSession.Handle"].Kind.ShouldEqual(BackendOwnershipKind.Unknown);
    [Fact] void should_use_valid_framework_signatures() => ProjectFor(Source).Compilation.GetDiagnostics(TestContext.Current.CancellationToken).Where(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error).ShouldBeEmpty();
}
