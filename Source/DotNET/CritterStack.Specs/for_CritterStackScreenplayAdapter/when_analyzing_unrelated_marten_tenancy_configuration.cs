// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Cratis.CritterStack.Screenplay.for_CritterStackScreenplayAdapter;

public class when_analyzing_unrelated_marten_tenancy_configuration : given.a_marten_tenancy_configuration_application
{
    [Fact] void should_ignore_unrelated_same_named_event_tenancy() => TenancyDiagnostics.Any(_ => _.Source?.Path == "Unrelated/Tenancy.cs" && _.Message.Contains("event tenancy-style", StringComparison.Ordinal)).ShouldBeFalse();
    [Fact] void should_ignore_unrelated_same_named_document_apis() => TenancyDiagnostics.Any(_ => _.Message.Contains("SameNamedDocument", StringComparison.Ordinal)).ShouldBeFalse();
    [Fact] void should_ignore_unrelated_same_named_policies() => TenancyDiagnostics.Any(_ => _.Source?.Path == "Unrelated/Tenancy.cs" && _.Message.Contains("policy declaration", StringComparison.Ordinal)).ShouldBeFalse();
    [Fact] void should_not_originate_unrelated_documents() => Graph.Artifacts.SelectMany(_ => _.Variants).Any(_ => _.Definition.Name == "SameNamedDocument").ShouldBeFalse();

    IReadOnlyList<GenerationDiagnostic> TenancyDiagnostics => [.. Contribution.Diagnostics.Where(_ => _.Code == MartenDiagnosticCodes.TenancyConfigurationOmitted)];
}
