// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Cratis.CritterStack.Screenplay.for_CritterStackScreenplayGenerator;

public class when_distinct_adapter_instances_share_an_identity : given.a_generator_roster
{
    readonly CountingAdapter _first = new("duplicate");
    readonly CountingAdapter _second = new("duplicate");
    GeneratedScreenplayDefinition _result = null!;

    void Because() => _result = Generate(_first, _second);

    [Fact] void should_probe_neither_instance() => (_first.ProbeCount + _second.ProbeCount).ShouldEqual(0);
    [Fact] void should_analyze_neither_instance() => (_first.AnalyzeCount + _second.AnalyzeCount).ShouldEqual(0);
    [Fact] void should_reject_every_duplicate_record() => _result.AdapterRun.Adapters.All(record => !record.Probed && !record.Executed && record.Disposition == AdapterRunDisposition.RosterRejected).ShouldBeTrue();
}
