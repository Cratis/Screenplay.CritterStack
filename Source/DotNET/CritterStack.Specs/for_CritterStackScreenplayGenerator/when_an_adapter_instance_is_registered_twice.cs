// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Cratis.CritterStack.Screenplay.for_CritterStackScreenplayGenerator;

public class when_an_adapter_instance_is_registered_twice : given.a_generator_roster
{
    readonly CountingAdapter _duplicate = new("duplicate");
    readonly CountingAdapter _valid = new("valid");
    GeneratedScreenplayDefinition _result = null!;

    void Because() => _result = Generate(_duplicate, _valid, _duplicate);

    [Fact] void should_not_probe_the_duplicate() => _duplicate.ProbeCount.ShouldEqual(0);
    [Fact] void should_not_analyze_the_duplicate() => _duplicate.AnalyzeCount.ShouldEqual(0);
    [Fact] void should_not_fall_back_to_the_legacy_probe() => _duplicate.LegacyProbeCount.ShouldEqual(0);
    [Fact] void should_reject_both_registrations() => _result.AdapterRun!.Adapters.Count(record => record.Disposition == AdapterRunDisposition.RosterRejected).ShouldEqual(2);
    [Fact] void should_probe_the_unaffected_adapter_once() => _valid.ProbeCount.ShouldEqual(1);
    [Fact] void should_analyze_the_unaffected_adapter_once() => _valid.AnalyzeCount.ShouldEqual(1);
    [Fact] void should_report_the_duplicate_identity() => _result.Diagnostics.Any(diagnostic => diagnostic.Code == DotNetAdapterGenerationDiagnosticCodes.DuplicateAdapterId).ShouldBeTrue();
}
