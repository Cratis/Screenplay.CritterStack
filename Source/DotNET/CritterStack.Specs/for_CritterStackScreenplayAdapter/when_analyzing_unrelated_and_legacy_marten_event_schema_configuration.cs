// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Cratis.CritterStack.Screenplay.for_CritterStackScreenplayAdapter;

public class when_analyzing_unrelated_and_legacy_marten_event_schema_configuration : given.a_marten_event_schema_configuration_application
{
    [Fact] void should_ignore_unrelated_same_named_apis() => ConfigurationDiagnostics.Any(_ => _.Message.Contains("unrelated", StringComparison.OrdinalIgnoreCase)).ShouldBeFalse();
    [Fact] void should_ignore_add_event_type_registrations() => EventConfiguration.Any(_ => _.Message.Contains("AddedOnly", StringComparison.Ordinal)).ShouldBeFalse();
    [Fact] void should_ignore_alias_less_marten_event_attributes() => EventConfiguration.Any(_ => _.Message.Contains("AttributeWithoutAlias", StringComparison.Ordinal)).ShouldBeFalse();
    [Fact] void should_ignore_the_unverified_legacy_event_mapping_api() => EventConfiguration.Any(_ => _.Message.Contains("LegacyExcluded", StringComparison.Ordinal)).ShouldBeFalse();
    [Fact] void should_not_interpret_arbitrary_upcaster_implementations() => UpcastConfiguration.Any(_ => _.Message.Contains("ArbitraryUpcaster", StringComparison.Ordinal)).ShouldBeFalse();

    IReadOnlyList<GenerationDiagnostic> ConfigurationDiagnostics => [.. EventConfiguration.Concat(UpcastConfiguration)];
    IReadOnlyList<GenerationDiagnostic> EventConfiguration => [.. Contribution.Diagnostics.Where(_ => _.Code == MartenDiagnosticCodes.EventTypeConfigurationOmitted)];
    IReadOnlyList<GenerationDiagnostic> UpcastConfiguration => [.. Contribution.Diagnostics.Where(_ => _.Code == MartenDiagnosticCodes.EventUpcastConfigurationOmitted)];
}
