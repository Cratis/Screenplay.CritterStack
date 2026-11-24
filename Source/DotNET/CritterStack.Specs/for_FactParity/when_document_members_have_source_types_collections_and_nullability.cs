// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using static Cratis.CritterStack.Screenplay.for_FactParity.given.fact_vectors;

namespace Cratis.CritterStack.Screenplay.for_FactParity;

/// <summary>Compares the entire typed property surface, not a text snapshot or just the type display names.</summary>
public class when_document_members_have_source_types_collections_and_nullability : Specification
{
    ArtifactKey _key = null!;
    ArtifactDefinition _compatibility = null!;
    GeneratedScreenplayDefinition _forward = null!;
    GeneratedScreenplayDefinition _reverse = null!;
    AdapterContribution _observations = null!;

    void Because()
    {
        var project = Project(Source);
        var type = Type(project, "FactParity.Document");
        _key = Key(project, type, ArtifactKind.Document);
        var facts = new List<GenerationFact>(Surface(project, type, _key, MartenAdapter))
        {
            Role(_key, "id", Evidence(project, type, MartenAdapter))
        };
        facts.AddRange([.. facts.OfType<ArtifactMemberTypeUseFact>().Where(_ => _.Definition.Type.ObservedTypeSubject is not null).Select(typeUse => new TypeUseBindingPolicyFact
        {
            Id = new() { Value = $"{typeUse.Id.Value}:source-fallback" }, Subject = typeUse.Subject, Evidence = typeUse.Evidence,
            Definition = new() { TypeUse = typeUse.Id, Policy = TypeUseBindingPolicyKind.ConceptOnlyWithUnboundFallback }
        })]);
        _observations = Contribution(MartenAdapter, facts);
        var peer = Contribution(WolverineAdapter, []);
        _forward = Generate(_observations, peer);
        _reverse = Generate(peer, _observations);
        _compatibility = CompatibilityDefinition(new CritterStackScreenplayAdapter().Analyze(new([project]), new()), _key);
    }

    [Fact] void should_preserve_the_complete_typed_surface() => Members(Definition(_forward, _key)).ShouldEqual(Members(_compatibility));
    [Fact] void should_preserve_the_reverse_order_typed_surface() => Members(Definition(_reverse, _key)).ShouldEqual(Members(_compatibility));
    [Fact] void should_preserve_the_authored_member_order() => Definition(_forward, _key).Properties.Select(_ => _.Name).ShouldEqualInOrder("id", "details", "tags", "count", "amount");
    [Fact] void should_retain_an_exact_source_subject_before_binding() => _observations.Facts.OfType<ArtifactMemberTypeUseFact>().Single(_ => _.Definition.Member.Name == "details").Definition.Type.ObservedTypeSubject.ShouldNotBeNull();
    [Fact] void should_not_invent_a_concept_for_the_plain_source_type() => _forward.Source.ShouldNotContain("concept Details");
    [Fact] void should_verify_both_atomic_orders() => _forward.Diagnostics.Concat(_reverse.Diagnostics).Where(_ => _.Severity == GenerationDiagnosticSeverity.Error).Select(_ => $"{_.Code}: {_.Message}").ShouldBeEmpty();

    const string Source = """
        namespace Wolverine { public sealed class WolverineOptions; }
        namespace Marten { public interface IDocumentSession { void Store<T>(T document); } }
        namespace FactParity
        {
            public sealed record Details(string Description);
            public sealed record Document(int Id, Details Details, string[] Tags, int? Count, decimal Amount);
            public sealed record Save;
            public static class SaveHandler
            {
                public static void Handle(Save message, Marten.IDocumentSession session) => session.Store(new Document(1, new Details("saved"), [], null, 0));
            }
        }
        """;
}
