// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Cratis.CritterStack.Screenplay.for_DiagnosticParity;

/// <summary>
/// Failure-only document candidates and warnings disappear together; an ordinary observation keeps both.
/// </summary>
public class when_document_diagnostics_depend_on_success_support : given.a_wolverine_attribute_middleware_application
{
    const string Source =
        """
        namespace Diagnostics;
        public record Register(System.Guid Id);
        public class Audit { public System.Guid Id { get; set; } }
        public class RegisterHandler
        {
            public static void Handle(Register message ORDINARY_PARAMETERS) { ORDINARY }
            public static void OnException(System.InvalidOperationException error, Register message, Marten.IDocumentSession session) => session.Store(new Audit { Id = message.Id });
        }
        """;

    readonly List<(bool HasOrdinarySupport, GeneratedScreenplayDefinition Compatibility, GeneratedScreenplayDefinition Forward, GeneratedScreenplayDefinition Reverse)> _vectors = [];

    void Because()
    {
        Add(false);
        Add(true);
    }

    [Fact] void should_exercise_failure_only_and_mixed_support() => _vectors.Count.ShouldEqual(2);
    [Fact] void should_keep_all_candidates_verified() => _vectors.TrueForAll(vector => vector.Compatibility.IsSuccess && vector.Forward.IsSuccess && vector.Reverse.IsSuccess).ShouldBeTrue();

    [Fact]
    void should_match_diagnostic_payloads_and_counts_in_either_order()
    {
        foreach (var vector in _vectors)
        {
            given.diagnostic_parity.Meaning(vector.Forward.Diagnostics).ShouldEqual(given.diagnostic_parity.Meaning(vector.Compatibility.Diagnostics));
            given.diagnostic_parity.Meaning(vector.Reverse.Diagnostics).ShouldEqual(given.diagnostic_parity.Meaning(vector.Compatibility.Diagnostics));
        }
    }

    [Fact]
    void should_keep_document_warnings_only_with_an_ordinary_observation()
    {
        foreach (var vector in _vectors)
        {
            vector.Compatibility.Diagnostics.Count(diagnostic => diagnostic.Code == MartenDiagnosticCodes.DocumentModelOmitted).ShouldEqual(vector.HasOrdinarySupport ? 1 : 0);
            vector.Forward.Diagnostics.Count(diagnostic => diagnostic.Code == MartenDiagnosticCodes.DocumentModelOmitted).ShouldEqual(vector.HasOrdinarySupport ? 1 : 0);
            vector.Forward.Graph.Artifacts.Count(artifact => artifact.Key.Kind == ArtifactKind.Document).ShouldEqual(vector.HasOrdinarySupport ? 1 : 0);
        }
    }

    [Fact]
    void should_preserve_the_one_failure_warning_without_success_consequences()
    {
        foreach (var vector in _vectors)
        {
            vector.Forward.Diagnostics.Count(diagnostic => diagnostic.Code == WolverineDiagnosticCodes.FailureStageConditionOmitted).ShouldEqual(1);
            vector.Forward.Source.ShouldEqual(vector.Compatibility.Source);
            vector.Reverse.Source.ShouldEqual(vector.Compatibility.Source);
            vector.Forward.Graph.Relationships.Any(relationship => relationship.Key.Kind == RelationshipKind.Cascades).ShouldBeFalse();
        }
    }

