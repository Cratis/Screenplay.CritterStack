// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using static Cratis.CritterStack.Screenplay.for_FactParity.given.fact_vectors;

namespace Cratis.CritterStack.Screenplay.for_FactParity;

public class when_query_parameters_are_emitted_from_source_symbols : Specification
{
    const string Source =
        """
        #nullable enable
        namespace Wolverine { public sealed class WolverineOptions; }
        namespace Wolverine.Http
        {
            public sealed class WolverineGetAttribute(string route) : System.Attribute;
        }
        namespace NativeQueries
        {
            public enum Mode { Active, Inactive }
            public sealed record Request(string Name);
            public sealed record Result(System.Guid Id);
            public static class Endpoints
            {
                [Wolverine.Http.WolverineGet("/search")]
                public static Result Search(Mode mode, System.Guid id, System.DateOnly date, string text, int limit, Request request, System.Threading.CancellationToken cancellation) => new(id);
                [Wolverine.Http.WolverineGet("/enum")]
                public static Result EnumOnly(Mode mode) => new(System.Guid.Empty);
                [Wolverine.Http.WolverineGet("/optional")]
                public static Result OptionalEnum(Mode? mode, System.Guid id) => new(id);
                [Wolverine.Http.WolverineGet("/folded")]
                public static Result FoldedNames(Mode value, System.Guid Value) => new(Value);
            }
        }
        """;

    DotNetProjectCompilation _project = null!;
    DotNetAnalysisContext _context = null!;
    AdapterContribution _compatibility = null!;
    AdapterContribution _result = null!;
    AdapterContribution _concept = null!;
    ArtifactKey _resultKey = null!;
    QueryVector[] _vectors = [];

    void Establish()
    {
        _project = Project(Source);
        _context = new([_project]);
        _compatibility = new CritterStackScreenplayAdapter().Analyze(_context, new());
        var result = Type(_project, "NativeQueries.Result");
        _resultKey = Key(_project, result, ArtifactKind.ReadModel);
        var evidence = Evidence(_project, result, WolverineAdapter);
        _result = Contribution(WolverineAdapter, [.. Surface(_project, result, _resultKey, WolverineAdapter), Placement(_resultKey, evidence)]);
        var mode = Type(_project, "NativeQueries.Mode");
        var key = Key(_project, mode, ArtifactKind.Concept);
        _concept = Contribution(MartenAdapter,
        [
            Declaration(key, mode, Evidence(_project, mode, MartenAdapter)),
            new ConceptRepresentationFact
            {
                Id = new() { Value = "query-member-vector:mode" }, Subject = key.Subject, Evidence = Evidence(_project, mode, MartenAdapter),
                Definition = new() { Concept = key.Subject, Kind = ConceptRepresentationKind.Enumeration, EnumerationValues = ["Active", "Inactive"], EnumerationNumbers = [0, 1] }
            }
        ]);
    }

    void Because() => _vectors = [Observe("Search"), Observe("EnumOnly"), Observe("OptionalEnum"), Observe("FoldedNames")];

    [Fact] void should_preserve_every_member_field_except_the_accepted_binding_discriminator() => _vectors.Select(_ => MemberTranscript(Definition(_.Forward, _.Expected.Key))).ShouldEqualInOrder([.. _vectors.Select(_ => MemberTranscript(_.Expected))]);
    [Fact] void should_preserve_the_same_member_parity_in_reverse_contribution_order() => _vectors.Select(_ => MemberTranscript(Definition(_.Reverse, _.Expected.Key))).ShouldEqualInOrder([.. _vectors.Select(_ => MemberTranscript(_.Expected))]);
    [Fact] void should_verify_the_compatibility_and_both_native_candidates() => _vectors.ShouldEachConformTo(_ => _.Forward.IsSuccess && _.Reverse.IsSuccess && _.Legacy.IsSuccess);
    [Fact] void should_filter_infrastructure_primitives_and_source_request_objects() => _vectors[0].Facts.OfType<ArtifactMemberDeclarationFact>().Select(_ => _.Definition.Member.Name).ShouldEqualInOrder("mode", "id", "date");
    [Fact] void should_not_let_an_authored_enum_take_the_first_non_enum_identifier() => Identifier(_vectors[0]).ShouldEqual("id");
    [Fact] void should_not_invent_an_identifier_for_an_enum_only_query() => Identifier(_vectors[1]).ShouldEqual(string.Empty);
    [Fact] void should_preserve_the_existing_nullable_enum_identifier_rule() => Identifier(_vectors[2]).ShouldEqual("mode");
    [Fact] void should_keep_the_first_normalized_name_without_borrowing_a_later_role() => Identifier(_vectors[3]).ShouldEqual(string.Empty);
    [Fact] void should_keep_one_member_when_parameter_names_fold_together() => _vectors[3].Facts.OfType<ArtifactMemberDeclarationFact>().Count().ShouldEqual(1);
    [Fact] void should_not_build_compatibility_artifacts() => _vectors.SelectMany(_ => _.Facts).OfType<ArtifactFact>().ShouldBeEmpty();
    [Fact] void should_emit_exact_source_type_uses_for_enum_parameters() => _vectors.SelectMany(_ => _.Facts).OfType<ArtifactMemberTypeUseFact>().Count(_ => _.Definition.Type.ObservedTypeSubject is not null).ShouldEqual(4);
    [Fact] void should_keep_concept_only_source_binding_policies() => _vectors.SelectMany(_ => _.Facts).OfType<TypeUseBindingPolicyFact>().ShouldEachConformTo(_ => _.Definition.Policy == TypeUseBindingPolicyKind.ConceptOnlyWithUnboundFallback);

