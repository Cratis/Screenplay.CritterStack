// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Cratis.CritterStack.Screenplay.for_MartenScreenplayAdapter;

public class when_only_marten_types_are_available : given.a_marten_adapter_context
{
    DotNetAnalysisContext _context = null!;
    AdapterProbeResult _result = null!;

    void Establish() => _context = CreateContext(stableSource: true, authoredUse: false);

    void Because() => _result = Adapter.Probe(_context);

    [Fact] void should_not_be_applicable() => _result.ShouldBeOfExactType<AdapterProbeNotApplicable>();
    [Fact] void should_not_make_the_legacy_bridge_applicable() => Adapter.CanAnalyze(_context).ShouldBeFalse();
}
