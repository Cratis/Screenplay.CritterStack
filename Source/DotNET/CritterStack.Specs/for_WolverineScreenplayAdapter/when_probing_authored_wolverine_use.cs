// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Cratis.CritterStack.Screenplay.for_WolverineScreenplayAdapter;

public class when_probing_authored_wolverine_use : given.an_atomic_adapter_probe_context
{
    AdapterProbeResult _invocation = null!;
    AdapterProbeResult _attribute = null!;
    DotNetAnalysisContext _invocationContext = null!;
    readonly WolverineScreenplayAdapter _adapter = new();

    void Because()
    {
        _invocationContext = CreateContext(WolverineUse);
        _invocation = _adapter.Probe(_invocationContext);
        _attribute = _adapter.Probe(CreateContext(WolverineAttributeUse));
    }

    [Fact] void should_admit_an_exact_invocation() => _invocation.ShouldBeOfExactType<AdapterProbeApplicable>();
    [Fact] void should_admit_an_exact_attribute() => _attribute.ShouldBeOfExactType<AdapterProbeApplicable>();
    [Fact] void should_make_the_legacy_bridge_applicable() => _adapter.CanAnalyze(_invocationContext).ShouldBeTrue();
    [Fact] void should_prove_only_the_wolverine_capability() => _invocation.Evidence.All(item => item.ApiCapability == CritterStackAdapterApiCapabilities.WolverineApplication).ShouldBeTrue();
    [Fact] void should_retain_authoritative_source_identity() => _invocation.Evidence.All(item => item.Source?.FileIdentity is not null).ShouldBeTrue();
    [Fact] void should_retain_the_containing_method_subject() => _invocation.Evidence.Single().Subject.Value.ShouldContain("Sender.Send");
}
