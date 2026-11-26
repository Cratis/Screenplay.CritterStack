// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Text.Json;
using static Cratis.CritterStack.Screenplay.for_FactParity.given.fact_vectors;
using application = Cratis.CritterStack.Screenplay.given.a_projection_side_effect_variants_application;
using diagnostic_parity = Cratis.CritterStack.Screenplay.given.diagnostic_parity;

namespace Cratis.CritterStack.Screenplay.for_FactParity;

public class when_projection_side_effect_variants_emit_native_declarations : application
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
    [Fact] void should_share_message_identity_across_projections() => Messages("Announced").Select(fact => fact.Definition.Artifact).Distinct().Count().ShouldEqual(1);
    [Fact] void should_retain_each_projection_declaration_support() => Messages("Announced").Count().ShouldEqual(2);
    [Fact] void should_preserve_each_publication_site() => _native.Facts.OfType<RelationshipFact>().Count(fact => fact.Definition.Key.Kind == RelationshipKind.Publishes).ShouldEqual(8);
    [Fact] void should_keep_timeout_messages() => Messages("Reminder").Count().ShouldEqual(1);
    [Fact] void should_keep_impostor_interface_messages() => Messages("Impostor").Count().ShouldEqual(1);
    [Fact] void should_not_declare_custom_sending_wrappers() => Messages("Delivery").ShouldBeEmpty();
    [Fact] void should_retain_all_unresolved_and_custom_sending_diagnostics() => _actual.Diagnostics.Count(diagnostic => diagnostic.Code == "MARTEN0015").ShouldEqual(5);
    [Fact] void should_observe_members_from_both_projection_supports() => _native.Facts.OfType<ArtifactMemberTypeUseFact>().Count(fact => fact.Definition.Member.Artifact.Kind == ArtifactKind.Message && fact.Definition.Member.Artifact.Subject.Value.EndsWith(".Announced", StringComparison.Ordinal)).ShouldEqual(2);

    static string Artifacts(GeneratedScreenplayDefinition result) => JsonSerializer.Serialize(result.Graph.Artifacts.SelectMany(artifact => artifact.Variants).Select(variant => variant.Definition).ToArray());
    static string Relationships(GeneratedScreenplayDefinition result) => JsonSerializer.Serialize(result.Graph.Relationships.SelectMany(relationship => relationship.Definitions).ToArray());

    IEnumerable<ArtifactDeclarationFact> Messages(string name) => _native.Facts.OfType<ArtifactDeclarationFact>().Where(fact => fact.Definition.Artifact.Kind == ArtifactKind.Message && fact.Definition.Name == name);
}
