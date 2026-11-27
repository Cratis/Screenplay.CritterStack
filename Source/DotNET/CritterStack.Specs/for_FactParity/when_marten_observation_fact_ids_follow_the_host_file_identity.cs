// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Cratis.CritterStack.Screenplay.for_FactParity;

/// <summary>Native reads, registrations and identity configuration keep their invocation identities across display policies.</summary>
public class when_marten_observation_fact_ids_follow_the_host_file_identity : given.a_document_delete_handler_comparison
{
    const string Application =
        """
        namespace FactParity
        {
            public sealed record Document(System.Guid Id, string Code);
            public static class DocumentAccess
            {
                public static void Configure(Marten.StoreOptions options) => options.Schema.For<Document>().Identity(document => document.Code);
                public static void Read(Marten.IQuerySession session, System.Guid id) => session.Load<Document>(id);
            }
        }
        """;

    string[] _workspaceIdentities = [];
    string[] _workspaceOmissions = [];
    string[] _workspaceReads = [];

    void Because()
    {
        CompareApplication(DotNetSourceDisplayRoot.Workspace);
        _workspaceIdentities = FactIds<ArtifactMemberRoleSelectionFact>();
        _workspaceOmissions = FactIds<OccurrenceOmissionDiagnosticFact>();
        _workspaceReads = FactIds<RelationshipFact>();
        CompareApplication(DotNetSourceDisplayRoot.Project);
    }

    [Fact] void should_keep_identity_fact_ids() => FactIds<ArtifactMemberRoleSelectionFact>().ShouldEqual(_workspaceIdentities);
    [Fact] void should_keep_omission_fact_ids() => FactIds<OccurrenceOmissionDiagnosticFact>().ShouldEqual(_workspaceOmissions);
    [Fact] void should_keep_read_fact_ids() => FactIds<RelationshipFact>().ShouldEqual(_workspaceReads);

    [Fact]
    void should_key_each_observation_by_its_host_file_and_span()
    {
        var calls = ComparedProject.AuthoredSyntaxTrees.Single(tree => Path.GetFileName(tree.FilePath) == "Application.cs")
            .GetRoot(TestContext.Current.CancellationToken).DescendantNodes().OfType<InvocationExpressionSyntax>().ToArray();
        calls.Length.ShouldEqual(3);
        var sites = calls.Select(Site).ToArray();
        AssertSites(FactIds<ArtifactMemberRoleSelectionFact>(), ":document-identity:", sites);
        AssertSites(FactIds<OccurrenceOmissionDiagnosticFact>(), ":omission:", sites);
        var read = calls.Single(call => ((MemberAccessExpressionSyntax)call.Expression).Name.Identifier.ValueText == "Load");
        AssertSites(FactIds<RelationshipFact>(), ":relationship:", [Site(read)]);
    }

    [Fact]
    void should_preserve_compatibility_output_with_real_admission()
    {
        AllAdmitted().ShouldBeTrue();
        AllArtifactsMatch().ShouldBeTrue();
        AllRelationshipsMatch().ShouldBeTrue();
        AssertDiagnosticsMatch();
        AllSourcesMatch().ShouldBeTrue();
        AllStatusesMatch().ShouldBeTrue();
    }

    void CompareApplication(DotNetSourceDisplayRoot root) => CompareHandlerFiles(root, (Application, "/workspace/FactParity/Application.cs"));

    string Site(InvocationExpressionSyntax call)
    {
        var source = DotNetSource.RangeForProject(call.GetLocation(), ComparedProject);
        source.FileIdentity.ShouldNotBeNull();
        return $":{Uri.EscapeDataString(source.FileIdentity.ToString())}:{call.SpanStart}:{call.Span.Length}";
    }

    string[] FactIds<T>()
        where T : GenerationFact => [.. NativeMarten.Facts.OfType<T>().Select(fact => fact.Id.Value).Order(StringComparer.Ordinal)];

    static void AssertSites(string[] ids, string marker, string[] sites)
    {
        ids.Length.ShouldEqual(sites.Length);
        foreach (var site in sites)
        {
            ids.Single(id => id.EndsWith(site, StringComparison.Ordinal)).ShouldContain(marker);
        }
    }
}
