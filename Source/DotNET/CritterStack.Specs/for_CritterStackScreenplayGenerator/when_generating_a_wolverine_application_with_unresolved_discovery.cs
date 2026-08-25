// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Cratis.CritterStack.Screenplay.for_CritterStackScreenplayGenerator;

public class when_generating_a_wolverine_application_with_unresolved_discovery : given.a_wolverine_configured_discovery_application
{
    GeneratedScreenplayDefinition _result = null!;

    void Because() => _result = new CritterStackScreenplayGenerator().Generate(
        [UnresolvedProject],
        new CritterStackScreenplayOptions { Domain = "UnresolvedDiscovery" });

    [Fact] void should_compile_the_fixture() => UnresolvedProject.Compilation.GetDiagnostics().Where(_ => _.Severity == Microsoft.CodeAnalysis.DiagnosticSeverity.Error).ShouldBeEmpty();
    [Fact] void should_succeed() => _result.IsSuccess.ShouldBeTrue();
    [Fact] void should_emit_stable_diagnostics_for_the_unresolved_configuration() => _result.Diagnostics.Count(_ => _.Code == WolverineDiagnosticCodes.HandlerDiscoveryConfigurationUnresolved).ShouldEqual(2);
    [Fact] void should_classify_the_unresolved_configuration_as_unknown() => _result.Diagnostics.Where(_ => _.Code == WolverineDiagnosticCodes.HandlerDiscoveryConfigurationUnresolved).All(_ => _.Outcome == GenerationDiagnosticOutcome.Unknown).ShouldBeTrue();
    [Fact] void should_identify_the_unresolved_customization() => _result.Diagnostics.Any(_ => _.Code == WolverineDiagnosticCodes.HandlerDiscoveryConfigurationUnresolved && _.Message.Contains("CustomizeHandlerDiscovery", StringComparison.Ordinal)).ShouldBeTrue();
    [Fact] void should_identify_the_unresolved_assembly_scan() => _result.Diagnostics.Any(_ => _.Code == WolverineDiagnosticCodes.HandlerDiscoveryConfigurationUnresolved && _.Message.Contains("IncludeAssembly", StringComparison.Ordinal)).ShouldBeTrue();
    [Fact] void should_not_guess_about_conventionally_discovered_handlers() => HasMessage("ConventionalTrigger").ShouldBeFalse();
    [Fact] void should_preserve_exact_explicit_type_inclusion() => HasMessage("ExplicitTrigger").ShouldBeTrue();

    bool HasMessage(string name) => _result.Graph.Artifacts.Any(_ =>
        _.Key.Kind == ArtifactKind.Message &&
        _.Key.Subject.Value.EndsWith($"/UnresolvedDiscovery.{name}", StringComparison.Ordinal));
}
