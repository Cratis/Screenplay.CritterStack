// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Cratis.CritterStack.Screenplay.for_CritterStackScreenplayAdapter;

public class when_analyzing_persistence_bound_queries : given.a_persistence_bound_query_application
{
    ResolvedApplicationGraph _graph = null!;

    void Because() => _graph = new GenerationResolver().Resolve([Contribution]);

    [Fact] void should_compile_the_fixture() => Project.Compilation.GetDiagnostics().Where(_ => _.Severity == Microsoft.CodeAnalysis.DiagnosticSeverity.Error).ShouldBeEmpty();
    [Fact] void should_retain_each_bound_query_read() => Reads.Count.ShouldEqual(3);
    [Fact] void should_retain_the_entity_read() => ReadFrom("GetById", "Student").Definition.Key.Discriminator.ShouldEqual("entity");
    [Fact] void should_retain_the_first_or_default_read() => ReadFrom("GetDefaults", "Defaults").Definition.Key.Discriminator.ShouldEqual("first-or-default");
    [Fact] void should_retain_the_queryable_read() => ReadFrom("GetHeartbeats", "Heartbeat").Definition.Key.Discriminator.ShouldEqual("queryable");
    [Fact] void should_mark_the_queryable_read_as_a_collection() => ReadFrom("GetHeartbeats", "Heartbeat").Definition.IsCollection.ShouldBeTrue();
    [Fact] void should_report_only_http_metadata_loss() => Contribution.Diagnostics.Select(_ => _.Code).Distinct().ShouldContainOnly(WolverineDiagnosticCodes.HttpMetadataOmitted);

    IReadOnlyList<RelationshipFact> Reads =>
        [.. Contribution.Facts.OfType<RelationshipFact>().Where(_ => _.Definition.Key.Kind == RelationshipKind.Reads)];

    RelationshipFact ReadFrom(string queryName, string modelName)
    {
        var query = _graph.Artifacts.Single(_ => _.Key.Kind == ArtifactKind.Query && _.Variants.Single().Definition.Name == queryName);
        var model = _graph.Artifacts.Single(_ => _.Key.Kind == ArtifactKind.ReadModel && _.Variants.Single().Definition.Name == modelName);
        return Reads.Single(_ => _.Definition.Key.Source == query.Key.Subject && _.Definition.Key.Target == model.Key.Subject);
    }
}
