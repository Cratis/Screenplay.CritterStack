// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Cratis.CritterStack.Screenplay.for_WolverineScreenplayAdapter;

public class when_describing_the_adapter : Specification
{
    AdapterDescriptor _descriptor = null!;
    WolverineScreenplayAdapter _adapter = null!;

    void Because()
    {
        _adapter = new();
        _descriptor = _adapter.Descriptor;
    }

    [Fact] void should_use_the_stable_identity() => _descriptor.Identity.ShouldEqual(new AdapterIdentity { Id = "wolverine", Version = "1.0.0" });
    [Fact] void should_agree_with_the_legacy_identity() => _adapter.Identity.ShouldEqual(_descriptor.Identity);
    [Fact] void should_own_application_framework_semantics() => _descriptor.Category.ShouldEqual(AdapterCategory.ApplicationFramework);
    [Fact] void should_analyze_csharp() => _descriptor.SourceLanguage.ShouldEqual(AdapterSourceLanguage.CSharp);
    [Fact] void should_require_generation_0_23_or_later() => _descriptor.CompatibleGenerationVersions.MinimumInclusive.ShouldEqual(new Version(0, 23, 0));
    [Fact] void should_require_exact_host_capabilities() => _descriptor.RequiredHostCapabilities.ShouldContainOnly(AdapterHostCapability.AuthoredSource, AdapterHostCapability.StableSourceLocations, AdapterHostCapability.SemanticAnalysis);
    [Fact] void should_require_only_the_wolverine_application_api() => _descriptor.RequiredApiCapabilities.ShouldContainOnly(CritterStackAdapterApiCapabilities.WolverineApplication);
    [Fact] void should_implement_both_adapter_contracts() => (_adapter is IDescribedDotNetScreenplayAdapter && _adapter is IDotNetScreenplayAdapter).ShouldBeTrue();
    [Fact] void should_not_be_public_until_emission_is_cut_over() => typeof(WolverineScreenplayAdapter).IsPublic.ShouldBeFalse();
}
