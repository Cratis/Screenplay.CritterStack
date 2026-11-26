// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Cratis.CritterStack.Screenplay.for_FactParity;

/// <summary>Native invocation-site FactIds follow the host file identity, so changing only the display root leaves them untouched.</summary>
public class when_http_delete_fact_ids_follow_the_host_file_identity : given.a_document_delete_http_command_comparison
{
    const string Types =
        """
        namespace FactParity
        {
            public sealed record Document(System.Guid Id);
            public sealed record RemoveDocument(System.Guid Id);
        }
        """;

    const string Endpoint =
        """
        namespace FactParity
        {
            public static class RemoveEndpoint
            {
                [Wolverine.Http.WolverineDelete("/documents")]
                public static void Delete(RemoveDocument command, Marten.IDocumentSession session) => session.Delete<Document>(command.Id);
            }
        }
        """;

    string[] _workspaceRootedIds = [];
    string[] _projectRootedIds = [];

    void Because()
    {
        _workspaceRootedIds = SiteFactIds(DotNetSourceDisplayRoot.Workspace);
        _projectRootedIds = SiteFactIds(DotNetSourceDisplayRoot.Project);
    }

    static string[] SiteFactIds(DotNetSourceDisplayRoot displayRoot)
    {
        var compilation = given.fact_vectors.Project(
        [
            CSharpSyntaxTree.ParseText($"{Framework}\n{MartenFramework}", path: "/workspace/FactParity/Framework.cs"),
            CSharpSyntaxTree.ParseText(Types, path: "/workspace/FactParity/Types.cs"),
            CSharpSyntaxTree.ParseText(Endpoint, path: "/workspace/FactParity/Endpoints.cs")
        ]);
        var project = compilation with
        {
            SourceContext = DotNetSourcePaths.Create(
                "FactParity/FactParity",
                new() { Version = 1, DisplayRoot = displayRoot, CasePolicy = DotNetSourcePathCasePolicy.Ordinal },
                [.. compilation.AuthoredSyntaxTrees.Select(tree => new DotNetSourceDocument
                {
                    SyntaxTree = tree,
                    ProjectRelativePath = Path.GetFileName(tree.FilePath),
                    WorkspaceRelativePath = $"FactParity/{Path.GetFileName(tree.FilePath)}"
                })])
        };
        var contribution = new real_wolverine_descriptor_bridge().Analyze(new DotNetAnalysisContext([project]), new());
        return
        [
            .. contribution.Facts
                .Where(_ => _ is EntryPointFact || (_ is RelationshipFact relationship && relationship.Definition.Key.Kind == RelationshipKind.Deletes))
                .Select(_ => _.Id.Value)
                .Order(StringComparer.Ordinal)
        ];
    }

    [Fact] void should_emit_an_entry_point_and_a_delete() => _workspaceRootedIds.Length.ShouldEqual(2);
    [Fact] void should_keep_the_fact_ids_when_only_the_display_root_changes() => _projectRootedIds.ShouldEqual(_workspaceRootedIds);
}
