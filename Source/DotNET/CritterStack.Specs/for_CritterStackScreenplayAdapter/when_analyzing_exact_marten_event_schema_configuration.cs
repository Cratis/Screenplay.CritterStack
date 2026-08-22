// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Cratis.CritterStack.Screenplay.for_CritterStackScreenplayAdapter;

public class when_analyzing_exact_marten_event_schema_configuration : given.a_marten_event_schema_configuration_application
{
    static readonly HashSet<string> _configurationOnlyTypes =
    [
        "AliasOnly",
        "DirectAliasOnly",
        "ControlledAliasOnly",
        "ExplicitSuffixOnly",
        "ConventionSuffixOnly",
        "ExplicitVersionOnly",
        "ConventionVersionOnly",
        "AttributeAliasOnly",
        "LegacyOne",
        "CurrentOne",
        "LegacyTwo",
        "CurrentTwo",
        "LegacyThree",
        "CurrentThree",
        "LegacyFour",
        "CurrentFour",
        "RawTargetOne",
        "RawTargetTwo",
        "RawTargetThree",
        "RawTargetFour",
        "RawTargetFive",
        "RawTargetSix",
        "RootRawTarget",
        "StaticAliasOnly",
        "StaticRawTarget",
        "StaticLegacy",
        "StaticCurrent",
        "ClassLegacy",
        "ClassCurrent",
        "ClrAsyncLegacy",
        "ClrAsyncCurrent",
        "StjTarget",
        "StjAsyncTarget",
        "JsonNetTarget",
        "JsonNetAsyncTarget",
        "ConditionalAliasOnly",
        "DeferredLegacy",
        "DeferredCurrent"
    ];

