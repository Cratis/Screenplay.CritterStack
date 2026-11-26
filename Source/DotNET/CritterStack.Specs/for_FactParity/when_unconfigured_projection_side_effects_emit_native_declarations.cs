// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Text.Json;
using static Cratis.CritterStack.Screenplay.for_FactParity.given.fact_vectors;
using application = Cratis.CritterStack.Screenplay.given.a_projection_side_effect_application;
using diagnostic_parity = Cratis.CritterStack.Screenplay.given.diagnostic_parity;

namespace Cratis.CritterStack.Screenplay.for_FactParity;

public class when_unconfigured_projection_side_effects_emit_native_declarations : application
{
    AdapterContribution _native = null!;
    GeneratedScreenplayDefinition _expected = null!;
    GeneratedScreenplayDefinition _actual = null!;

    void Because()
    {
        var contributions = given.native_query_contributions.Observe(UnconfiguredProject);
        _native = contributions.Marten;
        _expected = Generate(contributions.Compatibility);
        _actual = Generate(_native);
    }

    [Fact] void should_preserve_every_artifact_field() => Artifacts(_actual).ShouldEqual(Artifacts(_expected));
    [Fact] void should_preserve_every_relationship_field() => Relationships(_actual).ShouldEqual(Relationships(_expected));
    [Fact] void should_preserve_complete_diagnostics() => diagnostic_parity.Meaning(_actual.Diagnostics).ShouldEqual(diagnostic_parity.Meaning(_expected.Diagnostics));
    [Fact] void should_preserve_printed_source() => _actual.Source.ShouldEqual(_expected.Source);
    [Fact] void should_preserve_verification_status() => _actual.IsSuccess.ShouldEqual(_expected.IsSuccess);
    [Fact] void should_not_emit_compatibility_artifacts() => _native.Facts.OfType<ArtifactFact>().ShouldBeEmpty();
    [Fact] void should_declare_the_published_message() => _native.Facts.OfType<ArtifactDeclarationFact>().Single(fact => fact.Definition.Artifact.Kind == ArtifactKind.Message).Definition.Name.ShouldEqual("SummaryChanged");
    [Fact] void should_preserve_conventional_publication_evidence() => _native.Facts.OfType<RelationshipFact>().Single(fact => fact.Definition.Key.Kind == RelationshipKind.Publishes).Evidence.Strength.ShouldEqual(EvidenceStrength.Conventional);
    [Fact] void should_preserve_the_missing_configuration_explanation() => _native.Facts.OfType<RelationshipFact>().Single(fact => fact.Definition.Key.Kind == RelationshipKind.Publishes).Evidence.Explanation.ShouldEqual("This projection publishes a message as an inline side effect; side-effect option not observed in authored configuration");

    static string Artifacts(GeneratedScreenplayDefinition result) => JsonSerializer.Serialize(result.Graph.Artifacts.SelectMany(artifact => artifact.Variants).Select(variant => variant.Definition).ToArray());
    static string Relationships(GeneratedScreenplayDefinition result) => JsonSerializer.Serialize(result.Graph.Relationships.SelectMany(relationship => relationship.Definitions).ToArray());
}
