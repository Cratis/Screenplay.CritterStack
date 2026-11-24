// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using static Cratis.CritterStack.Screenplay.for_FactParity.given.fact_vectors;
using application = Cratis.CritterStack.Screenplay.given.a_marten_store_scope_application;

namespace Cratis.CritterStack.Screenplay.for_FactParity;

public class when_compiled_queries_have_unresolved_ownership : application
{
    const string Source =
        """
        using System.Linq;
        using System.Threading.Tasks;
        using Marten;
        using Wolverine.Http;
        namespace StoreScope;
        public interface IBilling : IDocumentStore;
        public record Account(int Id);
        public class AccountById : Marten.Linq.ICompiledQuery<Account, Account>
        {
            public int Id { get; set; }
            public System.Linq.Expressions.Expression<System.Func<Marten.Linq.IMartenQueryable<Account>, Account>> QueryIs() => query => query.First(account => account.Id == Id);
        }
        public static class Configuration
        {
            public static void Configure(Microsoft.Extensions.DependencyInjection.IServiceCollection services) =>
                services.AddMarten(options => options.EventModelName = "Primary");
        }
        public static class AccountEndpoints
        {
            [WolverineGet("/unknown")]
            [Wolverine.Marten.MartenStore(typeof(IBilling))]
            public static Task<Account> Unknown(IQuerySession session) => session.QueryAsync(new AccountById { Id = 1 });
        }
        """;

    AdapterContribution _contribution = null!;
    SubjectId _method = null!;
    SourceRange _invocation = null!;

    void Because()
    {
        var project = ProjectFor(Source);
        project.Compilation.GetDiagnostics().Where(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error).ShouldBeEmpty();
        var context = new DotNetAnalysisContext([project]);
        var subjects = new CritterStackSubjectResolver(context);
        var (_, ownership) = MartenStoreScopeDiscovery.DiscoverOwnership(context, MartenAdapter, subjects);
        var method = project.Compilation.GetTypeByMetadataName("StoreScope.AccountEndpoints").GetMembers("Unknown").OfType<IMethodSymbol>().Single();
        _method = DotNetMethodIdentity.SubjectFor(project, method);
        var execution = MartenCompiledQueryDiscovery.Observe(method, _method, project, MartenAdapter).Links.Single();
        _invocation = execution.Evidence.Source;
        _contribution = MartenCompiledQueryDiscovery.EmitFacts(project, context, MartenAdapter, ownership);
    }

    [Fact] void should_not_invent_an_artifact_occurrence() => _contribution.Facts.OfType<ArtifactDeclarationFact>().ShouldBeEmpty();
    [Fact] void should_not_map_the_unresolved_declaration_to_a_document() => _contribution.Facts.OfType<ArtifactUsageDeclarationFact>().ShouldBeEmpty();
    [Fact] void should_not_assert_an_artifact_usage() => _contribution.Facts.OfType<MethodArtifactUsageFact>().ShouldBeEmpty();
    [Fact] void should_not_emit_relationships() => _contribution.Facts.OfType<RelationshipFact>().ShouldBeEmpty();
    [Fact] void should_leave_ownership_diagnostics_to_their_owning_producers() => _contribution.Diagnostics.ShouldBeEmpty();
    [Fact] void should_emit_one_input_only_execution() => _contribution.Facts.OfType<MethodDeclarationExecutionFact>().Count().ShouldEqual(1);
    [Fact] void should_locate_the_owning_method() => Execution.Definition.Method.ShouldEqual(_method);
    [Fact] void should_preserve_the_exact_execution_source_range() => Execution.Evidence.Source.ShouldEqual(_invocation);
    [Fact] void should_keep_the_input_execution_neutral() => new[] { Execution.Definition.SourceScope, Execution.Definition.TargetScope }.ShouldEachConformTo(scope => scope is null);
    [Fact] void should_preserve_the_proved_execution_order() => Execution.Definition.ExecutionOrder.ShouldEqual(0);
    [Fact] void should_preserve_the_input_declaration() => _contribution.Facts.OfType<DeclarationInputMemberDeclarationFact>().Single().Definition.Member.Name.ShouldEqual("id");
    [Fact] void should_preserve_the_input_type() => _contribution.Facts.OfType<DeclarationInputMemberTypeUseFact>().Single().Definition.Type.Name.ShouldEqual("Int");

    MethodDeclarationExecutionFact Execution => _contribution.Facts.OfType<MethodDeclarationExecutionFact>().Single();
}
