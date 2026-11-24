// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Cratis.CritterStack.Screenplay.for_DiagnosticParity;

/// <summary>
/// Method-local uncertainty is projected only with marker-local method support and matching endpoint ownership.
/// Unknown and Unsupported outcomes retain their original payloads; neither is a contract gap.
/// </summary>
public class when_compiled_query_diagnostics_are_projected_to_an_entry_point : Specification
{
    GeneratedScreenplayDefinition _compatibility = null!;
    GeneratedScreenplayDefinition _forward = null!;
    GeneratedScreenplayDefinition _reverse = null!;
    GeneratedScreenplayDefinition _unowned = null!;
    GeneratedScreenplayDefinition _wrongRole = null!;
    GeneratedScreenplayDefinition _unsupportedControl = null!;
    IReadOnlyList<GenerationDiagnostic> _methodDiagnostics = null!;
    SubjectId _method = null!;
    ArtifactKey _query = null!;

    void Because()
    {
        var project = given.diagnostic_parity.ProjectFrom(typeof(given.a_marten_compiled_query_application));
        var context = new DotNetAnalysisContext([project]);
        var contribution = new CritterStackScreenplayAdapter().Analyze(context, new DotNetAdapterOptions());
        _compatibility = new CritterStackScreenplayGenerator().Generate([project], new CritterStackScreenplayOptions { Domain = "Diagnostics" });
        var method = project.Compilation.GetTypeByMetadataName("Students.StudentEndpoints").GetMembers("Nested").OfType<IMethodSymbol>().Single();
        _method = DotNetMethodIdentity.SubjectFor(project, method);
        _query = contribution.Facts.OfType<ArtifactFact>().Single(fact => fact.Definition.Key.Kind == ArtifactKind.Query && fact.Definition.Name == "Nested").Definition.Key;
        var bridge = given.diagnostic_parity.Identity("compiled-query-bridge");
        _methodDiagnostics = MartenCompiledQueryDiscovery.Discover(method, _method, project, bridge, new CritterStackSubjectResolver(context)).Diagnostics;

        // Payload support must survive eligibility selection; classifications are provenance-only.
        var diagnostics = new AdapterContribution
        {
            Adapter = bridge,
            Facts =
            [
                new ArtifactDeclarationFact
                {
                    Id = new FactId { Value = "compiled-query-bridge:observation" }, Subject = _query.Subject,
                    Evidence = given.diagnostic_parity.Evidence(bridge),
                    Definition = new ArtifactDeclarationDefinition { Artifact = _query, Name = "Nested" }
                },
                new MethodExecutionClassificationFact
                {
                    Id = new FactId { Value = "compiled-query-bridge:method" }, Subject = _method,
                    Evidence = given.diagnostic_parity.Evidence(bridge),
                    Definition = new MethodExecutionClassificationDefinition { Method = _method, Kind = MethodExecutionKind.Ordinary }
                },
                .. _methodDiagnostics.SelectMany((diagnostic, index) => ProjectionWithSupport(diagnostic, index, bridge))
            ]
        };
        var declarations = contribution with { Diagnostics = [.. contribution.Diagnostics.Where(diagnostic => diagnostic.Code != MartenDiagnosticCodes.CompiledQueryFlowUnresolved)] };
        var entryPoint = EntryPoint(_query);
        _forward = given.diagnostic_parity.Generate(declarations, diagnostics, entryPoint);
        _reverse = given.diagnostic_parity.Generate(entryPoint, diagnostics, declarations);
        var supportedOutcome = diagnostics with
        {
            Facts = [.. diagnostics.Facts.Select(fact => fact is MethodDiagnosticProjectionFact projection
                ? projection with { Definition = projection.Definition with { Diagnostic = projection.Definition.Diagnostic with { Outcome = GenerationDiagnosticOutcome.Unsupported } } }
                : fact)]
        };
        _unsupportedControl = given.diagnostic_parity.Generate(declarations, supportedOutcome, entryPoint);
        _unowned = given.diagnostic_parity.Generate(declarations, supportedOutcome);
        _wrongRole = given.diagnostic_parity.Generate(declarations, supportedOutcome, EntryPoint(_query with { Kind = ArtifactKind.Command }));
    }

