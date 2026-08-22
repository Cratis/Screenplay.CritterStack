// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Cratis.CritterStack.Screenplay.for_CritterStackScreenplayAdapter;

public class when_analyzing_unresolved_marten_event_schema_configuration : given.a_marten_event_schema_configuration_application
{
    [Fact] void should_report_each_unresolved_event_configuration_once() => UnresolvedEventConfiguration.Count.ShouldEqual(4);
    [Fact] void should_report_each_unresolved_upcast_configuration_once() => UnresolvedUpcastConfiguration.Count.ShouldEqual(5);
    [Fact] void should_not_guess_computed_aliases() => UnresolvedDiagnostics.Any(_ => _.Message.Contains("must-not-be-guessed", StringComparison.Ordinal)).ShouldBeFalse();
    [Fact] void should_not_guess_computed_versions() => UnresolvedDiagnostics.Any(_ => _.Message.Contains("'99'", StringComparison.Ordinal)).ShouldBeFalse();
    [Fact] void should_not_guess_a_runtime_enum_value() => UnresolvedEventConfiguration.Any(_ => _.Message.Contains("FullTypeName", StringComparison.Ordinal)).ShouldBeFalse();
    [Fact] void should_not_guess_an_indirect_raw_json_target() => UnresolvedUpcastConfiguration.Any(_ => _.Message.Contains("ComputedRawTarget", StringComparison.Ordinal)).ShouldBeFalse();
    [Fact] void should_treat_inline_params_collections_as_all_or_nothing() => UpcastConfiguration.Count(_ => _.Message.Contains("ClrSyncUpcaster", StringComparison.Ordinal)).ShouldEqual(1);
    [Fact] void should_anchor_each_unresolved_declaration_at_its_authored_occurrence() => UnresolvedDiagnostics.All(_ => _.Source?.Path == "Orders/UnresolvedEventSchemaConfiguration.cs" && _.Source.StartLine > 0 && _.Source.StartColumn > 0).ShouldBeTrue();
    [Fact] void should_not_originate_unresolved_configuration_types_as_events() => EventNames.Any(_ => _.StartsWith("Computed", StringComparison.Ordinal)).ShouldBeFalse();
    [Fact] void should_not_emit_behavioral_relationships_for_unresolved_configuration_types() => Contribution.Facts.OfType<RelationshipFact>().Any(_ => _.Definition.Key.Source.Value.Contains("Orders.Unresolved", StringComparison.Ordinal) || _.Definition.Key.Target.Value.Contains("Orders.Unresolved", StringComparison.Ordinal)).ShouldBeFalse();

    IReadOnlyList<GenerationDiagnostic> ConfigurationDiagnostics =>
    [
        .. Contribution.Diagnostics.Where(_ =>
            _.Code == MartenDiagnosticCodes.EventTypeConfigurationOmitted ||
            _.Code == MartenDiagnosticCodes.EventUpcastConfigurationOmitted)
    ];
    IReadOnlyList<GenerationDiagnostic> UpcastConfiguration => [.. Contribution.Diagnostics.Where(_ => _.Code == MartenDiagnosticCodes.EventUpcastConfigurationOmitted)];
    IReadOnlyList<GenerationDiagnostic> UnresolvedDiagnostics => [.. ConfigurationDiagnostics.Where(_ => _.Message.Contains("could not be resolved", StringComparison.Ordinal) || _.Message.Contains("otherwise unresolved", StringComparison.Ordinal))];
    IReadOnlyList<GenerationDiagnostic> UnresolvedEventConfiguration => [.. UnresolvedDiagnostics.Where(_ => _.Code == MartenDiagnosticCodes.EventTypeConfigurationOmitted)];
    IReadOnlyList<GenerationDiagnostic> UnresolvedUpcastConfiguration => [.. UnresolvedDiagnostics.Where(_ => _.Code == MartenDiagnosticCodes.EventUpcastConfigurationOmitted)];
    IReadOnlyList<string> EventNames =>
    [
        .. Graph.Artifacts
            .Where(_ => _.Key.Kind == ArtifactKind.Event)
            .SelectMany(_ => _.Variants)
            .Select(_ => _.Definition.Name)
    ];
}
