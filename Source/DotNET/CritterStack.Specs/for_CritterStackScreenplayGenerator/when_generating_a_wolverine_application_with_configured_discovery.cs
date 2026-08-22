// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Cratis.CritterStack.Screenplay.for_CritterStackScreenplayGenerator;

public class when_generating_a_wolverine_application_with_configured_discovery : given.a_wolverine_configured_discovery_application
{
    GeneratedScreenplayDefinition _result = null!;

    void Because() => _result = new CritterStackScreenplayGenerator().Generate(
        [Project],
        new CritterStackScreenplayOptions { Domain = "ConfiguredDiscovery" });

    [Fact] void should_compile_the_fixture() => Project.Compilation.GetDiagnostics().Where(_ => _.Severity == Microsoft.CodeAnalysis.DiagnosticSeverity.Error).ShouldBeEmpty();
    [Fact] void should_succeed() => _result.IsSuccess.ShouldBeTrue();
    [Fact] void should_activate_an_included_conventional_method() => HasMessage("IncludedTrigger").ShouldBeTrue();
    [Fact] void should_activate_an_included_current_explicit_method() => HasMessage("CurrentExplicitTrigger").ShouldBeTrue();
    [Fact] void should_activate_an_included_legacy_explicit_method() => HasMessage("LegacyExplicitTrigger").ShouldBeTrue();
    [Fact] void should_disable_the_handler_suffix_convention() => HasMessage("SuppressedSuffixTrigger").ShouldBeFalse();
    [Fact] void should_disable_the_consumer_suffix_convention() => HasMessage("SuppressedConsumerTrigger").ShouldBeFalse();
    [Fact] void should_disable_the_handler_interface_convention() => HasMessage("SuppressedInterfaceTrigger").ShouldBeFalse();
    [Fact] void should_disable_the_current_type_attribute_convention() => HasMessage("SuppressedCurrentTypeTrigger").ShouldBeFalse();
    [Fact] void should_disable_the_legacy_type_attribute_convention() => HasMessage("SuppressedLegacyTypeTrigger").ShouldBeFalse();
    [Fact] void should_not_activate_an_unincluded_explicit_method() => HasMessage("SuppressedMethodTrigger").ShouldBeFalse();
    [Fact] void should_honor_the_current_type_ignore_attribute() => HasMessage("CurrentIgnoredTrigger").ShouldBeFalse();
    [Fact] void should_honor_the_legacy_type_ignore_attribute() => HasMessage("LegacyIgnoredTrigger").ShouldBeFalse();
    [Fact] void should_honor_the_current_method_ignore_attribute() => HasMessage("CurrentMethodIgnoredTrigger").ShouldBeFalse();
    [Fact] void should_honor_the_legacy_method_ignore_attribute() => HasMessage("LegacyMethodIgnoredTrigger").ShouldBeFalse();
    [Fact] void should_not_activate_compound_middleware_independently() => HasMessage("MiddlewareTrigger").ShouldBeFalse();
    [Fact] void should_resolve_inclusion_of_the_current_source_assembly() => _result.Diagnostics.Select(_ => _.Code).ShouldNotContain(WolverineDiagnosticCodes.HandlerDiscoveryConfigurationUnresolved);

    bool HasMessage(string name) => _result.Graph.Artifacts.Any(_ =>
        _.Key.Kind == ArtifactKind.Message &&
        _.Key.Subject.Value.EndsWith($"/ConfiguredDiscovery.{name}", StringComparison.Ordinal));
}
