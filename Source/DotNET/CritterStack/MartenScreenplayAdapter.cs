// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Cratis.CritterStack.Screenplay.Marten;
using Cratis.Screenplay.Generation;
using Cratis.Screenplay.Generation.DotNet;
using Microsoft.CodeAnalysis;

namespace Cratis.CritterStack.Screenplay;

/// <summary>
/// Analyzes authored Marten application source into framework-neutral Screenplay facts.
/// </summary>
public sealed class MartenScreenplayAdapter : IDescribedDotNetScreenplayAdapter, IDotNetScreenplayAdapter
{
    const string AdapterId = "marten";
    const string AdapterVersion = "1.0.0";

    /// <inheritdoc/>
    public AdapterIdentity Identity { get; } = new() { Id = AdapterId, Version = AdapterVersion };

    /// <inheritdoc/>
    public AdapterDescriptor Descriptor { get; } = new()
    {
        Identity = new AdapterIdentity { Id = AdapterId, Version = AdapterVersion },
        SourceLanguage = AdapterSourceLanguage.CSharp,
        Category = AdapterCategory.EventStore,
        CompatibleGenerationVersions = new GenerationVersionRange
        {
            MinimumInclusive = new Version(0, 17, 0)
        },
        RequiredHostCapabilities =
        [
            AdapterHostCapability.AuthoredSource,
            AdapterHostCapability.StableSourceLocations,
            AdapterHostCapability.SemanticAnalysis
        ],
        RequiredApiCapabilities = [CritterStackAdapterApiCapabilities.MartenApplication],
        EmittedFactCapabilities =
        [
            GenerationFactCapability.Artifact,
            GenerationFactCapability.ArtifactPlacement,
            GenerationFactCapability.Relationship
        ]
    };

    /// <inheritdoc/>
    public bool CanAnalyze(DotNetAnalysisContext context) => Probe(context) is AdapterProbeApplicable;

    /// <inheritdoc/>
    public AdapterProbeResult Probe(DotNetAnalysisContext context)
    {
        var evidence = new List<AdapterProbeEvidence>();
        try
        {
            foreach (var project in context.Projects)
            {
                evidence.AddRange(AuthoredMartenUsesIn(project));
            }
        }
        catch (DotNetSourceTreeNotMapped)
        {
            return UnsafeSourceMapping();
        }

        if (evidence.Count == 0)
        {
            return new AdapterProbeNotApplicable();
        }

        if (evidence.Exists(item => item.Source?.FileIdentity is null))
        {
            return UnsafeSourceMapping();
        }

        return new AdapterProbeApplicable
        {
            Evidence =
            [
                .. evidence
                    .OrderBy(item => item.Subject?.Value, StringComparer.Ordinal)
                    .ThenBy(item => item.Source?.FileIdentity?.Project, StringComparer.Ordinal)
                    .ThenBy(item => item.Source?.FileIdentity?.Path, StringComparer.Ordinal)
                    .ThenBy(item => item.Source?.StartLine)
                    .ThenBy(item => item.Source?.StartColumn)
                    .ThenBy(item => item.Description, StringComparer.Ordinal)
            ]
        };
    }

    /// <inheritdoc/>
    public AdapterContribution Analyze(DotNetAnalysisContext context, DotNetAdapterOptions options)
    {
        var facts = new List<GenerationFact>();
        var diagnostics = new List<GenerationDiagnostic>();
        var placements = new List<CritterStackPlacementIntent>();
        var subjects = new CritterStackSubjectResolver(context);

        foreach (var project in context.Projects)
        {
            var subjectCheckpoint = subjects.Checkpoint();
            var marten = MartenFacts.Discover(project, options, Identity, subjects);
            var documents = MartenDocumentFacts.Discover(
                project,
                Identity,
                subjects,
                marten.Documents,
                includeIntegrationApis: false);
            if (subjects.HasBlockingDiagnosticsSince(subjectCheckpoint))
            {
                continue;
            }

            facts.AddRange(marten.Facts);
            facts.AddRange(documents.Facts);
            diagnostics.AddRange(marten.Diagnostics);
            diagnostics.AddRange(documents.Diagnostics);
            placements.AddRange(marten.Placements ?? []);
        }

        diagnostics.AddRange(subjects.Diagnostics);
        facts.AddRange(CritterStackSourcePlacement.Derive(context, options, placements, diagnostics));

        return new AdapterContribution
        {
            Adapter = Identity,
            Facts = facts,
            Diagnostics = diagnostics
        };
    }

    static IEnumerable<AdapterProbeEvidence> AuthoredMartenUsesIn(DotNetProjectCompilation project)
    {
        foreach (var tree in project.AuthoredSyntaxTrees.Where(tree => !DotNetGeneratedSource.IsGenerated(tree)))
        {
            var semanticModel = project.Compilation.GetSemanticModel(tree);
            foreach (var invocation in DotNetSource.AuthoredInvocationsIn(tree.GetRoot(), project))
            {
                if (DotNetInvocations.MethodFor(invocation, semanticModel) is not { } method || !IsOwnedMartenMethod(method))
                {
                    continue;
                }

                var source = DotNetSource.RangeForProject(invocation.GetLocation(), project);
                var containingSymbol = semanticModel.GetEnclosingSymbol(invocation.SpanStart);
                yield return new AdapterProbeEvidence
                {
                    Description = $"Authored source invokes exact Marten API '{DotNetMethodSignatures.From(method).Name}'",
                    ApiCapability = CritterStackAdapterApiCapabilities.MartenApplication,
                    Source = source,
                    Subject = containingSymbol is IMethodSymbol containingMethod
                        ? DotNetMethodIdentity.SubjectFor(project, containingMethod)
                        : null
                };
            }
        }
    }

    static bool IsOwnedMartenMethod(IMethodSymbol method)
    {
        var candidate = method.ReducedFrom ?? method;
        var @namespace = candidate.ContainingNamespace.ToDisplayString();
        return string.Equals(@namespace, "Marten", StringComparison.Ordinal) ||
               @namespace.StartsWith("Marten.", StringComparison.Ordinal);
    }

    static AdapterProbeBlocked UnsafeSourceMapping() => new()
    {
        Diagnostics =
        [
            new GenerationDiagnostic
            {
                Code = MartenDiagnosticCodes.UnsafeSourceMapping,
                Severity = GenerationDiagnosticSeverity.Error,
                Outcome = GenerationDiagnosticOutcome.Unknown,
                Message = "Applicable authored Marten source does not have authoritative stable source mapping"
            }
        ]
    };
}
