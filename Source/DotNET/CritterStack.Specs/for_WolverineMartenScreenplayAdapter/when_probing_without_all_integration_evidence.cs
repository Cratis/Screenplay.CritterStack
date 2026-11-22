// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Cratis.CritterStack.Screenplay.for_WolverineMartenScreenplayAdapter;

public class when_probing_without_all_integration_evidence : given.an_atomic_adapter_probe_context
{
    AdapterProbeResult _integrationOnly = null!;
    AdapterProbeResult _wolverineAndMarten = null!;
    AdapterProbeResult _wolverineOnly = null!;
    AdapterProbeResult _generatedOnly = null!;
    AdapterProbeResult _unmapped = null!;
    readonly WolverineMartenScreenplayAdapter _adapter = new();

    void Because()
    {
        _integrationOnly = _adapter.Probe(CreateContext(IntegrationUse));
        _wolverineAndMarten = _adapter.Probe(CreateContext(WolverineUse + MartenUse.Replace("namespace App;", string.Empty, StringComparison.Ordinal)));
        _wolverineOnly = _adapter.Probe(CreateContext(WolverineUse));
        _generatedOnly = _adapter.Probe(CreateContext(AllFamiliesUse, generatedApplication: true));
        _unmapped = _adapter.Probe(CreateContext(AllFamiliesUse, stableSource: false));
    }

    [Fact] void should_not_admit_integration_without_marten_and_wolverine_evidence() => _integrationOnly.ShouldBeOfExactType<AdapterProbeNotApplicable>();
    [Fact] void should_not_admit_wolverine_and_marten_without_the_integration() => _wolverineAndMarten.ShouldBeOfExactType<AdapterProbeNotApplicable>();
    [Fact] void should_not_admit_wolverine_without_marten() => _wolverineOnly.ShouldBeOfExactType<AdapterProbeNotApplicable>();
    [Fact] void should_not_admit_generated_only_use() => _generatedOnly.ShouldBeOfExactType<AdapterProbeNotApplicable>();
    [Fact] void should_block_unmapped_source() => _unmapped.ShouldBeOfExactType<AdapterProbeBlocked>();
}
