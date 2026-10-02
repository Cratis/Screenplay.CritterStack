// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Cratis.CritterStack.Screenplay.for_CritterStackScreenplayGenerator;

public class when_generating_declared_wolverine_slice_patterns : given.a_wolverine_slice_pattern_application
{
    const string Application =
        """
        namespace Patterns.Transfer;
        public record Move;
        public record Moved;
        public class State;
        public static class MoveHandler
        {
            [Wolverine.Persistence.EventSourcing.SlicePattern(JasperFx.Events.EventModeling.SlicePattern.Automation)]
            public static void Handle(Move command, JasperFx.Events.IEventStream<State> stream) => stream.AppendOne(new Moved());
        }
        """;

    GeneratedScreenplayDefinition _compatibility = null!;
    GeneratedScreenplayDefinition _strict = null!;
    AdapterContribution _contribution = null!;

    void Because()
    {
        var generator = new CritterStackScreenplayGenerator();
        _compatibility = generator.Generate([CreateApplication(Application)], new CritterStackScreenplayOptions { Domain = "Patterns" });
        var strict = CreateApplication(Application, strict: true);
        _strict = generator.Generate([strict], new CritterStackScreenplayOptions { Domain = "Patterns", FeatureRoot = "Source", NamespaceSegmentsToSkip = 1 });
        _contribution = new CritterStackScreenplayAdapter().Analyze(new([strict]), new() { FeatureRoot = "Source", NamespaceSegmentsToSkip = 1 });
    }

    [Fact] void should_generate_compatibility_placement() => _compatibility.IsSuccess.ShouldBeTrue();
    [Fact] void should_generate_strict_placement() => _strict.IsSuccess.ShouldBeTrue();
    [Fact] void should_print_the_declared_kind() => _compatibility.Source.ShouldContain("slice Automation");
    [Fact] void should_keep_the_command() => _strict.Graph.Artifacts.Any(artifact => artifact.Key.Kind == ArtifactKind.Command).ShouldBeTrue();
    [Fact] void should_keep_the_produced_event() => _strict.Graph.Relationships.Count(relationship => relationship.Key.Kind == RelationshipKind.Produces).ShouldEqual(1);
    [Fact] void should_not_upgrade_folder_location_evidence() => _contribution.Facts.OfType<ArtifactPlacementFact>().All(fact => fact.Evidence.Strength == EvidenceStrength.Heuristic).ShouldBeTrue();
    [Fact] void should_preserve_separate_exact_classification_evidence() => _contribution.Facts.OfType<ArtifactPlacementFact>().All(fact => fact.Evidence.Explanation!.Contains("sliceClassification(strength=Exact", StringComparison.Ordinal)).ShouldBeTrue();
    [Fact] void should_preserve_strict_placement_provenance() => _contribution.Facts.OfType<ArtifactPlacementFact>().All(fact => fact.Evidence.Explanation!.Contains("Host-owned source structure", StringComparison.Ordinal)).ShouldBeTrue();
}
