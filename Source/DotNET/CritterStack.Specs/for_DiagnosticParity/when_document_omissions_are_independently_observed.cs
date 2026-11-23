// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Cratis.CritterStack.Screenplay.for_DiagnosticParity;

/// <summary>
/// Document omissions are occurrence-keyed, not one warning per independent observer.
/// </summary>
public class when_document_omissions_are_independently_observed : given.a_wolverine_attribute_middleware_application
{
    const string Source =
        """
        namespace Diagnostics;
        public record Process(System.Guid Id);
        public class Ledger { public System.Guid Id { get; set; } }
        public static class ProcessHandler
        {
            public static void Handle(Process message, Marten.IDocumentSession session) => session.Store(new Ledger { Id = message.Id });
        }
        public static class OtherAccess
        {
            public static void Save(Marten.IDocumentSession session) => session.Store(new Ledger());
        }
        """;

    GeneratedScreenplayDefinition _compatibility = null!;
    GeneratedScreenplayDefinition _forward = null!;
    GeneratedScreenplayDefinition _reverse = null!;
    GeneratedScreenplayDefinition _duplicated = null!;

    void Because()
    {
        var project = CreateProject(Source, "Diagnostics");
        project.Compilation.GetDiagnostics().Where(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error).ShouldBeEmpty();
        var contribution = Analyze(project);
        _compatibility = new CritterStackScreenplayGenerator().Generate([project], new CritterStackScreenplayOptions { Domain = "Diagnostics" });
        var diagnostic = contribution.Diagnostics.Single(item => item.Code == MartenDiagnosticCodes.DocumentModelOmitted);
        var occurrence = contribution.Facts.OfType<ArtifactFact>().Single(fact => fact.Definition.Key.Kind == ArtifactKind.Document).Definition.Key;
        var invocations = project.AuthoredSyntaxTrees.SelectMany(tree => tree.GetRoot().DescendantNodes().OfType<InvocationExpressionSyntax>()).ToArray();
        invocations.Length.ShouldEqual(2);
        var first = Observation("marten-document", occurrence, diagnostic with { Source = CritterStackSource.RangeForProject(invocations[0].GetLocation(), project) });
        var second = Observation("integration-document", occurrence, diagnostic with { Source = CritterStackSource.RangeForProject(invocations[1].GetLocation(), project) });
        var declarations = contribution with { Diagnostics = [.. contribution.Diagnostics.Where(item => item.Code != MartenDiagnosticCodes.DocumentModelOmitted)] };
        _forward = given.diagnostic_parity.Generate(declarations, first, second);
        _reverse = given.diagnostic_parity.Generate(second, first, declarations);
        _duplicated = given.diagnostic_parity.Generate(first, declarations, first, second, second);
    }

    [Fact] void should_keep_the_compatibility_candidate_verified() => _compatibility.IsSuccess.ShouldBeTrue();
    [Fact] void should_keep_the_neutral_candidate_verified() => _forward.IsSuccess.ShouldBeTrue();
    [Fact] void should_have_one_located_compatibility_omission() => _compatibility.Diagnostics.Single(item => item.Code == MartenDiagnosticCodes.DocumentModelOmitted).Source.ShouldNotBeNull();
    [Fact] void should_keep_exactly_one_neutral_omission() => _forward.Diagnostics.Count(item => item.Code == MartenDiagnosticCodes.DocumentModelOmitted).ShouldEqual(1);
    [Fact] void should_preserve_the_entire_compatibility_diagnostic() => given.diagnostic_parity.Meaning(_forward.Diagnostics).ShouldEqual(given.diagnostic_parity.Meaning(_compatibility.Diagnostics));
    [Fact] void should_preserve_diagnostics_in_reverse_order() => given.diagnostic_parity.Meaning(_reverse.Diagnostics).ShouldEqual(given.diagnostic_parity.Meaning(_compatibility.Diagnostics));
    [Fact] void should_not_multiply_duplicate_observations() => given.diagnostic_parity.Meaning(_duplicated.Diagnostics).ShouldEqual(given.diagnostic_parity.Meaning(_compatibility.Diagnostics));
    [Fact] void should_preserve_generated_source() => _forward.Source.ShouldEqual(_compatibility.Source);

    static AdapterContribution Observation(string owner, ArtifactKey occurrence, GenerationDiagnostic diagnostic)
    {
        var adapter = given.diagnostic_parity.Identity(owner);
        return new AdapterContribution
        {
            Adapter = adapter,
            Facts =
            [
                new ArtifactDeclarationFact
                {
                    Id = new FactId { Value = $"{owner}:document" }, Subject = occurrence.Subject,
                    Evidence = given.diagnostic_parity.Evidence(adapter, diagnostic.Source),
                    Definition = new ArtifactDeclarationDefinition { Artifact = occurrence, Name = "Ledger" }
                },
                new OccurrenceOmissionDiagnosticFact
                {
                    Id = new FactId { Value = $"{owner}:document-omission" }, Subject = occurrence.Subject,
                    Evidence = given.diagnostic_parity.Evidence(adapter, diagnostic.Source),
                    Definition = new OccurrenceOmissionDiagnosticDefinition
                    {
                        Family = new DiagnosticFamilyId { Value = MartenDiagnosticCodes.DocumentModelOmitted },
                        Occurrence = occurrence, Policy = OccurrenceOmissionDiagnosticPolicy.OnePerOccurrence,
                        Preference = OmissionDiagnosticEvidencePreference.UseSite,
                        Diagnostic = diagnostic with { Facts = [new FactId { Value = $"{owner}:document" }] }
                    }
                }
            ]
        };
    }
}
