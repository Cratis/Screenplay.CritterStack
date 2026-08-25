// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Cratis.CritterStack.Screenplay.for_CritterStackScreenplayAdapter;

public class when_analyzing_batched_message_handlers : given.a_batched_message_handler_application
{
    ResolvedApplicationGraph _graph = null!;

    void Because() => _graph = new GenerationResolver().Resolve([Contribution]);

    [Fact] void should_compile_the_fixture() => Project.Compilation.GetDiagnostics().Where(_ => _.Severity == Microsoft.CodeAnalysis.DiagnosticSeverity.Error).ShouldBeEmpty();
    [Fact] void should_discover_the_message() => _graph.Artifacts.Any(_ => _.Key.Kind == ArtifactKind.Message && _.Variants.Single().Definition.Name == "ServiceUpdates").ShouldBeTrue();
    [Fact] void should_discover_the_reaction() => _graph.Artifacts.Any(_ => _.Key.Kind == ArtifactKind.Reaction && _.Variants.Single().Definition.Name == "ServiceUpdatesBatch").ShouldBeTrue();
    [Fact] void should_link_the_batch_handler() => HandlesRelationships.Count.ShouldEqual(1);
    [Fact] void should_mark_the_handled_message_as_a_collection() => HandlesRelationships.Single().Definition.IsCollection.ShouldBeTrue();
    [Fact] void should_retain_batched_delivery_evidence() => HandlesRelationships.Single().Evidence.Explanation.ShouldContain("batched: Wolverine delivers arrays of this message");
    [Fact] void should_not_report_loss() => Contribution.Diagnostics.ShouldBeEmpty();

    IReadOnlyList<RelationshipFact> HandlesRelationships =>
        [.. Contribution.Facts.OfType<RelationshipFact>().Where(_ => _.Definition.Key.Kind == RelationshipKind.Handles)];
}
