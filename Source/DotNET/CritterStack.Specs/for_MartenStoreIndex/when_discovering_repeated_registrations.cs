// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Cratis.CritterStack.Screenplay.Marten;

namespace Cratis.CritterStack.Screenplay.for_MartenStoreIndex;

public class when_discovering_repeated_registrations : given.a_marten_store_scope_application
{
    const string Source =
        """
        using Marten;
        namespace StoreScope;
        public interface IAccounts : IDocumentStore;
        public static class Configuration
        {
            public static void Configure(Microsoft.Extensions.DependencyInjection.IServiceCollection services)
            {
                services.AddMartenStore<IAccounts>(options => options.EventModelName = "Orders");
                services.AddMartenStore<IAccounts>(options => options.EventModelName = "Orders");
            }
        }
        """;

    MartenStoreIndex _index = null!;
    BackendScopeFact[] _declarations = [];

    void Because()
    {
        var context = new DotNetAnalysisContext([ProjectFor(Source)]);
        _index = MartenStoreIndex.Create(context, new() { Id = "test", Version = "1" }, new(context, context));
        _declarations = [.. _index.Facts.OfType<BackendScopeFact>()];
    }

    [Fact] void should_keep_two_registration_sites() => _declarations.Length.ShouldEqual(2);
    [Fact] void should_not_merge_equal_qualifiers() => _declarations.Select(fact => fact.Definition.Key.Store).Distinct().Count().ShouldEqual(2);
    [Fact] void should_preserve_case() => _declarations.All(fact => fact.Definition.Key.EventModelName == "Orders").ShouldBeTrue();
    [Fact] void should_use_located_declarations() => _declarations.All(fact => fact.Evidence.Source is not null && fact.Subject == fact.Definition.Key.Store).ShouldBeTrue();
    [Fact] void should_not_treat_ancillary_as_default() => _index.Default().Kind.ShouldEqual(BackendOwnershipKind.Unknown);
    [Fact] void should_not_report_proved_registration_as_unsupported() => _index.Diagnostics.ShouldBeEmpty();
}
