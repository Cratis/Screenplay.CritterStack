// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Cratis.CritterStack.Screenplay.Marten;

namespace Cratis.CritterStack.Screenplay.for_MartenStoreIndex;

public class when_discovering_ownership_for_separate_runs : given.a_marten_store_scope_application
{
    const string Source =
        """
        using Marten;
        namespace StoreScope;
        public interface IAccounts : IDocumentStore;
        public static class Configuration
        {
            public static void Configure(Microsoft.Extensions.DependencyInjection.IServiceCollection services) =>
                services.AddMartenStore<IAccounts>(options => options.EventModelName = "Orders");
        }
        """;

    (MartenStoreIndex Stores, MartenBackendOwnershipResolver Ownership) _first;
    (MartenStoreIndex Stores, MartenBackendOwnershipResolver Ownership) _second;

    void Because()
    {
        var context = new DotNetAnalysisContext([ProjectFor(Source)]);
        var adapter = new AdapterIdentity { Id = "test", Version = "1" };
        _first = MartenStoreScopeDiscovery.DiscoverOwnership(context, adapter, new(context, context));
        _second = MartenStoreScopeDiscovery.DiscoverOwnership(context, adapter, new(context, context));
    }

    [Fact] void should_not_share_the_store_index_between_runs() => _first.Stores.ShouldNotBeSame(_second.Stores);
    [Fact] void should_not_share_the_ownership_resolver_between_runs() => _first.Ownership.ShouldNotBeSame(_second.Ownership);
    [Fact] void should_produce_equal_scope_declarations_for_equal_input() => _first.Stores.Facts.Select(fact => fact.Id.Value).ShouldEqualInOrder(_second.Stores.Facts.Select(fact => fact.Id.Value));
}
