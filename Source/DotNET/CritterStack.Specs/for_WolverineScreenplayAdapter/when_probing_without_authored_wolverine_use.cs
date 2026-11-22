// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Cratis.CritterStack.Screenplay.for_WolverineScreenplayAdapter;

public class when_probing_without_authored_wolverine_use : given.an_atomic_adapter_probe_context
{
    AdapterProbeResult _noUse = null!;
    AdapterProbeResult _martenOnly = null!;
    AdapterProbeResult _integrationOnly = null!;
    AdapterProbeResult _generatedOnly = null!;
    DotNetAnalysisContext _martenContext = null!;
    readonly WolverineScreenplayAdapter _adapter = new();

    void Because()
    {
        _martenContext = CreateContext(MartenUse);
        _noUse = _adapter.Probe(CreateContext(NoUse));
        _martenOnly = _adapter.Probe(_martenContext);
        _integrationOnly = _adapter.Probe(CreateContext(IntegrationUse));
        _generatedOnly = _adapter.Probe(CreateContext(WolverineUse, generatedApplication: true));
    }

    [Fact] void should_not_admit_framework_types_that_are_merely_available() => _noUse.ShouldBeOfExactType<AdapterProbeNotApplicable>();
    [Fact] void should_not_admit_marten_only_source() => _martenOnly.ShouldBeOfExactType<AdapterProbeNotApplicable>();
    [Fact] void should_not_make_the_legacy_bridge_applicable_for_marten_only_source() => _adapter.CanAnalyze(_martenContext).ShouldBeFalse();
    [Fact] void should_not_treat_integration_apis_as_wolverine_apis() => _integrationOnly.ShouldBeOfExactType<AdapterProbeNotApplicable>();
    [Fact] void should_not_admit_generated_only_use() => _generatedOnly.ShouldBeOfExactType<AdapterProbeNotApplicable>();
}
