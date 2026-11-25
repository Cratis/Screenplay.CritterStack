// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Text.Json;
using static Cratis.CritterStack.Screenplay.for_FactParity.given.fact_vectors;
using application = Cratis.CritterStack.Screenplay.given.a_marten_store_scope_application;
using diagnostic_parity = Cratis.CritterStack.Screenplay.given.diagnostic_parity;

namespace Cratis.CritterStack.Screenplay.for_FactParity;

public class when_aggregate_projections_emit_native_declarations : application
{
    const string Source =
        """
        using System;
        using Marten;
        namespace StoreScope;
        public interface IAccounts : IDocumentStore;
        public interface IBilling : IDocumentStore;
        public enum Status { Open, Closed }
        public record Address(string Street);
        public record Opened(Guid Id, Status Status, int? Score);
        public record Renamed(string Name);
        public record Account(Guid Id, string Name, Status Status, int? Score, Address[] Addresses)
        {
            public static Account Create(Opened opened) => throw new NotImplementedException();
            public void Apply(Renamed renamed) { }
        }
        public static class Configuration
        {
            public static void Configure(Microsoft.Extensions.DependencyInjection.IServiceCollection services)
            {
                services.AddMartenStore<IAccounts>(options => { options.EventModelName = "Shared"; options.Projections.Snapshot<Account>(); });
                services.AddMartenStore<IBilling>(options => { options.EventModelName = "Shared"; options.Projections.Snapshot<Account>(); });
            }
        }
        """;

    AdapterContribution _native = null!;
    GeneratedScreenplayDefinition _expected = null!;
    GeneratedScreenplayDefinition _actual = null!;
    GeneratedScreenplayDefinition _reverse = null!;

    void Because()
    {
        var project = ProjectFor(Source);
        project.Compilation.GetDiagnostics().Where(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error).ShouldBeEmpty();
        var contributions = given.native_query_contributions.Observe(project);
        _native = contributions.Marten;
        _expected = Generate(contributions.Compatibility);
        _actual = Generate(contributions.Marten, contributions.Wolverine);
        _reverse = Generate(contributions.Wolverine, contributions.Marten);
    }

    [Fact] void should_preserve_every_artifact_field_except_the_accepted_binding_discriminator() => Artifacts(_actual).ShouldEqual(Artifacts(_expected));
    [Fact] void should_preserve_every_artifact_field_except_the_accepted_binding_discriminator_in_reverse_order() => Artifacts(_reverse).ShouldEqual(Artifacts(_expected));
    [Fact] void should_preserve_every_relationship_field() => Relationships(_actual).ShouldEqual(Relationships(_expected));
    [Fact] void should_preserve_every_relationship_field_in_reverse_order() => Relationships(_reverse).ShouldEqual(Relationships(_expected));

    [Fact] void should_preserve_complete_diagnostics() => Diagnostics(_actual).ShouldEqual(Diagnostics(_expected));
    [Fact] void should_preserve_complete_diagnostics_in_reverse_order() => Diagnostics(_reverse).ShouldEqual(Diagnostics(_expected));
    [Fact] void should_preserve_printed_source() => _actual.Source.ShouldEqual(_expected.Source);
    [Fact] void should_preserve_printed_source_in_reverse_order() => _reverse.Source.ShouldEqual(_expected.Source);
    [Fact] void should_preserve_verification_status() => _actual.IsSuccess.ShouldEqual(_expected.IsSuccess);
    [Fact] void should_preserve_verification_status_in_reverse_order() => _reverse.IsSuccess.ShouldEqual(_expected.IsSuccess);
    [Fact] void should_not_emit_compatibility_projection_artifacts() => _native.Facts.OfType<ArtifactFact>().Where(fact => fact.Definition.Key.Kind != ArtifactKind.Concept).ShouldBeEmpty();

    // Accepted metadata delta: decision-gap10-enum-concept-kind.md, Revision 2026-11-24.
    [Fact] void should_keep_the_accepted_native_concept_discriminator() => BoundTypes(_actual).Select(type => type.TargetArtifactKind).Distinct().ShouldContainOnly(ArtifactKind.Concept);
    [Fact] void should_keep_the_accepted_compatibility_null_discriminator() => BoundTypes(_expected).Select(type => type.TargetArtifactKind).Distinct().ShouldContainOnly((ArtifactKind?)null);
    [Fact] void should_not_change_discriminators_on_unbound_types() => Definitions(_actual).SelectMany(definition => definition.Properties).Where(property => property.Type.Subject is null).ShouldEachConformTo(property => property.Type.TargetArtifactKind is null);
    [Fact] void should_declare_both_aggregate_occurrences() => _native.Facts.OfType<ArtifactDeclarationFact>().Count(fact => fact.Definition.Artifact.Kind == ArtifactKind.Aggregate).ShouldEqual(2);
    [Fact] void should_declare_every_event_occurrence() => _native.Facts.OfType<ArtifactDeclarationFact>().Count(fact => fact.Definition.Artifact.Kind == ArtifactKind.Event).ShouldEqual(4);
    [Fact] void should_keep_both_store_identities() => _native.Facts.OfType<ArtifactDeclarationFact>().Where(fact => fact.Definition.Artifact.Kind == ArtifactKind.Reducer).Select(fact => fact.Definition.Artifact.Scope.Store).Distinct().Count().ShouldEqual(2);
    [Fact] void should_observe_model_and_event_member_types() => _native.Facts.OfType<ArtifactMemberTypeUseFact>().Count().ShouldBeGreaterThan(0);
    [Fact] void should_keep_fact_ids_unique() => _native.Facts.Select(fact => fact.Id).Distinct().Count().ShouldEqual(_native.Facts.Count);

    static IEnumerable<ArtifactDefinition> Definitions(GeneratedScreenplayDefinition result) => result.Graph.Artifacts.SelectMany(artifact => artifact.Variants).Select(variant => variant.Definition);
    static IEnumerable<TypeReferenceDefinition> BoundTypes(GeneratedScreenplayDefinition result) => Definitions(result).SelectMany(definition => definition.Properties).Select(property => property.Type).Where(type => type.Subject is not null);
    static string Artifacts(GeneratedScreenplayDefinition result) => JsonSerializer.Serialize(Definitions(result).Select(definition => definition with
    {
        Properties = [.. definition.Properties.Select(property => property with { Type = property.Type with { TargetArtifactKind = null } })]
    }).ToArray());
    static string Relationships(GeneratedScreenplayDefinition result) => JsonSerializer.Serialize(result.Graph.Relationships.SelectMany(relationship => relationship.Definitions).ToArray());
    static string[] Diagnostics(GeneratedScreenplayDefinition result) => diagnostic_parity.Meaning(result.Diagnostics);
}
