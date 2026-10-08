// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Cratis.Screenplay.Generation;

namespace Cratis.CritterStack.Screenplay;

/// <summary>
/// A source-derived placement for one artifact, with the intent that requested it and its provenance.
/// </summary>
/// <param name="Intent">The placement intent.</param>
/// <param name="Placement">The derived placement.</param>
/// <param name="Explanation">The placement provenance.</param>
sealed record CritterStackDerivedPlacement(
    CritterStackPlacementIntent Intent,
    ArtifactPlacement Placement,
    string Explanation);

/// <summary>
/// Splits a source-derived slice that mixes slice kinds, such as a flat feature namespace holding commands, events and read models.
/// </summary>
/// <remarks>
/// One Screenplay slice has one kind, so lowering such a slice would fail for every artifact in it. Each artifact
/// instead takes the slice name of its own compatibility placement (the command, aggregate or read model it belongs to)
/// within the same module and features, and a diagnostic explains how to place the source so the split is not needed.
/// </remarks>
static class CritterStackSliceKindSplit
{
    /// <summary>
    /// Splits every mixed-kind slice in the derived placements.
    /// </summary>
    /// <param name="placements">The derived placements.</param>
    /// <param name="diagnostics">Receives one diagnostic per split slice.</param>
    /// <returns>The placements with no slice assigned more than one kind.</returns>
    public static IReadOnlyList<CritterStackDerivedPlacement> Split(
        IReadOnlyList<CritterStackDerivedPlacement> placements,
        List<GenerationDiagnostic> diagnostics)
    {
        var mixed = MixedSlices(placements);
        if (mixed.Count == 0)
        {
            return placements;
        }

        foreach (var slice in mixed.Order(StringComparer.Ordinal))
        {
            diagnostics.Add(Diagnostic(slice, [.. placements.Where(_ => SliceKey(_.Placement) == slice)]));
        }

        var split = placements
            .Select(_ => mixed.Contains(SliceKey(_.Placement)) ? Rename(_, _.Intent.CompatibilityPlacement.Slice, "artifact") : _)
            .ToArray();
        var stillMixed = MixedSlices(split);
        return stillMixed.Count == 0
            ? split
            : [.. split.Select(_ => stillMixed.Contains(SliceKey(_.Placement)) ? Rename(_, $"{_.Placement.Slice}{_.Placement.SliceKind}", "kind") : _)];
    }

    static HashSet<string> MixedSlices(IEnumerable<CritterStackDerivedPlacement> placements) =>
    [
        .. placements
            .GroupBy(_ => SliceKey(_.Placement), StringComparer.Ordinal)
            .Where(_ => _.Select(placement => placement.Placement.SliceKind).Distinct().Skip(1).Any())
            .Select(_ => _.Key)
    ];

    static CritterStackDerivedPlacement Rename(CritterStackDerivedPlacement placement, string slice, string reason) => placement with
    {
        Placement = placement.Placement with { Slice = slice },
        Explanation = $"{placement.Explanation}; mixedSliceKindSplit=by-{reason}(from={placement.Placement.Slice}, to={slice})"
    };

    static GenerationDiagnostic Diagnostic(string slice, IReadOnlyList<CritterStackDerivedPlacement> placements)
    {
        var placement = placements
            .OrderBy(_ => _.Intent.Artifact.Subject.Value, StringComparer.Ordinal)
            .ThenBy(_ => _.Intent.Artifact.Kind)
            .First();
        var kinds = string.Join(", ", placements.Select(_ => _.Placement.SliceKind).Distinct().Order());
        var path = string.Join('.', [placement.Placement.Module, .. placement.Placement.Features, placement.Placement.Slice]);
        return new()
        {
            Code = CritterStackDiagnosticCodes.MixedSliceKindsSplit,
            Severity = GenerationDiagnosticSeverity.Warning,
            Outcome = GenerationDiagnosticOutcome.Conflict,
            Message = $"Source placement put {kinds} artifacts in the one slice '{path}', so they were split into one slice per command, aggregate or read model. " +
                      "To choose the slices yourself, give each slice its own folder or namespace segment (for example a Planning or Cards folder), " +
                      "or change which segments are used with --skip-segments, --feature-root or --module",
            Subject = placement.Intent.Artifact.Subject,
            Source = placement.Intent.Evidence.Source
        };
    }

    static string SliceKey(ArtifactPlacement placement) =>
        string.Join('\u001f', [placement.Module, .. placement.Features, placement.Slice]);
}
