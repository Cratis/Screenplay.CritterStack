// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Cratis.CritterStack.Screenplay.for_MartenScreenplayAdapter;

public class when_describing_the_adapter : Specification
{
    AdapterDescriptor _descriptor = null!;

    void Because() => _descriptor = new MartenScreenplayAdapter().Descriptor;

    [Fact] void should_use_the_stable_identity() => _descriptor.Identity.ShouldEqual(new AdapterIdentity { Id = "marten", Version = "1.0.0" });
    [Fact] void should_analyze_csharp() => _descriptor.SourceLanguage.ShouldEqual(AdapterSourceLanguage.CSharp);
    [Fact] void should_own_event_store_semantics() => _descriptor.Category.ShouldEqual(AdapterCategory.EventStore);
    [Fact] void should_require_generation_0_17_or_later() => _descriptor.CompatibleGenerationVersions.MinimumInclusive.ShouldEqual(new Version(0, 17, 0));
    [Fact] void should_require_exact_host_capabilities() => _descriptor.RequiredHostCapabilities.ShouldContainOnly(AdapterHostCapability.AuthoredSource, AdapterHostCapability.StableSourceLocations, AdapterHostCapability.SemanticAnalysis);
    [Fact] void should_require_the_marten_application_api() => _descriptor.RequiredApiCapabilities.ShouldContainOnly(CritterStackAdapterApiCapabilities.MartenApplication);
    [Fact] void should_declare_only_current_atomic_fact_families() => _descriptor.EmittedFactCapabilities.ShouldContainOnly(GenerationFactCapability.Artifact, GenerationFactCapability.ArtifactPlacement, GenerationFactCapability.Relationship);
}
