// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Cratis.CritterStack.Screenplay.for_WolverineMartenScreenplayAdapter;

public class when_describing_the_adapter : Specification
{
    AdapterDescriptor _descriptor = null!;
    WolverineMartenScreenplayAdapter _adapter = null!;

    void Because()
    {
        _adapter = new();
        _descriptor = _adapter.Descriptor;
    }

    [Fact] void should_use_the_stable_identity() => _descriptor.Identity.ShouldEqual(new AdapterIdentity { Id = "wolverine-marten", Version = "1.0.0" });
    [Fact] void should_agree_with_the_legacy_identity() => _adapter.Identity.ShouldEqual(_descriptor.Identity);
    [Fact] void should_own_integration_semantics() => _descriptor.Category.ShouldEqual(AdapterCategory.Integration);
    [Fact] void should_require_generation_0_23_or_later() => _descriptor.CompatibleGenerationVersions.MinimumInclusive.ShouldEqual(new Version(0, 23, 0));
    [Fact] void should_require_exact_host_capabilities() => _descriptor.RequiredHostCapabilities.ShouldContainOnly(AdapterHostCapability.AuthoredSource, AdapterHostCapability.StableSourceLocations, AdapterHostCapability.SemanticAnalysis);
    [Fact] void should_require_all_three_api_capabilities() => _descriptor.RequiredApiCapabilities.ShouldContainOnly(CritterStackAdapterApiCapabilities.MartenApplication, CritterStackAdapterApiCapabilities.WolverineApplication, CritterStackAdapterApiCapabilities.WolverineMartenIntegration);
    [Fact] void should_not_be_public_until_emission_is_cut_over() => typeof(WolverineMartenScreenplayAdapter).IsPublic.ShouldBeFalse();
}
