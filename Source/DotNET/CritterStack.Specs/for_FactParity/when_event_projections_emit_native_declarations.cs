// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Text.Json;
using static Cratis.CritterStack.Screenplay.for_FactParity.given.fact_vectors;
using application = Cratis.CritterStack.Screenplay.given.a_marten_projection_variants_application;
using diagnostic_parity = Cratis.CritterStack.Screenplay.given.diagnostic_parity;

namespace Cratis.CritterStack.Screenplay.for_FactParity;

public class when_event_projections_emit_native_declarations : application
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
    [Fact] void should_not_emit_compatibility_projection_artifacts() => _native.Facts.OfType<ArtifactFact>().ShouldBeEmpty();
    [Fact] void should_declare_conventional_projection_once() => Declarations(ArtifactKind.Projection, "ConventionalProjection").Select(fact => fact.Definition.Artifact).Distinct().Count().ShouldEqual(1);
    [Fact] void should_declare_switch_projection_once() => Declarations(ArtifactKind.Projection, "SwitchProjection").Select(fact => fact.Definition.Artifact).Distinct().Count().ShouldEqual(1);
    [Fact] void should_not_declare_an_unregistered_empty_projection() => Declarations(ArtifactKind.Projection, "EmptyProjection").ShouldBeEmpty();
    [Fact] void should_declare_each_observed_event_once() => Declarations(ArtifactKind.Event, "Opened").Select(fact => fact.Definition.Artifact).Distinct().Count().ShouldEqual(1);
    [Fact] void should_preserve_store_operations() => _native.Facts.OfType<RelationshipFact>().Where(fact => fact.Definition.Key.Kind == RelationshipKind.Stores).ShouldNotBeEmpty();
    [Fact] void should_preserve_insert_operations() => _native.Facts.OfType<RelationshipFact>().Where(fact => fact.Definition.Key.Kind == RelationshipKind.Stores).ShouldContain(fact => fact.Definition.Key.Target.Value.EndsWith("PingedView", StringComparison.Ordinal));
    [Fact] void should_preserve_update_operations() => _native.Facts.OfType<RelationshipFact>().Where(fact => fact.Definition.Key.Kind == RelationshipKind.Updates).ShouldNotBeEmpty();
    [Fact] void should_preserve_delete_operations() => _native.Facts.OfType<RelationshipFact>().Where(fact => fact.Definition.Key.Kind == RelationshipKind.Deletes).ShouldNotBeEmpty();
    [Fact] void should_observe_event_member_types() => _native.Facts.OfType<ArtifactMemberTypeUseFact>().Where(fact => fact.Definition.Member.Artifact.Kind == ArtifactKind.Event).ShouldNotBeEmpty();
    [Fact] void should_choose_the_closed_event_document_site_for_opened_view() => DocumentWarningCoordinates("OpenedView").ShouldEqual([44, 5, 44, 130]);
    [Fact] void should_choose_the_first_ordered_projection_site_for_shared_view() => DocumentWarningCoordinates("SharedView").ShouldEqual([7, 9, 7, 43]);
    [Fact] void should_retain_both_registration_warning_sites() => _actual.Diagnostics.Where(diagnostic => diagnostic.Code == "MARTEN0002" && diagnostic.Subject?.Value.EndsWith("/Variants.ConventionalProjection", StringComparison.Ordinal) == true).Select(diagnostic => diagnostic.Source.StartLine).Order().ShouldEqual([45, 46]);

    static string Artifacts(GeneratedScreenplayDefinition result) => JsonSerializer.Serialize(result.Graph.Artifacts.SelectMany(artifact => artifact.Variants).Select(variant => variant.Definition).ToArray());
    static string Relationships(GeneratedScreenplayDefinition result) => JsonSerializer.Serialize(result.Graph.Relationships.SelectMany(relationship => relationship.Definitions).ToArray());

    IEnumerable<ArtifactDeclarationFact> Declarations(ArtifactKind kind, string name) => _native.Facts.OfType<ArtifactDeclarationFact>().Where(fact => fact.Definition.Artifact.Kind == kind && fact.Definition.Name == name);

    int[] DocumentWarningCoordinates(string name)
    {
        var source = _actual.Diagnostics.Single(diagnostic => diagnostic.Code == "MARTEN0003" && diagnostic.Subject?.Value.EndsWith($"/Variants.{name}", StringComparison.Ordinal) == true).Source;
        return [source.StartLine, source.StartColumn, source.EndLine, source.EndColumn];
    }
}