    [Fact] void should_preserve_all_event_configuration_occurrences() => EventConfiguration.Count.ShouldEqual(18);
    [Fact] void should_preserve_all_upcast_configuration_occurrences() => UpcastConfiguration.Count.ShouldEqual(27);
    [Fact] void should_retain_the_generic_event_alias() => EventConfiguration.Any(_ => _.Message.Contains("OrderRegistered", StringComparison.Ordinal) && _.Message.Contains("storage alias 'order-registered'", StringComparison.Ordinal)).ShouldBeTrue();
    [Fact] void should_retain_the_direct_type_event_alias() => EventConfiguration.Any(_ => _.Message.Contains("DirectAliasOnly", StringComparison.Ordinal) && _.Message.Contains("direct-alias-only", StringComparison.Ordinal)).ShouldBeTrue();
    [Fact] void should_escape_control_characters_in_diagnostic_values() => EventConfiguration.Any(_ => _.Message.Contains("ControlledAliasOnly", StringComparison.Ordinal) && _.Message.Contains("line\\u000anext\\u001b", StringComparison.Ordinal)).ShouldBeTrue();
    [Fact] void should_retain_the_explicit_suffix_and_derived_alias() => EventConfiguration.Any(_ => _.Message.Contains("ExplicitSuffixOnly", StringComparison.Ordinal) && _.Message.Contains("'explicit-base_legacy'", StringComparison.Ordinal)).ShouldBeTrue();
    [Fact] void should_retain_only_the_convention_suffix_without_guessing_an_alias() => EventConfiguration.Any(_ => _.Message.Contains("ConventionSuffixOnly", StringComparison.Ordinal) && _.Message.Contains("suffix 'legacy'", StringComparison.Ordinal) && _.Message.Contains("effective storage alias was not inferred", StringComparison.Ordinal)).ShouldBeTrue();
    [Fact] void should_retain_the_explicit_schema_version_and_derived_alias() => EventConfiguration.Any(_ => _.Message.Contains("ExplicitVersionOnly", StringComparison.Ordinal) && _.Message.Contains("'version-base_v7'", StringComparison.Ordinal)).ShouldBeTrue();
    [Fact] void should_retain_only_the_convention_schema_version_without_guessing_an_alias() => EventConfiguration.Any(_ => _.Message.Contains("ConventionVersionOnly", StringComparison.Ordinal) && _.Message.Contains("version '3'", StringComparison.Ordinal) && _.Message.Contains("effective storage alias was not inferred", StringComparison.Ordinal)).ShouldBeTrue();
    [Fact] void should_retain_each_exact_naming_style_declaration() => EventConfiguration.Count(_ => _.Source?.Path == "Orders/EventSchemaConfiguration.cs" && _.Message.Contains("naming-style declaration", StringComparison.Ordinal)).ShouldEqual(3);
    [Fact] void should_retain_the_attribute_alias_with_the_auto_register_caveat() => EventConfiguration.Any(_ => _.Message.Contains("AttributeAliasOnly", StringComparison.Ordinal) && _.Message.Contains("MartenEvent alias 'attribute-order'", StringComparison.Ordinal) && _.Message.Contains("AutoRegister", StringComparison.Ordinal)).ShouldBeTrue();
    [Fact] void should_ignore_an_alias_less_marten_event_attribute() => EventConfiguration.Any(_ => _.Message.Contains("AttributeWithoutAlias", StringComparison.Ordinal)).ShouldBeFalse();
    [Fact] void should_ignore_add_event_type_registrations() => EventConfiguration.Any(_ => _.Message.Contains("AddedOnly", StringComparison.Ordinal)).ShouldBeFalse();
    [Fact] void should_ignore_the_unverified_legacy_event_mapping_api() => EventConfiguration.Any(_ => _.Message.Contains("LegacyExcluded", StringComparison.Ordinal)).ShouldBeFalse();
    [Fact] void should_retain_sync_typed_alias_upcasts() => UpcastConfiguration.Any(_ => _.Message.Contains("sync typed upcast declaration 'LegacyOne -> CurrentOne'", StringComparison.Ordinal) && _.Message.Contains("alias 'legacy-one'", StringComparison.Ordinal)).ShouldBeTrue();
    [Fact] void should_retain_sync_typed_convention_upcasts_without_guessing_an_alias() => UpcastConfiguration.Any(_ => _.Message.Contains("sync typed upcast declaration 'LegacyTwo -> CurrentTwo'", StringComparison.Ordinal) && _.Message.Contains("was not inferred", StringComparison.Ordinal)).ShouldBeTrue();
    [Fact] void should_retain_async_typed_alias_upcasts() => UpcastConfiguration.Any(_ => _.Message.Contains("async-only typed upcast declaration 'LegacyThree -> CurrentThree'", StringComparison.Ordinal) && _.Message.Contains("alias 'legacy-three'", StringComparison.Ordinal)).ShouldBeTrue();
    [Fact] void should_retain_async_typed_convention_upcasts() => UpcastConfiguration.Any(_ => _.Message.Contains("async-only typed upcast declaration 'LegacyFour -> CurrentFour'", StringComparison.Ordinal) && _.Message.Contains("was not inferred", StringComparison.Ordinal)).ShouldBeTrue();
    [Fact] void should_retain_sync_schema_version_upcasts() => UpcastConfiguration.Any(_ => _.Message.Contains("LegacyOne -> CurrentOne", StringComparison.Ordinal) && _.Message.Contains("schema version '2'", StringComparison.Ordinal)).ShouldBeTrue();
    [Fact] void should_retain_async_schema_version_upcasts() => UpcastConfiguration.Any(_ => _.Message.Contains("LegacyThree -> CurrentThree", StringComparison.Ordinal) && _.Message.Contains("schema version '4'", StringComparison.Ordinal)).ShouldBeTrue();
    [Fact] void should_retain_generic_raw_json_targets_and_aliases() => UpcastConfiguration.Any(_ => _.Message.Contains("unknown source schema alias 'raw-one' to 'RawTargetOne'", StringComparison.Ordinal)).ShouldBeTrue();
    [Fact] void should_retain_direct_raw_json_targets_and_aliases() => UpcastConfiguration.Any(_ => _.Message.Contains("unknown source schema alias 'raw-two' to 'RawTargetTwo'", StringComparison.Ordinal)).ShouldBeTrue();
    [Fact] void should_retain_generic_raw_json_schema_versions() => UpcastConfiguration.Any(_ => _.Message.Contains("source schema version '5' to 'RawTargetThree'", StringComparison.Ordinal)).ShouldBeTrue();
    [Fact] void should_retain_direct_raw_json_schema_versions() => UpcastConfiguration.Any(_ => _.Message.Contains("source schema version '6' to 'RawTargetFour'", StringComparison.Ordinal)).ShouldBeTrue();
    [Fact] void should_retain_generic_raw_json_convention_targets_without_guessing_an_alias() => UpcastConfiguration.Any(_ => _.Message.Contains("unknown source schema to 'RawTargetFive'", StringComparison.Ordinal) && _.Message.Contains("alias that was not inferred", StringComparison.Ordinal)).ShouldBeTrue();
    [Fact] void should_retain_direct_raw_json_convention_targets_without_guessing_an_alias() => UpcastConfiguration.Any(_ => _.Message.Contains("unknown source schema to 'RawTargetSix'", StringComparison.Ordinal) && _.Message.Contains("alias that was not inferred", StringComparison.Ordinal)).ShouldBeTrue();
    [Fact] void should_retain_root_raw_class_upcaster_targets() => UpcastConfiguration.Any(_ => _.Message.Contains("RootRawUpcaster", StringComparison.Ordinal) && _.Message.Contains("RootRawTarget", StringComparison.Ordinal)).ShouldBeTrue();
    [Fact] void should_retain_static_extension_event_aliases() => EventConfiguration.Any(_ => _.Message.Contains("StaticAliasOnly", StringComparison.Ordinal) && _.Message.Contains("'static-base_legacy'", StringComparison.Ordinal)).ShouldBeTrue();
    [Fact] void should_retain_static_extension_raw_upcasts() => UpcastConfiguration.Any(_ => _.Message.Contains("unknown source schema to 'StaticRawTarget'", StringComparison.Ordinal)).ShouldBeTrue();
    [Fact] void should_retain_static_extension_typed_upcasts() => UpcastConfiguration.Any(_ => _.Message.Contains("StaticLegacy -> StaticCurrent", StringComparison.Ordinal) && _.Message.Contains("schema version '8'", StringComparison.Ordinal)).ShouldBeTrue();
    [Fact] void should_retain_clr_class_upcaster_pairs() => UpcastConfiguration.Any(_ => _.Message.Contains("ClrSyncUpcaster", StringComparison.Ordinal) && _.Message.Contains("ClassLegacy -> ClassCurrent", StringComparison.Ordinal)).ShouldBeTrue();
    [Fact] void should_retain_async_clr_class_upcaster_pairs() => UpcastConfiguration.Any(_ => _.Message.Contains("ClrAsyncUpcaster", StringComparison.Ordinal) && _.Message.Contains("ClrAsyncLegacy -> ClrAsyncCurrent", StringComparison.Ordinal) && _.Message.Contains("async-only", StringComparison.Ordinal)).ShouldBeTrue();
    [Fact] void should_retain_system_text_json_class_upcaster_targets() => UpcastConfiguration.Any(_ => _.Message.Contains("StjSyncUpcaster", StringComparison.Ordinal) && _.Message.Contains("unknown JSON source schema to 'StjTarget'", StringComparison.Ordinal)).ShouldBeTrue();
    [Fact] void should_retain_async_system_text_json_class_upcaster_targets() => UpcastConfiguration.Any(_ => _.Message.Contains("StjAsyncUpcaster", StringComparison.Ordinal) && _.Message.Contains("StjAsyncTarget", StringComparison.Ordinal) && _.Message.Contains("async-only", StringComparison.Ordinal)).ShouldBeTrue();
    [Fact] void should_retain_json_net_class_upcaster_targets() => UpcastConfiguration.Any(_ => _.Message.Contains("JsonNetSyncUpcaster", StringComparison.Ordinal) && _.Message.Contains("JsonNetTarget", StringComparison.Ordinal)).ShouldBeTrue();
    [Fact] void should_retain_async_json_net_class_upcaster_targets() => UpcastConfiguration.Any(_ => _.Message.Contains("JsonNetAsyncUpcaster", StringComparison.Ordinal) && _.Message.Contains("JsonNetAsyncTarget", StringComparison.Ordinal) && _.Message.Contains("async-only", StringComparison.Ordinal)).ShouldBeTrue();
    [Fact] void should_not_inspect_class_upcaster_event_type_name_overrides() => UpcastConfiguration.Where(_ => _.Message.Contains("class-upcaster", StringComparison.Ordinal)).All(_ => _.Message.Contains("EventTypeName overrides", StringComparison.Ordinal)).ShouldBeTrue();
    [Fact] void should_retain_conditional_declarations_without_claiming_execution() => EventConfiguration.Any(_ => _.Message.Contains("ConditionalAliasOnly", StringComparison.Ordinal) && _.Message.Contains("runtime execution", StringComparison.Ordinal)).ShouldBeTrue();
    [Fact] void should_retain_deferred_declarations_without_claiming_execution() => UpcastConfiguration.Any(_ => _.Message.Contains("DeferredLegacy -> DeferredCurrent", StringComparison.Ordinal) && _.Message.Contains("runtime execution", StringComparison.Ordinal)).ShouldBeTrue();
    [Fact] void should_keep_the_independently_established_event_name() => EventNames.ShouldContain("OrderRegistered");
    [Fact] void should_keep_exactly_one_independently_established_event() => EventNames.Count.ShouldEqual(1);
    [Fact] void should_not_originate_configuration_only_events() => EventNames.Intersect(_configurationOnlyTypes).ShouldBeEmpty();
    [Fact] void should_not_originate_behavioral_artifacts_for_configuration_only_types() => ArtifactNames.Intersect(_configurationOnlyTypes).ShouldBeEmpty();
    [Fact] void should_not_emit_behavioral_relationships_for_configuration_only_types() => Contribution.Facts.OfType<RelationshipFact>().Any(_ => IsConfigurationOnly(_.Definition.Key.Source) || IsConfigurationOnly(_.Definition.Key.Target)).ShouldBeFalse();
    [Fact] void should_anchor_every_exact_declaration_at_its_authored_occurrence() => ExactConfiguration.All(_ => _.Source!.StartLine > 0 && _.Source.StartColumn > 0).ShouldBeTrue();
    [Fact] void should_preserve_each_exact_authored_occurrence() => ExactConfiguration.Select(_ => (_.Source!.StartLine, _.Source.StartColumn)).Distinct().Count().ShouldEqual(ExactConfiguration.Count);

