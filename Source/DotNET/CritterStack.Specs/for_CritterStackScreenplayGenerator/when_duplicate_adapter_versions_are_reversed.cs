// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Cratis.CritterStack.Screenplay.for_CritterStackScreenplayGenerator;

public class when_duplicate_adapter_versions_are_reversed : given.a_generator_roster
{
    readonly CountingAdapter _first = new("duplicate", "2.0.0");
    readonly CountingAdapter _second = new("duplicate", "1.0.0");
    GeneratedScreenplayDefinition _forward = null!;
    GeneratedScreenplayDefinition _reverse = null!;

    void Because()
    {
        _forward = Generate(_first, _second);
        _reverse = Generate(_second, _first);
    }

    [Fact] void should_probe_neither_version() => (_first.ProbeCount + _second.ProbeCount).ShouldEqual(0);
    [Fact] void should_analyze_neither_version() => (_first.AnalyzeCount + _second.AnalyzeCount).ShouldEqual(0);
    [Fact] void should_preserve_rejections_in_both_orders() => _forward.AdapterRun!.Adapters.Concat(_reverse.AdapterRun!.Adapters).All(record => record.Disposition == AdapterRunDisposition.RosterRejected).ShouldBeTrue();
    [Fact] void should_keep_canonical_version_order() => _forward.AdapterRun!.Adapters.Select(record => record.Descriptor.Identity.Version).ShouldEqual(["1.0.0", "2.0.0"]);
    [Fact] void should_keep_the_same_version_order_when_reversed() => _reverse.AdapterRun!.Adapters.Select(record => record.Descriptor.Identity.Version).ShouldEqual(["1.0.0", "2.0.0"]);
    [Fact] void should_keep_diagnostic_payloads_independent_of_order() => _reverse.Diagnostics.ToArray().ShouldEqualInOrder([.. _forward.Diagnostics]);
}
