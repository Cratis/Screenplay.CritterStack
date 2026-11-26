// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Text.Json;
using static Cratis.CritterStack.Screenplay.for_FactParity.given.fact_vectors;
using application = Cratis.CritterStack.Screenplay.given.a_projection_side_effect_application;
using diagnostic_parity = Cratis.CritterStack.Screenplay.given.diagnostic_parity;

namespace Cratis.CritterStack.Screenplay.for_FactParity;

public class when_projection_side_effects_emit_native_declarations : application
{
    AdapterContribution _native = null!;
    GeneratedScreenplayDefinition _expected = null!;
    GeneratedScreenplayDefinition _actual = null!;

    void Because()
    {
        var contributions = given.native_query_contributions.Observe(Project);
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
    [Fact] void should_declare_the_published_message_once_despite_duplicate_registrations() => Messages().Count().ShouldEqual(1);
    [Fact] void should_name_the_published_message() => Messages().Single().Definition.Name.ShouldEqual("SummaryChanged");
    [Fact] void should_observe_the_published_message_member_type() => _native.Facts.OfType<ArtifactMemberTypeUseFact>().Where(fact => fact.Definition.Member.Artifact.Kind == ArtifactKind.Message).Select(fact => fact.Definition.Member.Name).ShouldEqual(["id"]);
    [Fact] void should_preserve_exact_publication_evidence() => Publications().Single().Evidence.Strength.ShouldEqual(EvidenceStrength.Exact);
    [Fact] void should_preserve_the_unresolved_payload_diagnostic_once() => _actual.Diagnostics.Count(diagnostic => diagnostic.Code == "MARTEN0015").ShouldEqual(1);

    static string Artifacts(GeneratedScreenplayDefinition result) => JsonSerializer.Serialize(result.Graph.Artifacts.SelectMany(artifact => artifact.Variants).Select(variant => variant.Definition).ToArray());
    static string Relationships(GeneratedScreenplayDefinition result) => JsonSerializer.Serialize(result.Graph.Relationships.SelectMany(relationship => relationship.Definitions).ToArray());

    IEnumerable<ArtifactDeclarationFact> Messages() => _native.Facts.OfType<ArtifactDeclarationFact>().Where(fact => fact.Definition.Artifact.Kind == ArtifactKind.Message);
    IEnumerable<RelationshipFact> Publications() => _native.Facts.OfType<RelationshipFact>().Where(fact => fact.Definition.Key.Kind == RelationshipKind.Publishes);
}