    IReadOnlyList<GenerationDiagnostic> EventConfiguration => [.. Contribution.Diagnostics.Where(_ => _.Code == MartenDiagnosticCodes.EventTypeConfigurationOmitted)];
    IReadOnlyList<GenerationDiagnostic> UpcastConfiguration => [.. Contribution.Diagnostics.Where(_ => _.Code == MartenDiagnosticCodes.EventUpcastConfigurationOmitted)];
    IReadOnlyList<GenerationDiagnostic> ExactConfiguration => [.. EventConfiguration.Concat(UpcastConfiguration).Where(_ => _.Source?.Path == "Orders/EventSchemaConfiguration.cs")];
    IReadOnlyList<string> EventNames => ArtifactNamesOf(ArtifactKind.Event);
    IReadOnlyList<string> ArtifactNames => [.. Graph.Artifacts.SelectMany(_ => _.Variants).Select(_ => _.Definition.Name)];

    IReadOnlyList<string> ArtifactNamesOf(ArtifactKind kind) =>
    [
        .. Graph.Artifacts
            .Where(_ => _.Key.Kind == kind)
            .SelectMany(_ => _.Variants)
            .Select(_ => _.Definition.Name)
    ];

    bool IsConfigurationOnly(SubjectId subject) => _configurationOnlyTypes.Any(_ => subject.Value.EndsWith($".{_}", StringComparison.Ordinal));
}
