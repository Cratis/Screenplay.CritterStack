// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Cratis.CritterStack.Screenplay.for_MartenScreenplayAdapter;

public class when_probing_authored_marten_use : given.a_marten_adapter_context
{
    AdapterProbeResult _result = null!;

    void Because() => _result = Adapter.Probe(Context);

    [Fact] void should_be_applicable() => _result.ShouldBeOfExactType<AdapterProbeApplicable>();
    [Fact] void should_make_the_legacy_bridge_applicable() => Adapter.CanAnalyze(Context).ShouldBeTrue();
    [Fact] void should_prove_the_exact_api_capability() => _result.Evidence.All(evidence => evidence.ApiCapability == CritterStackAdapterApiCapabilities.MartenApplication).ShouldBeTrue();
    [Fact] void should_retain_authoritative_source_identity() => _result.Evidence.All(evidence => evidence.Source?.FileIdentity is not null).ShouldBeTrue();
    [Fact] void should_retain_the_containing_method_subject() => _result.Evidence.Single().Subject!.Value.ShouldContain("StudentEndpoint.Store");
}
