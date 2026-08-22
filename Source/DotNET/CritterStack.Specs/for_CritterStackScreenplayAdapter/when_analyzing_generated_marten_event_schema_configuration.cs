// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Cratis.CritterStack.Screenplay.for_CritterStackScreenplayAdapter;

public class when_analyzing_generated_marten_event_schema_configuration : given.a_marten_event_schema_configuration_application
{
    [Fact] void should_not_report_generated_alias_configuration() => ConfigurationDiagnostics.Any(_ => _.Message.Contains("GeneratedAlias", StringComparison.Ordinal) || _.Message.Contains("generated-alias", StringComparison.Ordinal)).ShouldBeFalse();
    [Fact] void should_not_report_generated_upcast_configuration() => ConfigurationDiagnostics.Any(_ => _.Message.Contains("GeneratedOld", StringComparison.Ordinal) || _.Message.Contains("GeneratedNew", StringComparison.Ordinal) || _.Message.Contains("generated-upcast", StringComparison.Ordinal)).ShouldBeFalse();
    [Fact] void should_not_originate_generated_configuration_types() => Graph.Artifacts.SelectMany(_ => _.Variants).Any(_ => _.Definition.Name.StartsWith("Generated", StringComparison.Ordinal)).ShouldBeFalse();

    IReadOnlyList<GenerationDiagnostic> ConfigurationDiagnostics =>
    [
        .. Contribution.Diagnostics.Where(_ =>
            _.Code == MartenDiagnosticCodes.EventTypeConfigurationOmitted ||
            _.Code == MartenDiagnosticCodes.EventUpcastConfigurationOmitted)
    ];
}
