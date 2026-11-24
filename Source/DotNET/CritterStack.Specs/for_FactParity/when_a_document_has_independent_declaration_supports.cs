// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using static Cratis.CritterStack.Screenplay.for_FactParity.given.fact_vectors;

namespace Cratis.CritterStack.Screenplay.for_FactParity;

/// <summary>One failed use must not erase a declaration, its member types, or roles supported by another use.</summary>
public class when_a_document_has_independent_declaration_supports : Specification
{
    ArtifactKey _key = null!;
    ArtifactDefinition _compatibility = null!;
    GeneratedScreenplayDefinition _forward = null!;
    GeneratedScreenplayDefinition _reverse = null!;
    GeneratedScreenplayDefinition _unsupported = null!;
    AdapterContribution _observations = null!;

    void Because()
    {
        var project = Project(Source);
        var document = Type(project, "FactParity.Document");
        _key = Key(project, document, ArtifactKind.Document);
        var handler = Type(project, "FactParity.SaveHandler");
        var success = handler.GetMembers("Handle").OfType<IMethodSymbol>().Single();
        var failure = handler.GetMembers("OnException").OfType<IMethodSymbol>().Single();
        var alternatives = new[] { success, failure }.Select(method => CritterStackMethodFacts.Observe(project, method)).ToArray();
        var candidates = Surface(project, document, _key, MartenAdapter).Concat([Role(_key, "id", Evidence(project, document, MartenAdapter))]).ToArray();
        _observations = Contribution(MartenAdapter, [.. candidates, .. candidates.Select(candidate => CritterStackMethodFacts.Support(candidate, alternatives))]);
        var classifier = Failure(project, failure);
        _forward = Generate(_observations, classifier);
        _reverse = Generate(classifier, _observations);
        _unsupported = Generate(_observations, classifier, Failure(project, success));
        _compatibility = CompatibilityDefinition(new CritterStackScreenplayAdapter().Analyze(new([project]), new()), _key);
    }

    [Fact] void should_verify_the_supported_declaration() => _forward.IsSuccess.ShouldBeTrue();
    [Fact] void should_verify_the_reverse_order() => _reverse.IsSuccess.ShouldBeTrue();
    [Fact] void should_preserve_the_exact_artifact_key() => Definition(_forward, _key).Key.ShouldEqual(_compatibility.Key);
    [Fact] void should_preserve_the_declaration_name() => Definition(_forward, _key).Name.ShouldEqual(_compatibility.Name);
    [Fact] void should_preserve_member_order_types_and_roles() => Members(Definition(_forward, _key)).ShouldEqual(Members(_compatibility));
    [Fact] void should_preserve_members_in_reverse_order() => Members(Definition(_reverse, _key)).ShouldEqual(Members(_compatibility));
    [Fact] void should_attach_both_methods_to_every_candidate() => _observations.Facts.OfType<MethodFactSupportFact>().All(_ => _.Definition.Alternatives.Count == 2).ShouldBeTrue();
    [Fact] void should_remove_the_declaration_when_no_support_survives() => _unsupported.Graph.Artifacts.ShouldBeEmpty();
    [Fact] void should_not_leave_orphaned_member_facts() => _unsupported.IsSuccess.ShouldBeTrue();

    const string Source = """
        namespace Wolverine { public sealed class WolverineOptions; }
        namespace Marten { public interface IDocumentSession { void Store<T>(T document); } }
        namespace FactParity
        {
            public sealed record Document(int Id, string Name);
            public sealed record Save;
            public static class SaveHandler
            {
                public static void Handle(Save message, Marten.IDocumentSession session) => session.Store(new Document(1, "saved"));
                public static void OnException(Save message, System.Exception exception, Marten.IDocumentSession session) => session.Store(new Document(1, "failed"));
            }
        }
        """;
}
