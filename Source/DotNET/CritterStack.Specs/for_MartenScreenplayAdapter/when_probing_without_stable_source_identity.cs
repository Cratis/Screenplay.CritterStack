// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Cratis.CritterStack.Screenplay.for_MartenScreenplayAdapter;

public class when_probing_without_stable_source_identity : given.a_marten_adapter_context
{
    DotNetAnalysisContext _context = null!;
    AdapterProbeResult _result = null!;

    void Establish() => _context = CreateContext(stableSource: false, authoredUse: true);

    void Because() => _result = Adapter.Probe(_context);

    [Fact] void should_block_instead_of_exposing_unstable_evidence() => _result.ShouldBeOfExactType<AdapterProbeBlocked>();
    [Fact] void should_report_the_stable_diagnostic() => ((AdapterProbeBlocked)_result).Diagnostics.Single().Code.ShouldEqual(Marten.MartenDiagnosticCodes.UnsafeSourceMapping);
    [Fact] void should_not_make_the_legacy_bridge_applicable() => Adapter.CanAnalyze(_context).ShouldBeFalse();
}
