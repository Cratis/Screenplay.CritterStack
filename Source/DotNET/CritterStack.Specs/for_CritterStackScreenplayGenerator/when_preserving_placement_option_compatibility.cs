// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Cratis.CritterStack.Screenplay.for_CritterStackScreenplayGenerator;

public class when_preserving_placement_option_compatibility : given.a_shared_source_placement_application
{
    GeneratedScreenplayDefinition _compilationFacade = null!;
    AdapterContribution _directAdapter = null!;
    AdapterContribution _explicitCompatibilityAdapter = null!;
    GeneratedScreenplayDefinition _legacyProjectFacade = null!;
    AdapterContribution _mixedAdapter = null!;
    GeneratedScreenplayDefinition _mixedProjectFacade = null!;
    AdapterContribution _strictDiagnosticAdapter = null!;

    void Because()
    {
        var options = new CritterStackScreenplayOptions
        {
            Domain = "Ordering",
            Module = "Commerce",
            NamespaceSegmentsToSkip = 1
        };
        var generator = new CritterStackScreenplayGenerator();
        _compilationFacade = generator.Generate(Project.Compilation, options);
        var legacyProject = Project with { SourceContext = null };
        _legacyProjectFacade = generator.Generate([legacyProject], options);
        var adapter = new CritterStackScreenplayAdapter();
        var adapterOptions = new DotNetAdapterOptions
        {
            Module = options.Module,
            NamespaceSegmentsToSkip = options.NamespaceSegmentsToSkip
        };
        var legacyContext = new DotNetAnalysisContext([legacyProject]);
        _directAdapter = adapter.Analyze(legacyContext, adapterOptions);
        _explicitCompatibilityAdapter = adapter.AnalyzeCompatibility(legacyContext, adapterOptions);
        var missingContextProject = Project with
        {
            Name = "Legacy",
            Compilation = Project.Compilation.RemoveAllSyntaxTrees(),
            AuthoredSyntaxTrees = new HashSet<SyntaxTree>(),
            SourceContext = null
        };
        var mixedContext = new DotNetAnalysisContext([Project, missingContextProject]);
        _mixedAdapter = adapter.Analyze(mixedContext, adapterOptions);
        _mixedProjectFacade = generator.Generate([Project, missingContextProject], options);
        _strictDiagnosticAdapter = adapter.Analyze(
            new([Project]),
            adapterOptions with { FeatureRoot = "../Source" });
    }

    [Fact] void should_preserve_direct_compilation_source() => _compilationFacade.Source.ShouldEqual(_legacyProjectFacade.Source);
    [Fact] void should_preserve_byte_identical_compatibility_output() => System.Text.Encoding.UTF8.GetBytes(_compilationFacade.Source).SequenceEqual(System.Text.Encoding.UTF8.GetBytes(_legacyProjectFacade.Source)).ShouldBeTrue();
    [Fact] void should_preserve_direct_compilation_diagnostics() => DiagnosticSignatures(_compilationFacade).ShouldContainOnly(DiagnosticSignatures(_legacyProjectFacade));
    [Fact] void should_preserve_the_configured_module() => _compilationFacade.Source.ShouldContain("module Commerce");
    [Fact] void should_preserve_legacy_success() => (_compilationFacade.IsSuccess && _legacyProjectFacade.IsSuccess).ShouldBeTrue();
    [Fact] void should_preserve_direct_legacy_adapter_facts() => FactSignatures(_directAdapter).SequenceEqual(FactSignatures(_explicitCompatibilityAdapter)).ShouldBeTrue();
    [Fact] void should_preserve_direct_legacy_adapter_diagnostics() => DiagnosticSignatures(_directAdapter).SequenceEqual(DiagnosticSignatures(_explicitCompatibilityAdapter)).ShouldBeTrue();
    [Fact] void should_emit_direct_legacy_adapter_placements_without_source_context() => _directAdapter.Facts.OfType<ArtifactPlacementFact>().ShouldNotBeEmpty();
    [Fact] void should_use_strict_adapter_placement_for_a_mixed_source_context_set() => _mixedAdapter.Diagnostics.Select(_ => _.Code).ShouldContain(DotNetSourceStructureDiagnosticCodes.MissingSourceContext);
    [Fact] void should_emit_no_direct_adapter_placements_for_a_mixed_source_context_set() => _mixedAdapter.Facts.OfType<ArtifactPlacementFact>().ShouldBeEmpty();
    [Fact] void should_use_strict_generator_placement_for_a_mixed_source_context_set() => _mixedProjectFacade.Diagnostics.Select(_ => _.Code).ShouldContain(DotNetSourceStructureDiagnosticCodes.MissingSourceContext);
    [Fact] void should_fail_closed_for_a_mixed_source_context_set() => _mixedProjectFacade.Graph.Placements.ShouldBeEmpty();
    [Fact] void should_exercise_another_strict_diagnostic_for_a_context_bearing_project() => _strictDiagnosticAdapter.Diagnostics.Select(_ => _.Code).ShouldContain(DotNetSourceStructureDiagnosticCodes.InvalidPath);
    [Fact] void should_never_fall_back_for_a_context_bearing_project_with_another_strict_diagnostic() => _strictDiagnosticAdapter.Facts.OfType<ArtifactPlacementFact>().ShouldBeEmpty();

    static IReadOnlyList<string> DiagnosticSignatures(AdapterContribution result) =>
    [
        .. result.Diagnostics.Select(_ => $"{_.Code}|{_.Severity}|{_.Outcome}|{_.Subject?.Value}|{_.Source?.Path}|{_.Message}")
    ];

    static IReadOnlyList<string> DiagnosticSignatures(GeneratedScreenplayDefinition result) =>
    [
        .. result.Diagnostics.Select(_ => $"{_.Code}|{_.Severity}|{_.Outcome}|{_.Subject?.Value}|{_.Source?.Path}")
    ];

    static IReadOnlyList<string> FactSignatures(AdapterContribution result) =>
    [
        .. result.Facts.Select(_ => $"{_.GetType().FullName}|{System.Text.Json.JsonSerializer.Serialize(_, _.GetType())}")
    ];
}
