// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using static Cratis.CritterStack.Screenplay.for_FactParity.given.fact_vectors;

namespace Cratis.CritterStack.Screenplay.for_FactParity;

/// <summary>Contract-only placement vectors isolate the two-stage split from source-owner discovery.</summary>
public class when_mixed_slice_kinds_need_two_stage_fallback : Specification
{
    SplitVector _artifact = null!;
    SplitVector _kind = null!;

    void Because()
    {
        _artifact = Observe("Change", "View");
        _kind = Observe("Shared", "Shared");
    }

    [Fact] void should_preserve_artifact_specific_fallback() => _artifact.Atomic.ShouldEqual(_artifact.Compatibility);
    [Fact] void should_preserve_kind_suffix_fallback() => _kind.Atomic.ShouldEqual(_kind.Compatibility);
    [Fact] void should_exercise_both_stages() => (_artifact.Atomic == _kind.Atomic).ShouldBeFalse();
    [Fact] void should_preserve_reverse_order_artifact_splits() => _artifact.Reverse.ShouldEqual(_artifact.Compatibility);
    [Fact] void should_preserve_reverse_order_kind_splits() => _kind.Reverse.ShouldEqual(_kind.Compatibility);
    [Fact] void should_verify_both_artifact_split_orders() => _artifact.Success.ShouldBeTrue();
    [Fact] void should_verify_both_kind_split_orders() => _kind.Success.ShouldBeTrue();

    static SplitVector Observe(string firstFallback, string secondFallback)
    {
        var project = Project("namespace FactParity; public sealed class Change; public sealed class View;");
        var first = ObservePlacement(project, "Change", firstFallback, GenerationSliceKind.StateChange, MartenAdapter);
        var second = ObservePlacement(project, "View", secondFallback, GenerationSliceKind.StateView, WolverineAdapter);
        var compatibility = CritterStackSliceKindSplit.Split([first.Derived, second.Derived], []);
        var forward = Generate(first.Contribution, second.Contribution);
        var reverse = Generate(second.Contribution, first.Contribution);
        return new(
            Meaning(compatibility.Select(_ => (_.Intent.Artifact, _.Placement))),
            Meaning(forward.Graph.Placements.Select(_ => (_.Artifact, _.EffectiveVariants.Single().Placement))),
            Meaning(reverse.Graph.Placements.Select(_ => (_.Artifact, _.EffectiveVariants.Single().Placement))),
            forward.IsSuccess && reverse.IsSuccess);
    }

    static (CritterStackDerivedPlacement Derived, AdapterContribution Contribution) ObservePlacement(
        DotNetProjectCompilation project, string name, string fallback, GenerationSliceKind kind, AdapterIdentity adapter)
    {
        var type = Type(project, $"FactParity.{name}");
        var key = Key(project, type, ArtifactKind.Document);
        var evidence = Evidence(project, type, adapter);
        var placement = new ArtifactPlacement { Module = "FactParity", Features = ["Placement"], Slice = "Shared", SliceKind = kind };
        var intent = new CritterStackPlacementIntent($"split-vector:{name}", key, null, placement with { Slice = fallback }, evidence);
        var facts = new List<GenerationFact>(Surface(project, type, key, adapter))
        {
            new ArtifactPlacementFact
            {
                Id = new() { Value = $"split-vector:{name}:placement" }, Subject = key.Subject, Artifact = key, Evidence = evidence,
                Placement = placement, MixedSliceFallback = new() { Slice = fallback, Evidence = evidence }
            }
        };
        return (new(intent, placement, "Independent source placement"), Contribution(adapter, facts));
    }

    static string Meaning(IEnumerable<(ArtifactKey Key, ArtifactPlacement Placement)> placements) =>
        string.Join(';', placements.OrderBy(_ => _.Key.Subject.Value, StringComparer.Ordinal).Select(_ =>
            $"{_.Key.Subject.Value}:{_.Key.Kind}={_.Placement.Module}/{string.Join('/', _.Placement.Features)}/{_.Placement.Slice}:{_.Placement.SliceKind}"));

    sealed record SplitVector(string Compatibility, string Atomic, string Reverse, bool Success);
}
