// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Cratis.CritterStack.Screenplay.for_WolverineScreenplayAdapter;

public class when_probing_without_stable_source_identity : given.an_atomic_adapter_probe_context
{
    DotNetAnalysisContext _context = null!;
    AdapterProbeResult _result = null!;
    readonly WolverineScreenplayAdapter _adapter = new();

    void Establish() => _context = CreateContext(WolverineUse, stableSource: false);

    void Because() => _result = _adapter.Probe(_context);

    [Fact] void should_block_instead_of_exposing_unstable_evidence() => _result.ShouldBeOfExactType<AdapterProbeBlocked>();
    [Fact] void should_report_an_unknown_error() => ((AdapterProbeBlocked)_result).Diagnostics.Single().Outcome.ShouldEqual(GenerationDiagnosticOutcome.Unknown);
    [Fact] void should_not_make_the_legacy_bridge_applicable() => _adapter.CanAnalyze(_context).ShouldBeFalse();
}
