// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Cratis.CritterStack.Screenplay.for_MartenScreenplayAdapter;

public class when_only_marten_aggregation_apis_are_used : given.a_marten_adapter_context
{
    AdapterProbeResult _probe = null!;
    AdapterContribution _contribution = null!;

    void Because()
    {
        var context = CreateAggregationContext();
        _probe = Adapter.Probe(context);
        _contribution = Adapter.Analyze(context, new DotNetAdapterOptions { Module = "Students" });
    }

    [Fact] void should_probe_the_source_as_applicable() => _probe.ShouldBeOfExactType<AdapterProbeApplicable>();
    [Fact] void should_allow_the_context_to_be_analyzed() => Adapter.CanAnalyze(CreateAggregationContext()).ShouldBeTrue();
    [Fact] void should_not_emit_wolverine_diagnostics() => _contribution.Diagnostics.Any(diagnostic => diagnostic.Code.StartsWith("WOLVERINE", StringComparison.Ordinal)).ShouldBeFalse();
}
