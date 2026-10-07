// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Globalization;

using Cratis.Screenplay.Generation;
using Cratis.Screenplay.Generation.DotNet;

namespace Cratis.CritterStack.Screenplay;

sealed record CritterStackPlacementIntent(
    string Id,
    ArtifactKey Artifact,
    SubjectId? SourceOwner,
    ArtifactPlacement CompatibilityPlacement,
    Evidence Evidence,
    Evidence? SliceKindEvidence = null);

static class CritterStackSourcePlacement
{
    public static IReadOnlyList<GenerationFact> Derive(
        DotNetAnalysisContext context,
        DotNetAdapterOptions options,
        IEnumerable<CritterStackPlacementIntent> intents,
        List<GenerationDiagnostic> diagnostics)
    {
        var orderedIntents = intents
            .OrderBy(_ => _.Artifact.Subject.Value, StringComparer.Ordinal)
            .ThenBy(_ => _.Artifact.Kind)
            .ThenBy(_ => _.SourceOwner?.Value, StringComparer.Ordinal)
            .ThenBy(_ => _.Id, StringComparer.Ordinal)
            .ThenBy(_ => (int)_.Evidence.Strength)
            .ThenBy(_ => _.Evidence.Adapter.Id, StringComparer.Ordinal)
            .ThenBy(_ => _.Evidence.Source?.Path, StringComparer.Ordinal)
            .ToArray();
        var unsupportedStrengthDiagnostics = orderedIntents
            .Where(_ => _.Evidence.Strength == EvidenceStrength.Unknown || !Enum.IsDefined(_.Evidence.Strength))
            .Select(UnsupportedEvidenceStrength)
            .ToArray();
        if (unsupportedStrengthDiagnostics.Length > 0)
        {
            diagnostics.AddRange(unsupportedStrengthDiagnostics);
            return [];
        }

        var strongestIntents = CollapseProducedEvents(RetainStrongest(orderedIntents));
        var snapshot = DotNetSourceStructures.Create(context);
        diagnostics.AddRange(snapshot.Diagnostics);
        if (!snapshot.IsSuccess)
        {
            return [];
        }

        var structures = snapshot.Structures.ToDictionary(_ => _.Subject);
        var missingStructures = strongestIntents
            .Where(intent => !structures.ContainsKey(EffectiveOwner(intent)))
            .GroupBy(intent => $"{intent.Artifact.Subject.Value}\u001f{(int)intent.Artifact.Kind}\u001f{EffectiveOwner(intent).Value}", StringComparer.Ordinal)
            .Select(_ => _.First())
            .Select(MissingSourceMapping)
            .OrderBy(_ => _.Code, StringComparer.Ordinal)
            .ThenBy(_ => _.Subject?.Value, StringComparer.Ordinal)
            .ThenBy(_ => _.Source?.Path, StringComparer.Ordinal)
            .ThenBy(_ => _.Source?.StartLine)
            .ThenBy(_ => _.Source?.StartColumn)
            .ThenBy(_ => _.Message, StringComparer.Ordinal)
            .ToArray();
        if (missingStructures.Length > 0)
        {
            diagnostics.AddRange(missingStructures);
            return [];
        }

        var requests = strongestIntents.Select(intent => new DotNetSourcePlacementRequest
        {
            Artifact = intent.Artifact,
            Structure = structures[EffectiveOwner(intent)],
            SourceOwner = intent.SourceOwner,
            SliceKind = intent.CompatibilityPlacement.SliceKind,
            Policy = options.SourceStructurePolicy,
            CompatibilityPolicy = new DotNetSourcePlacementCompatibilityPolicy
            {
                Version = 1,
                Placement = intent.CompatibilityPlacement
            }
        });
        var placementSnapshot = DotNetSourcePlacementDerivation.Derive(requests);
        diagnostics.AddRange(placementSnapshot.Diagnostics);
        if (!placementSnapshot.IsSuccess)
        {
            return [];
        }

        var selectedIntents = SelectRepresentatives(strongestIntents);
        var derived = placementSnapshot.Placements
            .Select(placement => new CritterStackDerivedPlacement(
                selectedIntents.Single(_ => _.Artifact == placement.Artifact),
                placement.Placement,
                ProvenanceExplanation(placement)))
            .ToArray();
        return
        [
            .. CritterStackSliceKindSplit.Split(derived, diagnostics).Select(placement => PlacementFact(
                placement.Intent,
                placement.Placement,
                placement.Intent.Evidence with
                {
                    Strength = EvidenceStrength.Heuristic,
                    Explanation = placement.Explanation
                }))
        ];
    }

