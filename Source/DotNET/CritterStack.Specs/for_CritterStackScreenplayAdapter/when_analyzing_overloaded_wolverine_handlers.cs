// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Cratis.CritterStack.Screenplay.for_CritterStackScreenplayAdapter;

public class when_analyzing_overloaded_wolverine_handlers : given.an_overloaded_wolverine_handler_application
{
    ResolvedApplicationGraph _graph = null!;

    void Because() => _graph = new GenerationResolver().Resolve([Contribution]);

    [Fact] void should_compile_the_fixture() => Project.Compilation.GetDiagnostics().Where(_ => _.Severity == Microsoft.CodeAnalysis.DiagnosticSeverity.Error).ShouldBeEmpty();
    [Fact] void should_discover_each_overload_as_a_distinct_reaction() => Reactions.Select(_ => _.Key.Subject).Distinct().Count().ShouldEqual(2);
    [Fact] void should_link_each_overload_to_its_message() => HandledMessageNames.ShouldContainOnly("FirstTrigger", "SecondTrigger");
    [Fact] void should_keep_fact_identities_distinct() => Contribution.Facts.Select(_ => _.Id.Value).Distinct(StringComparer.Ordinal).Count().ShouldEqual(Contribution.Facts.Count);
    [Fact] void should_not_create_conflicts() => _graph.Artifacts.Any(_ => _.IsConflicted).ShouldBeFalse();
    [Fact] void should_not_report_loss() => Contribution.Diagnostics.ShouldBeEmpty();

    IReadOnlyList<ResolvedArtifact> Reactions => [.. _graph.Artifacts.Where(_ => _.Key.Kind == ArtifactKind.Reaction)];

    IReadOnlyList<string> HandledMessageNames =>
        [.. _graph.Relationships
            .Where(_ => _.Key.Kind == RelationshipKind.Handles)
            .Select(relationship => _graph.Artifacts.Single(artifact => artifact.Key.Subject == relationship.Key.Target).Variants.Single().Definition.Name)];
}
