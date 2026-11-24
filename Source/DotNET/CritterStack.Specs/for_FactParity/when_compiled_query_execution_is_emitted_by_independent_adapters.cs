// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Text.Json;
using static Cratis.CritterStack.Screenplay.for_FactParity.given.fact_vectors;
using application = Cratis.CritterStack.Screenplay.given.a_compiled_query_member_vector_application;
using diagnostic_parity = Cratis.CritterStack.Screenplay.given.diagnostic_parity;

namespace Cratis.CritterStack.Screenplay.for_FactParity;

public class when_compiled_query_execution_is_emitted_by_independent_adapters : application
{
    AdapterContribution _marten = null!;
    AdapterContribution _wolverine = null!;
    GeneratedScreenplayDefinition _expected = null!;
    GeneratedScreenplayDefinition _actual = null!;
    GeneratedScreenplayDefinition _reverse = null!;

    void Because()
    {
        var contributions = given.native_query_contributions.Observe(Project);
        _marten = contributions.Marten;
        _wolverine = contributions.Wolverine;
        _expected = Generate(contributions.Compatibility);
        _actual = Generate(_marten, _wolverine);
        _reverse = Generate(_wolverine, _marten);
    }

    [Fact] void should_preserve_all_artifact_fields() => Artifacts(_actual).ShouldEqual(Artifacts(_expected));
    [Fact] void should_preserve_all_artifact_fields_in_reverse_order() => Artifacts(_reverse).ShouldEqual(Artifacts(_expected));
    [Fact] void should_preserve_all_relationship_fields() => Relationships(_actual).ShouldEqual(Relationships(_expected));
    [Fact] void should_preserve_all_relationship_fields_in_reverse_order() => Relationships(_reverse).ShouldEqual(Relationships(_expected));
    [Fact] void should_preserve_complete_diagnostics() => Diagnostics(_actual).ShouldEqual(Diagnostics(_expected));
    [Fact] void should_preserve_complete_diagnostics_in_reverse_order() => Diagnostics(_reverse).ShouldEqual(Diagnostics(_expected));
    [Fact] void should_generate_identical_source() => _actual.Source.ShouldEqual(_expected.Source);
    [Fact] void should_generate_identical_source_in_reverse_order() => _reverse.Source.ShouldEqual(_expected.Source);
    [Fact] void should_verify_all_candidates() => new[] { _expected, _actual, _reverse }.ShouldEachConformTo(result => result.IsSuccess);
    [Fact] void should_emit_no_completed_query_artifacts() => _wolverine.Facts.OfType<ArtifactFact>().ShouldBeEmpty();
    [Fact] void should_emit_no_queries_from_the_library_adapter() => _marten.Facts.OfType<ArtifactDeclarationFact>().ShouldNotContain(fact => fact.Definition.Artifact.Kind == ArtifactKind.Query);
    [Fact] void should_record_the_proven_library_execution() => _marten.Facts.OfType<MethodArtifactUsageFact>().Count().ShouldEqual(1);
    [Fact] void should_supply_query_input_transfer_independently() => _wolverine.Facts.OfType<EntryPointInputTransferFact>().Count().ShouldEqual(1);

    static string Artifacts(GeneratedScreenplayDefinition result) => JsonSerializer.Serialize(result.Graph.Artifacts.SelectMany(artifact => artifact.Variants).Select(variant => variant.Definition).ToArray());
    static string Relationships(GeneratedScreenplayDefinition result) => JsonSerializer.Serialize(result.Graph.Relationships.SelectMany(relationship => relationship.Definitions).ToArray());
    static string[] Diagnostics(GeneratedScreenplayDefinition result) => diagnostic_parity.Meaning(result.Diagnostics);
}
