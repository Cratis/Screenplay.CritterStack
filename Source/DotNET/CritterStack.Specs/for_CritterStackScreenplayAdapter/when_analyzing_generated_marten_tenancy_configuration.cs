// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Cratis.CritterStack.Screenplay.for_CritterStackScreenplayAdapter;

public class when_analyzing_generated_marten_tenancy_configuration : given.a_marten_tenancy_configuration_application
{
    [Fact] void should_ignore_generated_event_tenancy() => TenancyDiagnostics.Any(_ => _.Source?.Path == "Orders/GeneratedTenancy.g.cs" && _.Message.Contains("event tenancy-style", StringComparison.Ordinal)).ShouldBeFalse();
    [Fact] void should_ignore_generated_document_tenancy() => TenancyDiagnostics.Any(_ => _.Message.Contains("GeneratedDocument", StringComparison.Ordinal)).ShouldBeFalse();
    [Fact] void should_ignore_generated_attribute_tenancy() => TenancyDiagnostics.Any(_ => _.Message.Contains("GeneratedAttributedDocument", StringComparison.Ordinal)).ShouldBeFalse();
    [Fact] void should_ignore_generated_policy_tenancy() => TenancyDiagnostics.Any(_ => _.Source?.Path == "Orders/GeneratedTenancy.g.cs" && _.Message.Contains("policy declaration", StringComparison.Ordinal)).ShouldBeFalse();
    [Fact] void should_not_originate_generated_tenancy_types() => Graph.Artifacts.SelectMany(_ => _.Variants).Any(_ => _.Definition.Name.StartsWith("Generated", StringComparison.Ordinal)).ShouldBeFalse();

    IReadOnlyList<GenerationDiagnostic> TenancyDiagnostics => [.. Contribution.Diagnostics.Where(_ => _.Code == MartenDiagnosticCodes.TenancyConfigurationOmitted)];
}