    // Accepted metadata delta: decision-gap10-enum-concept-kind.md, Revision 2026-11-24.
    [Fact] void should_document_the_accepted_native_concept_discriminator() => _vectors.SelectMany(_ => BoundKinds(Definition(_.Forward, _.Expected.Key))).ShouldEqualInOrder(ArtifactKind.Concept, ArtifactKind.Concept, ArtifactKind.Concept, ArtifactKind.Concept);
    [Fact] void should_document_the_same_accepted_discriminator_in_reverse_order() => _vectors.SelectMany(_ => BoundKinds(Definition(_.Reverse, _.Expected.Key))).ShouldEqualInOrder(ArtifactKind.Concept, ArtifactKind.Concept, ArtifactKind.Concept, ArtifactKind.Concept);
    [Fact] void should_document_the_accepted_legacy_null_discriminator() => _vectors.SelectMany(_ => BoundKinds(_.Expected)).ShouldEqualInOrder(null, null, null, null);
    [Fact] void should_not_change_discriminators_on_unbound_types() => _vectors.SelectMany(vector => new[] { vector.Expected, Definition(vector.Forward, vector.Expected.Key), Definition(vector.Reverse, vector.Expected.Key) }).SelectMany(_ => _.Properties).Where(_ => _.Type.Subject is null).ShouldEachConformTo(_ => _.Type.TargetArtifactKind is null);
    [Fact] void should_lower_to_identical_generated_source() => _vectors.Select(_ => _.Forward.Source).ShouldEqualInOrder([.. _vectors.Select(_ => _.Legacy.Source)]);
    [Fact] void should_lower_to_identical_generated_source_in_reverse_order() => _vectors.Select(_ => _.Reverse.Source).ShouldEqualInOrder([.. _vectors.Select(_ => _.Legacy.Source)]);
    [Fact] void should_preserve_complete_generation_diagnostics() => _vectors.Select(_ => Diagnostics(_.Forward)).ShouldEqualInOrder([.. _vectors.Select(_ => Diagnostics(_.Legacy))]);
    [Fact] void should_preserve_complete_generation_diagnostics_in_reverse_order() => _vectors.Select(_ => Diagnostics(_.Reverse)).ShouldEqualInOrder([.. _vectors.Select(_ => Diagnostics(_.Legacy))]);

    QueryVector Observe(string name)
    {
        var method = Type(_project, "NativeQueries.Endpoints").GetMembers(name).OfType<IMethodSymbol>().Single();
        var subject = DotNetMethodIdentity.SubjectFor(_project, method);
        var key = new ArtifactKey { Subject = new() { Value = $"{subject.Value}:query" }, Kind = ArtifactKind.Query };
        var evidence = Evidence(_project, method, WolverineAdapter);
        var members = WolverineFacts.QueryMemberFacts(method, key, _context, evidence);
        GenerationFact[] shared =
        [
            Placement(key, evidence),
            new RelationshipFact
            {
                Id = new() { Value = $"query-member-vector:{name}:returns" }, Subject = key.Subject, Evidence = evidence,
                Definition = new() { Key = new() { Source = key.Subject, Kind = RelationshipKind.Returns, Target = _resultKey.Subject } }
            }
        ];
        var query = Contribution(WolverineAdapter,
        [
            new ArtifactDeclarationFact
            {
                Id = new() { Value = $"query-member-vector:{name}" }, Subject = key.Subject, Evidence = evidence,
                Definition = new() { Artifact = key, Name = name, File = evidence.Source?.Path }
            },
            .. members,
            .. shared
        ]);
        var original = _compatibility.Facts.OfType<ArtifactFact>().Single(_ => _.Definition.Key == key);
        var legacy = Contribution(_compatibility.Adapter, [original, .. shared]);
        return new(original.Definition, Generate(query, _result, _concept), Generate(_concept, _result, query), Generate(legacy, _result, _concept), members);
    }

    static string Identifier(QueryVector vector) => string.Join(',', vector.Facts.OfType<ArtifactMemberRoleFact>().Select(_ => _.Definition.Member.Name));

    static IEnumerable<ArtifactKind?> BoundKinds(ArtifactDefinition definition) => definition.Properties.Where(_ => _.Type.Subject is not null).Select(_ => _.Type.TargetArtifactKind);

    static string MemberTranscript(ArtifactDefinition definition)
    {
        var comparison = System.Text.Json.JsonSerializer.SerializeToNode(definition.Properties).AsArray();
        foreach (var property in comparison)
        {
            property["Type"].AsObject().Remove(nameof(TypeReferenceDefinition.TargetArtifactKind));
        }

        return comparison.ToJsonString();
    }

    static string Diagnostics(GeneratedScreenplayDefinition result) => System.Text.Json.JsonSerializer.Serialize(result.Diagnostics);

    static ArtifactPlacementFact Placement(ArtifactKey key, Evidence evidence) => new()
    {
        Id = new() { Value = $"query-member-vector:{key.Subject.Value}:placement" },
        Subject = key.Subject,
        Evidence = evidence,
        Artifact = key,
        Placement = new() { Module = "NativeQueries", Features = ["Parameters"], Slice = "Search", SliceKind = GenerationSliceKind.StateView }
    };

    sealed record QueryVector(ArtifactDefinition Expected, GeneratedScreenplayDefinition Forward, GeneratedScreenplayDefinition Reverse, GeneratedScreenplayDefinition Legacy, IReadOnlyList<GenerationFact> Facts);
}
