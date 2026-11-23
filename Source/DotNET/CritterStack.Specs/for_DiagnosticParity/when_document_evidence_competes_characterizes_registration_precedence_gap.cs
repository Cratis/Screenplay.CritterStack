// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Cratis.CritterStack.Screenplay.for_DiagnosticParity;

/// <summary>
/// Compatibility keeps a document's first use unless identity is configured. Neutral preference ranks
/// explicit registration above a use site. Pin that source-location difference rather than changing it.
/// </summary>
public class when_document_evidence_competes_characterizes_registration_precedence_gap : Specification
{
    const string Source =
        """
        namespace Diagnostics;
        public class Alpha { public int Id { get; set; } }
        public class Beta { public int Id { get; set; } }
        public static class Access
        {
            public static void Save(Marten.IDocumentSession session)
            {
                session.Store(new Alpha());
                session.Store(new Beta());
            }
        }
        public static class Configuration
        {
            public static void Configure(Marten.StoreOptions options)
            {
                options.Schema.For<Alpha>();
                options.Schema.For<Alpha>().Identity(value => value.Id);
                options.Schema.For<Beta>();
            }
        }
        """;

    GeneratedScreenplayDefinition _compatibility = null!;
    GeneratedScreenplayDefinition _forward = null!;
    GeneratedScreenplayDefinition _reverse = null!;

    void Because()
    {
        var project = given.diagnostic_parity.ProjectWithSource(typeof(given.a_marten_document_application), Source);
        project.Compilation.GetDiagnostics().Where(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error).ShouldBeEmpty();
        var contribution = new CritterStackScreenplayAdapter().Analyze(new DotNetAnalysisContext([project]), new DotNetAdapterOptions());
        _compatibility = new CritterStackScreenplayGenerator().Generate([project], new CritterStackScreenplayOptions { Domain = "Diagnostics" });
        var candidates = new List<AdapterContribution>();
        var invocations = project.AuthoredSyntaxTrees.SelectMany(tree => tree.GetRoot().DescendantNodes().OfType<InvocationExpressionSyntax>()).ToArray();
        foreach (var document in contribution.Facts.OfType<ArtifactFact>().Where(fact => fact.Definition.Key.Kind == ArtifactKind.Document))
        {
            var warning = contribution.Diagnostics.Single(diagnostic => diagnostic.Code == MartenDiagnosticCodes.DocumentModelOmitted && diagnostic.Subject == document.Subject);
            foreach (var invocation in invocations.Where(invocation => invocation.ToString().Contains(document.Definition.Name, StringComparison.Ordinal)))
            {
                var source = CritterStackSource.RangeForProject(invocation.GetLocation(), project);
                candidates.Add(Observation(document, warning with { Source = source }, invocation));
            }
        }
        var declarations = contribution with { Diagnostics = [.. contribution.Diagnostics.Where(diagnostic => diagnostic.Code != MartenDiagnosticCodes.DocumentModelOmitted)] };
        _forward = given.diagnostic_parity.Generate([declarations, .. candidates]);
        _reverse = given.diagnostic_parity.Generate([.. candidates.AsEnumerable().Reverse(), declarations]);
    }

    [Fact] void should_keep_both_candidates_verified() => (_compatibility.IsSuccess && _forward.IsSuccess).ShouldBeTrue();
    [Fact] void should_keep_one_warning_per_document_in_both_pipelines() => new[] { _compatibility, _forward, _reverse }.All(result => Warnings(result).Count() == 2).ShouldBeTrue();
    [Fact] void should_keep_identity_configuration_as_the_alpha_winner() => Warning(_forward, "Alpha").Source.ShouldEqual(Warning(_compatibility, "Alpha").Source);
    [Fact] void should_locate_the_compatibility_beta_warning_at_its_first_store() => Warning(_compatibility, "Beta").Source.StartLine.ShouldEqual(9);
    [Fact] void should_characterize_the_neutral_beta_warning_at_explicit_registration() => Warning(_forward, "Beta").Source.StartLine.ShouldEqual(18);
    [Fact] void should_preserve_every_beta_payload_field_except_the_characterized_location() => given.diagnostic_parity.Meaning([Warning(_forward, "Beta") with { Source = Warning(_compatibility, "Beta").Source }]).ShouldEqual(given.diagnostic_parity.Meaning([Warning(_compatibility, "Beta")]));
    [Fact] void should_resolve_the_same_winners_in_reverse_order() => given.diagnostic_parity.Meaning(_reverse.Diagnostics).ShouldEqual(given.diagnostic_parity.Meaning(_forward.Diagnostics));
    [Fact] void should_not_change_source_or_create_a_read_model() => _forward.Source.ShouldEqual(_compatibility.Source);

    static IEnumerable<GenerationDiagnostic> Warnings(GeneratedScreenplayDefinition result) => result.Diagnostics.Where(diagnostic => diagnostic.Code == MartenDiagnosticCodes.DocumentModelOmitted);
    static GenerationDiagnostic Warning(GeneratedScreenplayDefinition result, string document) => Warnings(result).Single(diagnostic => diagnostic.Message.Contains($"'{document}'", StringComparison.Ordinal));

    static OmissionDiagnosticEvidencePreference PreferenceFor(string name)
    {
        if (name == "Identity") return OmissionDiagnosticEvidencePreference.IdentityConfiguration;
        if (name == "For") return OmissionDiagnosticEvidencePreference.ExplicitRegistration;
        return OmissionDiagnosticEvidencePreference.UseSite;
    }

    static AdapterContribution Observation(ArtifactFact document, GenerationDiagnostic diagnostic, InvocationExpressionSyntax invocation)
    {
        var owner = $"observation-{invocation.SpanStart}-{invocation.Span.Length}";
        var adapter = given.diagnostic_parity.Identity(owner);
        var declaration = new ArtifactDeclarationFact
        {
            Id = new FactId { Value = $"{owner}:document" }, Subject = document.Subject,
            Evidence = given.diagnostic_parity.Evidence(adapter, diagnostic.Source),
            Definition = new ArtifactDeclarationDefinition { Artifact = document.Definition.Key, Name = document.Definition.Name }
        };
        var name = ((MemberAccessExpressionSyntax)invocation.Expression).Name.Identifier.ValueText;
        var preference = PreferenceFor(name);
        return new AdapterContribution
        {
            Adapter = adapter,
            Facts =
            [
                declaration,
                new OccurrenceOmissionDiagnosticFact
                {
                    Id = new FactId { Value = $"{owner}:omission" }, Subject = document.Subject, Evidence = declaration.Evidence,
                    Definition = new OccurrenceOmissionDiagnosticDefinition
                    {
                        Family = new DiagnosticFamilyId { Value = diagnostic.Code }, Occurrence = document.Definition.Key,
                        Preference = preference, Policy = OccurrenceOmissionDiagnosticPolicy.OnePerOccurrence,
                        Diagnostic = diagnostic with { Facts = [declaration.Id] }
                    }
                }
            ]
        };
    }
}
