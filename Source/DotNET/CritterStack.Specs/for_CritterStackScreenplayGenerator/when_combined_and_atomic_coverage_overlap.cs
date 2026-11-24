// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Cratis.CritterStack.Screenplay.for_CritterStackScreenplayGenerator;

public class when_combined_and_atomic_coverage_overlap : given.a_generator_roster
{
    readonly CountingAdapter _unaffected = new("unaffected");
    readonly IDotNetScreenplayAdapter[] _atomic = [new MartenScreenplayAdapter(), new WolverineScreenplayAdapter(), new WolverineMartenScreenplayAdapter()];
    Exception?[] _forward = [];
    Exception?[] _reverse = [];

    void Because()
    {
        _forward = [.. _atomic.Select(adapter => Catch.Exception(() => Generate(_unaffected, new CritterStackScreenplayAdapter(), adapter)))];
        _reverse = [.. _atomic.Select(adapter => Catch.Exception(() => Generate(adapter, new CritterStackScreenplayAdapter(), _unaffected)))];
    }

    [Fact] void should_reject_each_atomic_overlap() => _forward.All(error => error is OverlappingCritterStackAdapters).ShouldBeTrue();
    [Fact] void should_reject_each_reversed_overlap() => _reverse.All(error => error is OverlappingCritterStackAdapters).ShouldBeTrue();
    [Fact] void should_name_the_invalid_roster_parameter() => _forward.Cast<ArgumentException>().All(error => error.ParamName == "adapters").ShouldBeTrue();
    [Fact] void should_reject_before_any_described_probe() => _unaffected.ProbeCount.ShouldEqual(0);
    [Fact] void should_reject_before_any_legacy_probe() => _unaffected.LegacyProbeCount.ShouldEqual(0);
    [Fact] void should_reject_before_any_analysis() => _unaffected.AnalyzeCount.ShouldEqual(0);
}