    public static IReadOnlyList<GenerationFact> Compatibility(IEnumerable<CritterStackPlacementIntent> intents) =>
    [
        .. intents.Select(_ => PlacementFact(_, _.CompatibilityPlacement, _.Evidence))
    ];

    public static ArtifactPlacement CompatibilityPlacement(
        DotNetProjectCompilation project,
        DotNetAdapterOptions options,
        string feature,
        string slice,
        GenerationSliceKind sliceKind) => new()
        {
            Module = ScreenplayNames.Declaration(options.Module ?? project.Name),
            Features = [feature],
            Slice = slice,
            SliceKind = sliceKind
        };

    static SubjectId EffectiveOwner(CritterStackPlacementIntent intent) => intent.SourceOwner ?? intent.Artifact.Subject;

    static GenerationDiagnostic MissingSourceMapping(CritterStackPlacementIntent intent)
    {
        var owner = EffectiveOwner(intent);
        return new()
        {
            Code = DotNetSourceStructureDiagnosticCodes.MissingSourceMapping,
            Severity = GenerationDiagnosticSeverity.Error,
            Outcome = GenerationDiagnosticOutcome.Unsupported,
            Message = $"Source owner '{owner.Value}' for artifact '{intent.Artifact.Subject.Value}' has no host-supplied source structure",
            Subject = owner,
            Source = intent.Evidence.Source
        };
    }

    static GenerationDiagnostic UnsupportedEvidenceStrength(CritterStackPlacementIntent intent)
    {
        var strength = intent.Evidence.Strength;
        var isUnknown = strength == EvidenceStrength.Unknown;
        return new()
        {
            Code = GenerationDiagnosticCodes.UnsupportedEvidenceStrength,
            Severity = GenerationDiagnosticSeverity.Error,
            Outcome = isUnknown ? GenerationDiagnosticOutcome.Unknown : GenerationDiagnosticOutcome.Unsupported,
            Message = $"Placement intent '{intent.Id}' uses {(isUnknown ? "unknown" : "undefined")} EvidenceStrength value '{(int)strength}'; the entire placement batch was omitted",
            Subject = intent.Artifact.Subject,
            Source = intent.Evidence.Source
        };
    }

    static IReadOnlyList<CritterStackPlacementIntent> RetainStrongest(IEnumerable<CritterStackPlacementIntent> intents) =>
    [
        .. intents
            .GroupBy(_ => _.Artifact)
            .SelectMany(group =>
            {
                var strongest = group.Min(_ => _.Evidence.Strength);
                return group.Where(_ => _.Evidence.Strength == strongest);
            })
            .OrderBy(_ => _.Artifact.Subject.Value, StringComparer.Ordinal)
            .ThenBy(_ => _.Artifact.Kind)
            .ThenBy(_ => _.SourceOwner?.Value, StringComparer.Ordinal)
            .ThenBy(_ => _.Id, StringComparer.Ordinal)
    ];

