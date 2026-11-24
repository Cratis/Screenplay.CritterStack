// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Text.Json;
using static Cratis.CritterStack.Screenplay.for_FactParity.given.fact_vectors;

namespace Cratis.CritterStack.Screenplay.for_FactParity;

public class when_query_artifacts_are_emitted_from_source_observations : Specification
{
    const string Source =
        """
        #nullable enable
        namespace Wolverine { public sealed class WolverineOptions; public abstract class Saga; }
        namespace Wolverine.Http
        {
            public sealed class WolverineGetAttribute(string route) : System.Attribute;
        }
        namespace NativeQueryArtifacts
        {
            public sealed record Result(System.Guid Id, string? Name, string[] Tags, System.DateTimeOffset When);
            public sealed class SagaState : Wolverine.Saga;
            public static class OrdersEndpoint
            {
                [Wolverine.Http.WolverineGet("/orders")]
                public static System.Threading.Tasks.Task<System.Collections.Generic.IReadOnlyList<Result>> Get(System.Guid id) => throw new System.NotImplementedException();
            }
            public static class Endpoints
            {
                [Wolverine.Http.WolverineGet("/optional")]
                public static Result? Optional(System.DateOnly date) => null;
                [Wolverine.Http.WolverineGet("/number")]
                public static int Number() => 0;
                [Wolverine.Http.WolverineGet("/saga")]
                public static SagaState Saga() => new();
                [Wolverine.Http.WolverineGet("/external")]
                public static System.Uri External() => new("https://example.com");
            }
        }
        """;

    DotNetProjectCompilation _project = null!;
    DotNetAnalysisContext _context = null!;
    AdapterContribution _legacy = null!;
    AdapterContribution _native = null!;
    WolverineQueryObservation[] _observations = [];
    GeneratedScreenplayDefinition _expected = null!;
    GeneratedScreenplayDefinition _actual = null!;
    GeneratedScreenplayDefinition _reverse = null!;

    void Establish()
    {
        _project = Project(Source);
        _context = new([_project]);
        _legacy = new CritterStackScreenplayAdapter().Analyze(_context, new());
    }

    void Because()
    {
        _observations = [.. WolverineFacts.ObserveEntryPoints(_project, new(true, true, []))
            .Where(entry => entry.Endpoint is { IsQuery: true })
            .Select(entry => WolverineFacts.ObserveQuery(_project, entry.Endpoint!))
            .OfType<WolverineQueryObservation>()];
        var emitter = WolverineFacts.QueryArtifacts(_project, _context, new(), WolverineAdapter);
        var emitted = _observations.Select(query => emitter.Emit(query, BackendOwnership.Legacy)).ToArray();
        List<GenerationDiagnostic> diagnostics = [.. emitted.SelectMany(result => result.Diagnostics)];
        var placement = CritterStackSourcePlacement.Derive(_context, new(), emitted.SelectMany(result => result.Placements!), diagnostics);
        _native = new()
        {
            Adapter = WolverineAdapter,
            Facts = [.. emitted.SelectMany(result => result.Facts), .. placement],
            Diagnostics = diagnostics
        };
        _expected = Generate(_legacy);
        _actual = Generate(_native);
        _reverse = Generate(_native with { Facts = [.. _native.Facts.Reverse()], Diagnostics = [.. _native.Diagnostics.Reverse()] });
    }

    [Fact] void should_observe_only_source_models_that_are_not_saga_state() => _observations.Select(query => query.Name).ShouldEqualInOrder("Optional", "Orders");
    [Fact] void should_preserve_every_artifact_field() => ArtifactTranscript(_actual).ShouldEqual(ArtifactTranscript(_expected));
    [Fact] void should_preserve_every_artifact_field_in_reverse_fact_order() => ArtifactTranscript(_reverse).ShouldEqual(ArtifactTranscript(_expected));
    [Fact] void should_preserve_every_relationship_field() => RelationshipTranscript(_actual).ShouldEqual(RelationshipTranscript(_expected));
    [Fact] void should_preserve_every_relationship_field_in_reverse_fact_order() => RelationshipTranscript(_reverse).ShouldEqual(RelationshipTranscript(_expected));
    [Fact] void should_preserve_every_placement_field() => PlacementTranscript(_actual).ShouldEqual(PlacementTranscript(_expected));
    [Fact] void should_preserve_every_placement_field_in_reverse_fact_order() => PlacementTranscript(_reverse).ShouldEqual(PlacementTranscript(_expected));
    [Fact] void should_preserve_complete_diagnostics() => Diagnostics(_actual).ShouldEqual(Diagnostics(_expected));
    [Fact] void should_preserve_complete_diagnostics_in_reverse_fact_order() => Diagnostics(_reverse).ShouldEqual(Diagnostics(_expected));
    [Fact] void should_generate_identical_source() => _actual.Source.ShouldEqual(_expected.Source);
    [Fact] void should_generate_identical_source_in_reverse_fact_order() => _reverse.Source.ShouldEqual(_expected.Source);
    [Fact] void should_compile_all_candidates() => new[] { _actual, _reverse, _expected }.ShouldEachConformTo(result => result.IsSuccess);
    [Fact] void should_declare_queries_and_result_models_without_completed_artifacts() => _native.Facts.OfType<ArtifactFact>().ShouldBeEmpty();
    [Fact] void should_preserve_collection_returns() => Returns("Orders").IsCollection.ShouldBeTrue();
    [Fact] void should_preserve_optional_returns() => Returns("Optional").IsOptional.ShouldBeTrue();
    [Fact] void should_not_turn_result_id_properties_into_identifiers() => _actual.Graph.Artifacts.Where(artifact => artifact.Key.Kind == ArtifactKind.ReadModel).SelectMany(artifact => artifact.Variants).SelectMany(variant => variant.Definition.Properties).ShouldEachConformTo(property => !property.IsIdentifier);

    RelationshipDefinition Returns(string name) => _actual.Graph.Relationships.Single(relationship =>
        relationship.Key.Kind == RelationshipKind.Returns && relationship.Key.Source == _observations.Single(query => query.Name == name).Subject).Definitions.Single();

    static string ArtifactTranscript(GeneratedScreenplayDefinition result) => JsonSerializer.Serialize(result.Graph.Artifacts.SelectMany(artifact => artifact.Variants).Select(variant => variant.Definition).ToArray());
    static string RelationshipTranscript(GeneratedScreenplayDefinition result) => JsonSerializer.Serialize(result.Graph.Relationships.SelectMany(relationship => relationship.Definitions).ToArray());
    static string PlacementTranscript(GeneratedScreenplayDefinition result) => JsonSerializer.Serialize(result.Graph.Placements.Select(placement => new { placement.Artifact, Variants = placement.Variants.Select(variant => variant.Placement).ToArray() }).ToArray());
    static string Diagnostics(GeneratedScreenplayDefinition result) => JsonSerializer.Serialize(result.Diagnostics);
}
