// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Text.Json;

namespace Cratis.CritterStack.Screenplay.for_MartenStoreIndex;

public class when_resolving_cross_project_store_ownership : given.a_marten_store_scope_application
{
    const string DomainSource =
        """
        using Marten;
        using Wolverine.Marten;
        namespace StoreScope;
        public interface AccountsStore : IDocumentStore;
        public record ChangeAccount(int Id);
        public record Account(int Id);
        [MartenStore(typeof(AccountsStore))]
        public static class AccountHandler
        {
            public static void Handle(ChangeAccount command, IDocumentSession session) => session.Store(new Account(command.Id));
        }
        """;

    const string HostSource =
        """
        using Marten;
        using StoreScope;
        namespace Host;
        public static class Configuration
        {
            public static void Configure(Microsoft.Extensions.DependencyInjection.IServiceCollection services)
            {
                services.AddMartenStore<AccountsStore>(o => o.EventModelName = "Accounts");
            }
        }
        """;

    AdapterContribution _baseline = null!;
    AdapterContribution _reversed = null!;
    AdapterContribution _relocated = null!;
    AdapterContribution _reversedTrees = null!;
    AdapterContribution _ambiguous = null!;
    AdapterContribution _sameNamed = null!;
    ResolvedApplicationGraph _graph = null!;

    void Because()
    {
        var domain = MappedDomain("/workspace");
        var host = Host(domain, "/workspace", HostSource);
        _baseline = AnalyzeProjects([domain, host]);
        _reversed = AnalyzeProjects([host, domain]);
        var relocated = MappedDomain("/relocated");
        _relocated = AnalyzeProjects([relocated, Host(relocated, "/relocated", HostSource)]);
        var reversedTrees = MappedDomain("/workspace", reverseTrees: true);
        _reversedTrees = AnalyzeProjects([reversedTrees, Host(reversedTrees, "/workspace", HostSource)]);
        _ambiguous = AnalyzeProjects([domain, Host(domain, "/workspace", HostSource.Replace("services.AddMartenStore<AccountsStore>(o => o.EventModelName = \"Accounts\");", "services.AddMartenStore<AccountsStore>(o => o.EventModelName = \"Accounts\"); services.AddMartenStore<AccountsStore>(o => o.EventModelName = \"Other\");", StringComparison.Ordinal))]);
        _sameNamed = AnalyzeProjects([domain, host, MappedDomain("/workspace/other", identity: "Other/Other")]);
        _graph = new GenerationResolver().Resolve([_baseline]);
    }

    [Fact] void should_bind_the_referenced_marker_to_its_defining_project() => _graph.Artifacts.Single(artifact => artifact.Key.Kind == ArtifactKind.Document).Key.Scope.EventModelName.ShouldEqual("Accounts");
    [Fact] void should_keep_the_document_owned_by_the_domain_project() => _graph.Artifacts.Single(artifact => artifact.Key.Kind == ArtifactKind.Document).Key.Subject.Value.ShouldContain("StoreScope");
    [Fact] void should_not_use_the_marker_as_the_store_identity() => _graph.Artifacts.Single(artifact => artifact.Key.Kind == ArtifactKind.Document).Key.Scope.Store.Value.ShouldContain("marten-store-registration");
    [Fact] void should_preserve_graphs_when_project_order_is_reversed() => Graph(_reversed).ShouldEqual(Graph(_baseline));
    [Fact] void should_preserve_graphs_when_syntax_tree_order_is_reversed() => Graph(_reversedTrees).ShouldEqual(Graph(_baseline));
    [Fact] void should_preserve_graphs_after_physical_relocation() => Graph(_relocated).ShouldEqual(Graph(_baseline));
    [Fact] void should_preserve_diagnostics_when_project_order_is_reversed() => JsonSerializer.Serialize(_reversed.Diagnostics).ShouldEqual(JsonSerializer.Serialize(_baseline.Diagnostics));
    [Fact] void should_preserve_diagnostics_after_physical_relocation() => JsonSerializer.Serialize(_relocated.Diagnostics).ShouldEqual(JsonSerializer.Serialize(_baseline.Diagnostics));
    [Fact] void should_preserve_placement_intents_when_project_order_is_reversed() => JsonSerializer.Serialize(_reversed.Facts.OfType<ArtifactPlacementFact>()).ShouldEqual(JsonSerializer.Serialize(_baseline.Facts.OfType<ArtifactPlacementFact>()));
    [Fact] void should_preserve_fact_ids_when_project_order_is_reversed() => _reversed.Facts.Select(fact => fact.Id.Value).Order(StringComparer.Ordinal).ShouldContainOnly(_baseline.Facts.Select(fact => fact.Id.Value).Order(StringComparer.Ordinal));
    [Fact] void should_not_choose_between_repeated_marker_registrations() => _ambiguous.Facts.OfType<ArtifactFact>().Any(fact => fact.Definition.Key.Kind == ArtifactKind.Document).ShouldBeFalse();
    [Fact] void should_report_unknown_ownership_for_repeated_marker_registrations() => _ambiguous.Diagnostics.Any(diagnostic => diagnostic.Code == GenerationDiagnosticCodes.AmbiguousBackendScopeOwnership).ShouldBeTrue();
    [Fact] void should_not_merge_a_same_named_marker_from_another_project() => _sameNamed.Facts.OfType<ArtifactFact>().Count(fact => fact.Definition.Key.Kind == ArtifactKind.Document).ShouldEqual(1);