    /// <summary>
    /// Gives an event one placement request: with its producing command when exactly one command produces it, otherwise from its own source.
    /// </summary>
    /// <remarks>
    /// An event that is also consumed by a projection carries a self-owned request from that projection; the producing
    /// command still wins, because an event belongs to the state change that appends it rather than to a state view.
    /// </remarks>
    static IReadOnlyList<CritterStackPlacementIntent> CollapseProducedEvents(IReadOnlyList<CritterStackPlacementIntent> intents) =>
    [
        .. intents
            .GroupBy(_ => _.Artifact)
            .SelectMany(group =>
            {
                var candidates = group.ToArray();
                if (group.Key.Kind != ArtifactKind.Event || candidates.Length == 1)
                {
                    return candidates;
                }

                var producers = candidates.Where(_ => _.SourceOwner is not null).ToArray();
                if (producers.Length == 0)
                {
                    return [candidates[0]];
                }

                return [producers.Select(_ => _.SourceOwner).Distinct().Count() == 1 ? producers[0] : candidates[0] with { SourceOwner = null }];
            })
            .OrderBy(_ => _.Artifact.Subject.Value, StringComparer.Ordinal)
            .ThenBy(_ => _.Artifact.Kind)
            .ThenBy(_ => _.SourceOwner?.Value, StringComparer.Ordinal)
            .ThenBy(_ => _.Id, StringComparer.Ordinal)
    ];

    static IReadOnlyList<CritterStackPlacementIntent> SelectRepresentatives(IEnumerable<CritterStackPlacementIntent> intents) =>
    [
        .. intents
            .GroupBy(_ => _.Artifact)
            .Select(_ => _.First())
            .OrderBy(_ => _.Artifact.Subject.Value, StringComparer.Ordinal)
            .ThenBy(_ => _.Artifact.Kind)
    ];

    static string ProvenanceExplanation(DotNetSourcePlacement placement)
    {
        var policy = placement.Policy;
        var compatibilityPolicy = placement.CompatibilityPolicy!;
        var compatibilityPlacement = compatibilityPolicy.Placement;
        var compatibilityPath = string.Join(
            '/',
            new[] { compatibilityPlacement.Module }
                .Concat(compatibilityPlacement.Features)
                .Append(compatibilityPlacement.Slice));
        var placementPolicy = placement.UsedCompatibilityPlacement
            ? "Explicit compatibility provides the Screenplay placement after strict DOTNETSP0004"
            : "Host-owned source structure provides the strict Screenplay placement";
        return $"{placementPolicy}; " +
               $"effectiveOwner={placement.SourceOwner?.Value ?? placement.Artifact.Subject.Value}; " +
               $"strictPolicy(version={policy.Version.ToString(CultureInfo.InvariantCulture)}, " +
               $"featureRoot={Optional(policy.FeatureRoot)}, " +
               $"namespaceSegmentsToSkip={policy.NamespaceSegmentsToSkip.ToString(CultureInfo.InvariantCulture)}, " +
               $"module={Optional(policy.Module)}); " +
               $"compatibilityPolicy(version={compatibilityPolicy.Version.ToString(CultureInfo.InvariantCulture)}, " +
               $"placement={compatibilityPath}:{compatibilityPlacement.SliceKind}); " +
               $"usedCompatibility={placement.UsedCompatibilityPlacement.ToString().ToLowerInvariant()}; " +
               $"compatibilityReason={placement.CompatibilityReasonCode ?? "<none>"}";
    }

    static string Optional(string? value) => value ?? "<absent>";

    static ArtifactPlacementFact PlacementFact(
        CritterStackPlacementIntent intent,
        ArtifactPlacement placement,
        Evidence evidence) => new()
        {
            Id = new FactId { Value = intent.Id },
            Subject = intent.Artifact.Subject,
            Artifact = intent.Artifact,
            Placement = placement,
            Evidence = intent.SliceKindEvidence is { } classification
                ? evidence with
                {
                    Explanation = $"{evidence.Explanation}; sliceClassification(strength={classification.Strength}, source={classification.Source?.Path}:{classification.Source?.StartLine}:{classification.Source?.StartColumn}): {classification.Explanation}"
                }
                : evidence
        };
}
