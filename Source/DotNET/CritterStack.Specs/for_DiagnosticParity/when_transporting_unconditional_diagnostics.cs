// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Cratis.CritterStack.Screenplay.for_DiagnosticParity;

/// <summary>
/// Separately contributed unconditional diagnostics retain the facade's complete payload and multiplicity.
/// These are SDK transport vectors; they deliberately do not stand in for atomic adapter execution.
/// </summary>
public class when_transporting_unconditional_diagnostics : given.a_community_kitchen_application
{
    static readonly Type[] _fixtures =
    [
        typeof(given.a_marten_document_variants_application),
        typeof(given.a_marten_configuration_variants_application),
        typeof(given.a_marten_projection_variants_application),
        typeof(given.a_marten_wire_variants_application),
        typeof(given.a_projection_side_effect_variants_application),
        typeof(given.a_marten_event_schema_argument_variants_application),
        typeof(given.a_marten_multi_stream_grouping_application),
        typeof(given.a_legacy_marten_projection_metadata_application),
        typeof(given.a_session_listener_application),
        typeof(given.a_convention_alteration_application),
        typeof(given.a_handler_chain_configuration_application),
        typeof(given.a_compound_stage_handler_application),
        typeof(given.a_wolverine_policy_variants_application),
        typeof(given.a_wolverine_validation_authorization_application),
        typeof(given.a_wolverine_targeted_event_stream_application),
        typeof(given.a_wolverine_returns_and_links_variants_application),
        typeof(given.a_wolverine_saga_variants_application),
        typeof(given.a_wolverine_dcb_application),
        typeof(given.a_wolverine_dcb_competition_application),
        typeof(given.a_wolverine_slice_pattern_application),
        typeof(given.an_event_forwarding_application),
        typeof(given.a_marten_compiled_query_application),
        typeof(given.a_marten_store_scope_application),
        typeof(given.a_wolverine_marten_application),
        typeof(given.a_wolverine_configured_discovery_application),
        typeof(given.a_wolverine_fetch_many_application),
        typeof(given.a_wolverine_saga_application),
        typeof(given.a_community_kitchen_application),
        typeof(for_CritterStackScreenplayAdapter.when_analyzing_marten_composite_projection),
        typeof(for_CritterStackScreenplayAdapter.when_analyzing_marten_flat_table_projection),
        typeof(for_CritterStackScreenplayAdapter.when_analyzing_endpoint_idempotency),
        typeof(for_CritterStackScreenplayGenerator.when_generating_natural_key_aggregates)
    ];

    readonly List<(string Fixture, GeneratedScreenplayDefinition Compatibility, GeneratedScreenplayDefinition Forward, GeneratedScreenplayDefinition Reverse)> _vectors = [];

    void Because()
    {
        foreach (var fixture in _fixtures)
        {
            Add(fixture.Name, given.diagnostic_parity.ProjectFrom(fixture));
        }
        Add("mixed slice placement", CreateFlatProject());
        Add("current tenancy", given.diagnostic_parity.ProjectFrom(typeof(given.a_marten_tenancy_configuration_application), "CurrentFrameworkSource", "CurrentApplicationSource"));
        Add("legacy tenancy", given.diagnostic_parity.ProjectFrom(typeof(given.a_marten_tenancy_configuration_application), "LegacyFrameworkSource", "LegacyApplicationSource"));
    }

    [Fact] void should_exercise_every_fixture() => _vectors.Count.ShouldEqual(_fixtures.Length + 3);

    [Fact]
    void should_cover_every_emitted_unconditional_diagnostic_family() => _vectors.SelectMany(vector => vector.Compatibility.Diagnostics)
        .Select(diagnostic => diagnostic.Code).Where(code => code.StartsWith("MARTEN", StringComparison.Ordinal) || code.StartsWith("WOLVERINE", StringComparison.Ordinal) || code.StartsWith("CRITTERSTACK", StringComparison.Ordinal))
        .Distinct().Order(StringComparer.Ordinal).ToArray().ShouldEqual(
        [
            "CRITTERSTACK0004",
            "MARTEN0001", "MARTEN0002", "MARTEN0003", "MARTEN0004", "MARTEN0005", "MARTEN0006", "MARTEN0007", "MARTEN0008", "MARTEN0009", "MARTEN0010",
            "MARTEN0011", "MARTEN0012", "MARTEN0013", "MARTEN0014", "MARTEN0015", "MARTEN0016", "MARTEN0018", "MARTEN0019",
            "WOLVERINE0001", "WOLVERINE0002", "WOLVERINE0003", "WOLVERINE0004", "WOLVERINE0005", "WOLVERINE0006", "WOLVERINE0007", "WOLVERINE0008", "WOLVERINE0009", "WOLVERINE0010",
            "WOLVERINE0011", "WOLVERINE0012", "WOLVERINE0013", "WOLVERINE0014", "WOLVERINE0015", "WOLVERINE0016", "WOLVERINE0017", "WOLVERINE0018", "WOLVERINE0019", "WOLVERINE0020",
            "WOLVERINE0021", "WOLVERINE0022", "WOLVERINE0023", "WOLVERINE0024", "WOLVERINE0025", "WOLVERINE0027", "WOLVERINE0028", "WOLVERINE0030"
        ]);

    [Fact]
    void should_preserve_code_severity_outcome_message_subject_location_and_count()
    {
        foreach (var vector in _vectors)
        {
            given.diagnostic_parity.Meaning(vector.Forward.Diagnostics).ShouldEqual(given.diagnostic_parity.Meaning(vector.Compatibility.Diagnostics));
        }
    }

    [Fact]
    void should_preserve_diagnostics_in_reverse_contribution_order()
    {
        foreach (var vector in _vectors)
        {
            given.diagnostic_parity.Meaning(vector.Reverse.Diagnostics).ShouldEqual(given.diagnostic_parity.Meaning(vector.Compatibility.Diagnostics));
        }
    }

    [Fact]
    void should_not_change_generated_source_or_verification()
    {
        foreach (var vector in _vectors)
        {
            vector.Forward.Source.ShouldEqual(vector.Compatibility.Source);
            vector.Reverse.Source.ShouldEqual(vector.Compatibility.Source);
            vector.Forward.IsSuccess.ShouldEqual(vector.Compatibility.IsSuccess);
            vector.Reverse.IsSuccess.ShouldEqual(vector.Compatibility.IsSuccess);
        }
    }

    void Add(string fixture, DotNetProjectCompilation project)
    {
        var adapter = new CritterStackScreenplayAdapter();
        var contribution = adapter.Analyze(new DotNetAnalysisContext([project]), new DotNetAdapterOptions());
        var compatibility = new CritterStackScreenplayGenerator(adapter, new ScreenplayDefinitionGenerator())
            .Generate([project], new CritterStackScreenplayOptions { Domain = "Diagnostics" });
        var first = given.diagnostic_parity.Reown(contribution, "first-owner");
        var second = given.diagnostic_parity.Reown(contribution, "second-owner");
        _vectors.Add((fixture, compatibility, given.diagnostic_parity.Generate(first, second), given.diagnostic_parity.Generate(second, first)));
    }
}