    static AdapterContribution AnalyzeProjects(IReadOnlyList<DotNetProjectCompilation> projects) => new CritterStackScreenplayAdapter().Analyze(new DotNetAnalysisContext(projects), new DotNetAdapterOptions());

    static string Graph(AdapterContribution contribution) => JsonSerializer.Serialize(new GenerationResolver().Resolve([contribution]));

    static DotNetProjectCompilation MappedDomain(string root, bool reverseTrees = false, string identity = "StoreScope/StoreScope")
    {
        var template = ProjectFor(DomainSource);
        SyntaxTree[] trees = [.. template.Compilation.SyntaxTrees.Select(tree => CSharpSyntaxTree.ParseText(tree.GetText().ToString(), path: tree.FilePath.Replace("/workspace", root, StringComparison.Ordinal))), CSharpSyntaxTree.ParseText("namespace StoreScope; public record Auxiliary(int Value);", path: $"{root}/StoreScope/Auxiliary.cs")];
        var compilation = CSharpCompilation.Create(identity.Replace('/', '.'), reverseTrees ? trees.Reverse() : trees, template.Compilation.References, (CSharpCompilationOptions)template.Compilation.Options);
        return Project(identity, root, compilation, trees);
    }

    static DotNetProjectCompilation Host(DotNetProjectCompilation domain, string root, string source)
    {
        var tree = CSharpSyntaxTree.ParseText(source, path: $"{root}/Host/Configuration.cs");
        var compilation = CSharpCompilation.Create("Host", [tree], [.. domain.Compilation.References, domain.Compilation.ToMetadataReference()], (CSharpCompilationOptions)domain.Compilation.Options);
        compilation.GetDiagnostics().Where(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error).ShouldBeEmpty();
        return Project("Host/Host", root, compilation, [tree]);
    }

    static DotNetProjectCompilation Project(string identity, string root, CSharpCompilation compilation, IReadOnlyList<SyntaxTree> authored) => new()
    {
        Name = identity,
        ProjectPath = $"{root}/{identity}.csproj",
        Compilation = compilation,
        AuthoredSyntaxTrees = authored.ToHashSet(),
        SourceContext = DotNetSourcePaths.Create(
            identity,
            new DotNetSourcePathPolicy { DisplayRoot = DotNetSourceDisplayRoot.Workspace, CasePolicy = DotNetSourcePathCasePolicy.Ordinal },
            [.. authored.Select(tree => new DotNetSourceDocument { SyntaxTree = tree, ProjectRelativePath = Path.GetFileName(tree.FilePath), WorkspaceRelativePath = $"{identity}/{Path.GetFileName(tree.FilePath)}" })])
    };
}
