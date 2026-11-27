// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Cratis.CritterStack.Screenplay.for_FactParity;

/// <summary>Changing the display root cannot change the identity of matching sites in different authored files.</summary>
public class when_mixed_document_writes_share_site_coordinates_across_files : given.a_document_delete_handler_comparison
{
    const string Types = "namespace FactParity { public sealed record Document(int Id); public sealed record Remove(int Id); }";
    const string FirstFile =
        """
        namespace FactParity
        {
            public static class FirstHandler
            {
                public static void Handle(Remove message, Marten.IDocumentSession session)
                {
                    session.Delete<Document>(message.Id);
                    session.Store(new Document(message.Id));
                }
            }
        }
        """;

    string[] _workspaceMartenIds = [];
    string[] _projectMartenIds = [];
    string[] _workspaceWolverineIds = [];
    string[] _projectWolverineIds = [];
    GeneratedScreenplayDefinition[] _workspaceComposites = [];
    GeneratedScreenplayDefinition _workspaceOracle = null!;
    bool _workspaceAdmitted;

    void Because()
    {
        CompareFiles(DotNetSourceDisplayRoot.Workspace);
        _workspaceMartenIds = Ids(NativeMarten.Facts.OfType<RelationshipFact>());
        _workspaceWolverineIds = Ids([.. EntryPoints, .. NativeDeletes]);
        _workspaceComposites = [.. Composites];
        _workspaceOracle = Expected;
        _workspaceAdmitted = AllAdmitted();
        CompareFiles(DotNetSourceDisplayRoot.Project);
        _projectMartenIds = Ids(NativeMarten.Facts.OfType<RelationshipFact>());
        _projectWolverineIds = Ids([.. EntryPoints, .. NativeDeletes]);
    }

    void CompareFiles(DotNetSourceDisplayRoot root) => CompareHandlerFiles(
        root,
        (Types, "/workspace/FactParity/Types.cs"),
        (FirstFile, "/workspace/FactParity/First.cs"),
        (FirstFile.Replace("FirstHandler", "OtherHandler"), "/workspace/FactParity/Other.cs"));

    [Fact] void should_keep_marten_operation_ids_when_only_the_display_root_changes() => _projectMartenIds.ShouldEqual(_workspaceMartenIds);
    [Fact] void should_keep_wolverine_site_ids_when_only_the_display_root_changes() => _projectWolverineIds.ShouldEqual(_workspaceWolverineIds);

    [Fact]
    void should_include_the_host_identity_and_exact_span_for_each_fact()
    {
        var trees = ComparedProject.AuthoredSyntaxTrees.Where(tree =>
            string.Equals(Path.GetFileName(tree.FilePath), "First.cs", StringComparison.Ordinal) ||
            string.Equals(Path.GetFileName(tree.FilePath), "Other.cs", StringComparison.Ordinal));
        var methods = trees.Select(tree => tree.GetRoot().DescendantNodes().OfType<MethodDeclarationSyntax>().Single()).ToArray();
        methods.Select(method => method.Identifier.Span).Distinct().Count().ShouldEqual(1);
        methods.SelectMany(method => method.DescendantNodes().OfType<InvocationExpressionSyntax>()).Select(call => call.Span).Distinct().Count().ShouldEqual(2);
        foreach (var method in methods)
        {
            var entry = Site(method.Identifier.GetLocation());
            _projectWolverineIds.Count(id => id.EndsWith(entry, StringComparison.Ordinal)).ShouldEqual(1);
            foreach (var call in method.DescendantNodes().OfType<InvocationExpressionSyntax>())
            {
                var site = Site(call.GetLocation());
                _projectMartenIds.Count(id => id.EndsWith(site, StringComparison.Ordinal)).ShouldEqual(1);
                var isDelete = ((MemberAccessExpressionSyntax)call.Expression).Name.Identifier.ValueText == "Delete";
                _projectWolverineIds.Count(id => id.EndsWith(site, StringComparison.Ordinal)).ShouldEqual(isDelete ? 1 : 0);
            }
        }
    }

    [Fact]
    void should_admit_both_rosters_under_both_policies()
    {
        _workspaceAdmitted.ShouldBeTrue();
        AllAdmitted().ShouldBeTrue();
    }

    [Fact]
    void should_not_repeat_any_fact_id()
    {
        Native.Facts.Select(fact => fact.Id).Distinct().Count().ShouldEqual(Native.Facts.Count);
        NativeMarten.Facts.Select(fact => fact.Id).Distinct().Count().ShouldEqual(NativeMarten.Facts.Count);
    }

    [Fact]
    void should_match_the_oracle_within_each_display_policy()
    {
        Commands(_workspaceOracle).Single().Name.ShouldEqual("Remove");
        Commands(Expected).Single().Name.ShouldEqual("Remove");
        AllArtifactsMatch().ShouldBeTrue();
        AllRelationshipsMatch().ShouldBeTrue();
        AssertDiagnosticsMatch();
        AllSourcesMatch().ShouldBeTrue();
        AllStatusesMatch().ShouldBeTrue();
        foreach (var actual in _workspaceComposites)
        {
            ExactArtifacts(actual).ShouldEqual(ExactArtifacts(_workspaceOracle));
            Relationships(actual).ShouldEqual(Relationships(_workspaceOracle));
            Diagnostics(actual).ShouldEqual(Diagnostics(_workspaceOracle));
            actual.Source.ShouldEqual(_workspaceOracle.Source);
            actual.IsSuccess.ShouldEqual(_workspaceOracle.IsSuccess);
        }
    }

    string Site(Location location)
    {
        var source = DotNetSource.RangeForProject(location, ComparedProject);
        source.FileIdentity.ShouldNotBeNull();
        return $":{Uri.EscapeDataString(source.FileIdentity.ToString())}:{location.SourceSpan.Start}:{location.SourceSpan.Length}";
    }

    static string[] Ids(IEnumerable<GenerationFact> facts) => [.. facts.Select(fact => fact.Id.Value).Order(StringComparer.Ordinal)];
}