    [Fact] void should_discover_two_method_local_uncertainties() => _methodDiagnostics.Count.ShouldEqual(2);
    [Fact] void should_start_with_the_exact_method_subject() => _methodDiagnostics.All(diagnostic => diagnostic.Subject == _method).ShouldBeTrue();
    [Fact] void should_keep_both_unknown_compatibility_warnings() => _compatibility.Diagnostics.Count(diagnostic => diagnostic.Code == MartenDiagnosticCodes.CompiledQueryFlowUnresolved && diagnostic.Outcome == GenerationDiagnosticOutcome.Unknown).ShouldEqual(2);
    [Fact] void should_project_both_unknown_templates() => _forward.Diagnostics.Count(diagnostic => diagnostic.Code == MartenDiagnosticCodes.CompiledQueryFlowUnresolved).ShouldEqual(2);
    [Fact] void should_accept_the_complete_projection_metadata() => _forward.Diagnostics.Any(diagnostic => diagnostic.Code == "GEN0080").ShouldBeFalse();
    [Fact] void should_preserve_the_complete_compatibility_payloads_and_counts() => given.diagnostic_parity.Meaning(_forward.Diagnostics).ShouldEqual(given.diagnostic_parity.Meaning(_compatibility.Diagnostics));
    [Fact] void should_not_depend_on_contribution_order() => given.diagnostic_parity.Meaning(_reverse.Diagnostics).ShouldEqual(given.diagnostic_parity.Meaning(_forward.Diagnostics));
    [Fact] void should_accept_both_otherwise_identical_unsupported_templates() => _unsupportedControl.Diagnostics.Count(diagnostic => diagnostic.Code == MartenDiagnosticCodes.CompiledQueryFlowUnresolved).ShouldEqual(2);
    [Fact] void should_project_the_control_to_the_query_subject() => _unsupportedControl.Diagnostics.Where(diagnostic => diagnostic.Code == MartenDiagnosticCodes.CompiledQueryFlowUnresolved).All(diagnostic => diagnostic.Subject == _query.Subject).ShouldBeTrue();
    [Fact] void should_preserve_the_control_payload_except_for_its_deliberately_changed_outcome() => given.diagnostic_parity.Meaning(_unsupportedControl.Diagnostics).ShouldEqual(given.diagnostic_parity.Meaning(_compatibility.Diagnostics.Select(diagnostic => diagnostic.Code == MartenDiagnosticCodes.CompiledQueryFlowUnresolved ? diagnostic with { Outcome = GenerationDiagnosticOutcome.Unsupported } : diagnostic)));
    [Fact] void should_not_emit_a_warning_without_entry_point_ownership() => _unowned.Diagnostics.Any(diagnostic => diagnostic.Code == MartenDiagnosticCodes.CompiledQueryFlowUnresolved).ShouldBeFalse();
    [Fact] void should_not_project_a_query_warning_onto_a_command() => _wrongRole.Diagnostics.Any(diagnostic => diagnostic.Code == MartenDiagnosticCodes.CompiledQueryFlowUnresolved).ShouldBeFalse();
    [Fact] void should_preserve_verification() => _forward.IsSuccess.ShouldEqual(_compatibility.IsSuccess);
    [Fact] void should_preserve_generated_source() => _forward.Source.ShouldEqual(_compatibility.Source);

    [Fact] void should_explain_both_unowned_diagnostics_without_invalidating_the_templates() => _unowned.Diagnostics.Count(diagnostic => diagnostic.Code == "GEN0081").ShouldEqual(2);
    [Fact] void should_explain_both_role_mismatches_without_invalidating_the_templates() => _wrongRole.Diagnostics.Count(diagnostic => diagnostic.Code == "GEN0081").ShouldEqual(2);

    IEnumerable<GenerationFact> ProjectionWithSupport(GenerationDiagnostic diagnostic, int index, AdapterIdentity bridge)
    {
        var marker = new MethodDiagnosticProjectionFact
        {
            Id = new FactId { Value = $"compiled-query-bridge:warning:{index}" },
            Subject = _method,
            Evidence = given.diagnostic_parity.Evidence(bridge, diagnostic.Source),
            Definition = new MethodDiagnosticProjectionDefinition
            {
                Method = _method,
                EntryPointRole = ArtifactKind.Query,
                Mode = MethodDiagnosticProjectionMode.EntryPointArtifact,
                Diagnostic = diagnostic with { Facts = [new FactId { Value = "compiled-query-bridge:observation" }] }
            }
        };
        yield return marker;
        yield return new MethodFactSupportFact
        {
            Id = new FactId { Value = $"compiled-query-bridge:support:{index}" },
            Subject = _method,
            Evidence = marker.Evidence,
            Definition = new MethodFactSupportDefinition
            {
                Candidate = marker.Id,
                Alternatives = [new MethodFactSupportAlternative { Method = _method }]
            }
        };
    }

    AdapterContribution EntryPoint(ArtifactKey artifact)
    {
        var adapter = given.diagnostic_parity.Identity("endpoint-owner");
        return new AdapterContribution
        {
            Adapter = adapter,
            Facts =
            [
                new EntryPointFact
                {
                    Id = new FactId { Value = "endpoint-owner:nested" }, Subject = _method,
                    Evidence = given.diagnostic_parity.Evidence(adapter),
                    Definition = new EntryPointDefinition { Method = _method, Artifact = artifact }
                }
            ]
        };
    }
}
