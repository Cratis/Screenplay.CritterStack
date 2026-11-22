// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Cratis.CritterStack.Screenplay.Marten;

namespace Cratis.CritterStack.Screenplay.for_MartenStoreIndex;

public class when_binding_qualifiers_to_registered_options : given.a_marten_store_scope_application
{
    const string Source =
        """
        using Marten;
        namespace StoreScope;
        public interface IBilling : IDocumentStore;
        public static class Configuration
        {
            public static void Configure(Microsoft.Extensions.DependencyInjection.IServiceCollection services)
            {
                REGISTRATION
            }
        }
        """;

    MartenStoreIndex _detached = null!;
    MartenStoreIndex _nestedCallback = null!;

    void Because()
    {
        _detached = Index("services.AddMartenStore<IBilling>(options => { var other = new StoreOptions { EventModelName = \"Detached\" }; });");
        _nestedCallback = Index("services.AddMartenStore<IBilling>(options => { System.Action<StoreOptions> detached = other => other.EventModelName = \"Detached\"; });");
    }

    [Fact] void should_leave_the_registered_store_unnamed_for_a_detached_initializer() => Store(_detached).EventModelName.ShouldBeNull();
    [Fact] void should_report_the_detached_initializer_as_unbound() => _detached.Diagnostics.Single().Code.ShouldEqual(GenerationDiagnosticCodes.UnknownBackendScope);
    [Fact] void should_leave_the_registered_store_unnamed_for_a_nested_callback() => Store(_nestedCallback).EventModelName.ShouldBeNull();
    [Fact] void should_report_the_nested_callback_as_unbound() => _nestedCallback.Diagnostics.Single().Code.ShouldEqual(GenerationDiagnosticCodes.UnknownBackendScope);
    [Fact] void should_bind_an_inline_registered_object() => Index("services.AddMarten(new StoreOptions { EventModelName = \"Registered\" });").Default().Scope.EventModelName.ShouldEqual("Registered");
    [Fact] void should_bind_a_registered_local_initializer() => Index("var options = new StoreOptions { EventModelName = \"Registered\" }; services.AddMarten(options);").Default().Scope.EventModelName.ShouldEqual("Registered");
    [Fact] void should_bind_an_alias_of_the_registered_callback_parameter() => Store(Index("services.AddMartenStore<IBilling>(options => { var alias = options; alias.EventModelName = \"Registered\"; });")).EventModelName.ShouldEqual("Registered");

    static BackendScopeKey Store(MartenStoreIndex index) => index.Facts.OfType<BackendScopeFact>().Single().Definition.Key;

    static MartenStoreIndex Index(string registration)
    {
        var project = ProjectFor(Source.Replace("REGISTRATION", registration, StringComparison.Ordinal));
        project.Compilation.GetDiagnostics(TestContext.Current.CancellationToken).Where(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error).ShouldBeEmpty();
        var context = new DotNetAnalysisContext([project]);
        return MartenStoreIndex.Create(context, new() { Id = "test", Version = "1" }, new(context, context));
    }
}
