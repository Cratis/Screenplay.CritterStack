// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Cratis.CritterStack.Screenplay.for_CritterStackScreenplayAdapter;

public class when_deriving_explicit_flat_source_compatibility : given.a_shared_source_placement_application
{
    AdapterContribution _contribution = null!;
    ArtifactPlacementFact _commandPlacement = null!;

    void Because()
    {
        _contribution = Adapter.Analyze(
            new([CreateFlatProject()]),
            new DotNetAdapterOptions { NamespaceSegmentsToSkip = 1 });
        _commandPlacement = _contribution.Facts
            .OfType<ArtifactPlacementFact>()
            .Single(_ => _.Artifact.Kind == ArtifactKind.Command);
    }

    [Fact] void should_succeed_without_blocking_source_placement_diagnostics() => _contribution.Diagnostics.Where(_ => _.Code.StartsWith("DOTNETSP", StringComparison.Ordinal)).ShouldBeEmpty();
    [Fact] void should_use_the_exact_legacy_compatibility_placement() => Canonical(_commandPlacement.Placement).ShouldEqual("Application/Order/SubmitOrder:StateChange");
    [Fact] void should_record_that_explicit_compatibility_was_used() => _commandPlacement.Evidence.Explanation.ShouldContain("usedCompatibility=true");
    [Fact] void should_record_dotnetsp0004_as_the_sole_compatibility_reason() => _commandPlacement.Evidence.Explanation.ShouldContain("compatibilityReason=DOTNETSP0004");
    [Fact] void should_record_the_versioned_exact_compatibility_policy() => _commandPlacement.Evidence.Explanation.ShouldContain("compatibilityPolicy(version=1, placement=Application/Order/SubmitOrder:StateChange)");

    static string Canonical(ArtifactPlacement placement) =>
        $"{placement.Module}/{string.Join('/', placement.Features)}/{placement.Slice}:{placement.SliceKind}";
}