    void Add(bool ordinary)
    {
        var source = Source.Replace("ORDINARY_PARAMETERS", ordinary ? ", Marten.IDocumentSession session" : string.Empty, StringComparison.Ordinal)
            .Replace("ORDINARY", ordinary ? "session.Store(new Audit { Id = message.Id });" : string.Empty, StringComparison.Ordinal);
        var project = CreateProject(source, "Diagnostics");
        project.Compilation.GetDiagnostics().Where(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error).ShouldBeEmpty();
        var context = new DotNetAnalysisContext([project]);
        var combined = Analyze(project);
        var marten = new MartenScreenplayAdapter().Analyze(context, new DotNetAdapterOptions());
        var methods = project.Compilation.GetTypeByMetadataName("Diagnostics.RegisterHandler").GetMembers().OfType<IMethodSymbol>().Where(method => method.Name == "Handle" || method.Name == "OnException").ToArray();
        var success = DotNetMethodIdentity.SubjectFor(project, methods.Single(method => method.Name == "Handle"));
        var failure = DotNetMethodIdentity.SubjectFor(project, methods.Single(method => method.Name == "OnException"));
        var candidates = marten.Facts.Select(fact => fact.Id).ToHashSet();
        var remaining = combined with
        {
            Facts = [.. combined.Facts.Where(fact => !candidates.Contains(fact.Id))],
            Diagnostics = [.. combined.Diagnostics.Where(diagnostic => diagnostic.Code != MartenDiagnosticCodes.DocumentModelOmitted)]
        };
        var owner = marten.Adapter;
        var evidence = given.diagnostic_parity.Evidence(owner);
        var facts = new List<GenerationFact>(marten.Facts);
        foreach (var candidate in marten.Facts)
        {
            facts.Add(Support(candidate, Supporters(candidate, ordinary, success, failure), evidence));
        }
        facts.Add(new MethodExecutionClassificationFact
        {
            Id = new FactId { Value = "document-support:failure" },
            Subject = failure,
            Evidence = evidence,
            Definition = new MethodExecutionClassificationDefinition
            {
                Method = failure,
                Kind = MethodExecutionKind.FailureOnly,
                OmissionReason = combined.Diagnostics.Single(diagnostic => diagnostic.Code == WolverineDiagnosticCodes.FailureStageConditionOmitted)
            }
        });
        AddOmissionObservations(project, marten, evidence, facts);
        var neutral = new AdapterContribution { Adapter = owner, Facts = facts };
        var compatibility = new CritterStackScreenplayGenerator().Generate([project], new CritterStackScreenplayOptions { Domain = "Diagnostics" });
        _vectors.Add((ordinary, compatibility, given.diagnostic_parity.Generate(remaining, neutral), given.diagnostic_parity.Generate(neutral, remaining)));
    }

    static SubjectId[] Supporters(GenerationFact candidate, bool ordinary, SubjectId success, SubjectId failure)
    {
        if (candidate is not ArtifactFact { Definition.Key.Kind: ArtifactKind.Document }) return [candidate.Subject];
        return ordinary ? [success, failure] : [failure];
    }

    static void AddOmissionObservations(DotNetProjectCompilation project, AdapterContribution marten, Evidence evidence, List<GenerationFact> facts)
    {
        var documentFact = marten.Facts.OfType<ArtifactFact>().Single(fact => fact.Definition.Key.Kind == ArtifactKind.Document);
        var document = documentFact.Definition.Key;
        var warning = marten.Diagnostics.Single(diagnostic => diagnostic.Code == MartenDiagnosticCodes.DocumentModelOmitted);
        foreach (var invocation in project.AuthoredSyntaxTrees.SelectMany(tree => tree.GetRoot().DescendantNodes().OfType<InvocationExpressionSyntax>()))
        {
            var method = (IMethodSymbol)project.Compilation.GetSemanticModel(invocation.SyntaxTree).GetEnclosingSymbol(invocation.SpanStart);
            var subject = DotNetMethodIdentity.SubjectFor(project, method);
            var candidate = new OccurrenceOmissionDiagnosticFact
            {
                Id = new FactId { Value = $"{evidence.Adapter.Id}:warning:{invocation.SpanStart}" },
                Subject = document.Subject,
                Evidence = evidence with { Source = CritterStackSource.RangeForProject(invocation.GetLocation(), project) },
                Definition = new OccurrenceOmissionDiagnosticDefinition
                {
                    Family = new DiagnosticFamilyId { Value = warning.Code },
                    Occurrence = document,
                    Policy = OccurrenceOmissionDiagnosticPolicy.OnePerOccurrence,
                    Preference = OmissionDiagnosticEvidencePreference.UseSite,
                    Diagnostic = warning with { Source = CritterStackSource.RangeForProject(invocation.GetLocation(), project), Facts = [documentFact.Id] }
                }
            };
            facts.Add(candidate);
            facts.Add(Support(candidate, [subject], evidence));
        }
    }

    static MethodFactSupportFact Support(GenerationFact candidate, SubjectId[] methods, Evidence evidence) => new()
    {
        Id = new FactId { Value = $"document-support:{candidate.Id.Value}" },
        Subject = candidate.Subject,
        Evidence = evidence,
        Definition = new MethodFactSupportDefinition
        {
            Candidate = candidate.Id,
            Alternatives = [.. methods.Select(method => new MethodFactSupportAlternative { Method = method })]
        }
    };
}
