// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Text.Json;
using static Cratis.CritterStack.Screenplay.for_FactParity.given.fact_vectors;
using application = Cratis.CritterStack.Screenplay.given.a_marten_compiled_query_application;
using diagnostic_parity = Cratis.CritterStack.Screenplay.given.diagnostic_parity;

namespace Cratis.CritterStack.Screenplay.for_FactParity;

public class when_compiled_query_source_flows_are_composed : Specification
{
    AdapterContribution _marten = null!;
    GeneratedScreenplayDefinition _expected = null!;
    GeneratedScreenplayDefinition _actual = null!;
    GeneratedScreenplayDefinition _reverse = null!;

    void Because()
    {
        var fixture = diagnostic_parity.ProjectFrom(typeof(application));

        // Give the unused local-function method group a delegate type so the shared source compiles.
        var project = Project([.. fixture.AuthoredSyntaxTrees.Select(tree => CSharpSyntaxTree.ParseText(
            tree.GetText().ToString().Replace("_ = Local;", "_ = (System.Func<System.Threading.Tasks.Task<Student>>)Local;", StringComparison.Ordinal),
            path: tree.FilePath))]);
        var contributions = given.native_query_contributions.Observe(project);
        _marten = contributions.Marten;
        _expected = Generate(contributions.Compatibility);
        _actual = Generate(_marten, contributions.Wolverine);
        _reverse = Generate(contributions.Wolverine, _marten);
    }

    [Fact] void should_preserve_all_artifact_fields() => Artifacts(_actual).ShouldEqual(Artifacts(_expected));
    [Fact] void should_preserve_all_artifact_fields_in_reverse_order() => Artifacts(_reverse).ShouldEqual(Artifacts(_expected));
    [Fact] void should_preserve_all_relationship_fields() => Relationships(_actual).ShouldEqual(Relationships(_expected));
    [Fact] void should_preserve_all_relationship_fields_in_reverse_order() => Relationships(_reverse).ShouldEqual(Relationships(_expected));
    [Fact] void should_preserve_complete_diagnostic_meaning() => diagnostic_parity.Meaning(_actual.Diagnostics).ShouldEqual(diagnostic_parity.Meaning(_expected.Diagnostics));
    [Fact] void should_preserve_complete_diagnostic_meaning_in_reverse_order() => diagnostic_parity.Meaning(_reverse.Diagnostics).ShouldEqual(diagnostic_parity.Meaning(_expected.Diagnostics));
    [Fact] void should_generate_identical_source() => _actual.Source.ShouldEqual(_expected.Source);
    [Fact] void should_generate_identical_source_in_reverse_order() => _reverse.Source.ShouldEqual(_expected.Source);
    [Fact] void should_verify_all_candidates() => new[] { _expected, _actual, _reverse }.ShouldEachConformTo(result => result.IsSuccess);
    [Fact] void should_keep_two_dead_nested_flow_observations() => _marten.Facts.OfType<MethodDiagnosticProjectionFact>().Count().ShouldEqual(2);
    [Fact] void should_keep_two_located_compatibility_flow_diagnostics() => _expected.Diagnostics.Count(diagnostic => diagnostic.Code == MartenDiagnosticCodes.CompiledQueryFlowUnresolved && diagnostic.Source is not null).ShouldEqual(2);
    [Fact] void should_retain_both_document_declarations_of_the_multi_document_plan() => _marten.Facts.OfType<ArtifactUsageDeclarationFact>().Where(fact => fact.Definition.Declaration.Value.Contains("MultiDocumentPlan", StringComparison.Ordinal)).Select(fact => fact.Definition.Artifact.Subject).Distinct().Count().ShouldEqual(2);
    [Fact] void should_project_each_unproven_diagnostic_to_the_query() => _actual.Diagnostics.Where(diagnostic => diagnostic.Code == MartenDiagnosticCodes.CompiledQueryFlowUnresolved).Select(diagnostic => diagnostic.Subject).ShouldEachConformTo(subject => _actual.Graph.Artifacts.Any(artifact => artifact.Key.Kind == ArtifactKind.Query && artifact.Key.Subject == subject));

    static string Artifacts(GeneratedScreenplayDefinition result) => JsonSerializer.Serialize(result.Graph.Artifacts.SelectMany(artifact => artifact.Variants).Select(variant => variant.Definition).ToArray());
    static string Relationships(GeneratedScreenplayDefinition result) => JsonSerializer.Serialize(result.Graph.Relationships.SelectMany(relationship => relationship.Definitions).ToArray());
}
