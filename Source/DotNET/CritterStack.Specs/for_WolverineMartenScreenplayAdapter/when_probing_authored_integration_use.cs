// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Cratis.CritterStack.Screenplay.for_WolverineMartenScreenplayAdapter;

public class when_probing_authored_integration_use : given.an_atomic_adapter_probe_context
{
    AdapterProbeResult _result = null!;
    DotNetAnalysisContext _context = null!;
    readonly WolverineMartenScreenplayAdapter _adapter = new();

    void Because()
    {
        _context = CreateContext(AllFamiliesUse);
        _result = _adapter.Probe(_context);
    }

    [Fact] void should_be_applicable() => _result.ShouldBeOfExactType<AdapterProbeApplicable>();
    [Fact] void should_make_the_legacy_bridge_applicable() => _adapter.CanAnalyze(_context).ShouldBeTrue();
    [Fact] void should_prove_every_required_capability() => _result.Evidence.Select(item => item.ApiCapability).Distinct().ShouldContainOnly(CritterStackAdapterApiCapabilities.MartenApplication, CritterStackAdapterApiCapabilities.WolverineApplication, CritterStackAdapterApiCapabilities.WolverineMartenIntegration);
    [Fact] void should_retain_authoritative_source_identity() => _result.Evidence.All(item => item.Source?.FileIdentity is not null).ShouldBeTrue();
    [Fact] void should_count_the_integration_call_once_as_integration_evidence() => _result.Evidence.Count(item => item.ApiCapability == CritterStackAdapterApiCapabilities.WolverineMartenIntegration).ShouldEqual(1);
}
