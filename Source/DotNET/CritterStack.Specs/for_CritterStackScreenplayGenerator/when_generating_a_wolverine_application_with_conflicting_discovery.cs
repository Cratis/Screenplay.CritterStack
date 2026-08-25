// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Cratis.CritterStack.Screenplay.for_CritterStackScreenplayGenerator;

public class when_generating_a_wolverine_application_with_conflicting_discovery : given.a_wolverine_configured_discovery_application
{
    GeneratedScreenplayDefinition _result = null!;

    void Because() => _result = new CritterStackScreenplayGenerator().Generate(
        [ConflictingProject],
        new CritterStackScreenplayOptions { Domain = "ConflictingDiscovery" });

    [Fact] void should_compile_the_fixture() => ConflictingProject.Compilation.GetDiagnostics().Where(_ => _.Severity == Microsoft.CodeAnalysis.DiagnosticSeverity.Error).ShouldBeEmpty();
    [Fact] void should_succeed() => _result.IsSuccess.ShouldBeTrue();
    [Fact] void should_report_one_discovery_conflict() => ConflictDiagnostics.Count.ShouldEqual(1);
    [Fact] void should_classify_the_conflict() => ConflictDiagnostics.Single().Outcome.ShouldEqual(GenerationDiagnosticOutcome.Conflict);
    [Fact] void should_not_guess_conventional_discovery_state() => _result.Graph.Artifacts.Any(_ => _.Key.Subject.Value.EndsWith("/ConflictingDiscovery.ConventionalTrigger", StringComparison.Ordinal)).ShouldBeFalse();

    IReadOnlyList<GenerationDiagnostic> ConflictDiagnostics =>
        [.. _result.Diagnostics.Where(_ => _.Code == WolverineDiagnosticCodes.HandlerDiscoveryConfigurationUnresolved)];
}
