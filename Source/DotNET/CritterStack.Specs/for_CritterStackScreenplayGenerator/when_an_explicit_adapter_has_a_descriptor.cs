// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Cratis.CritterStack.Screenplay.for_CritterStackScreenplayGenerator;

public class when_an_explicit_adapter_has_a_descriptor : given.a_generator_roster
{
    readonly CountingAdapter _adapter = new("independent");
    GeneratedScreenplayDefinition _first = null!;
    GeneratedScreenplayDefinition _second = null!;

    void Because()
    {
        _first = Generate(_adapter);
        _second = Generate(_adapter);
    }

    [Fact] void should_use_the_described_probe() => _adapter.ProbeCount.ShouldEqual(2);
    [Fact] void should_not_use_the_legacy_probe() => _adapter.LegacyProbeCount.ShouldEqual(0);
    [Fact] void should_analyze_once_per_run() => _adapter.AnalyzeCount.ShouldEqual(2);
    [Fact] void should_preserve_the_admitted_run_record() => _first.AdapterRun.Adapters.Single().Disposition.ShouldEqual(AdapterRunDisposition.Admitted);
    [Fact] void should_keep_each_runs_snapshot_independent() => ReferenceEquals(_first.AdapterRun, _second.AdapterRun).ShouldBeFalse();
    [Fact] void should_repeat_the_generated_source() => _second.Source.ShouldEqual(_first.Source);
}
