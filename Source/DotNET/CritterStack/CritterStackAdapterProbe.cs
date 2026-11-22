// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Cratis.CritterStack.Screenplay.Marten;
using Cratis.Screenplay.Generation;
using Cratis.Screenplay.Generation.DotNet;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Cratis.CritterStack.Screenplay;

/// <summary>
/// Shared, emission-neutral probing of authored source for exact framework API use. It never consults another adapter.
/// </summary>
static class CritterStackAdapterProbe
{
    /// <summary>
    /// Stable diagnostic for applicable authored Wolverine source without authoritative stable source mapping.
    /// </summary>
    internal const string WolverineUnsafeSourceMapping = "WOLVERINE9001";

    /// <summary>
    /// Stable diagnostic for applicable authored Wolverine-Marten integration source without authoritative stable source mapping.
    /// </summary>
    internal const string IntegrationUnsafeSourceMapping = "WOLVERINEMARTEN9001";

    public static bool IsMartenApi(string @namespace) =>
        string.Equals(@namespace, "Marten", StringComparison.Ordinal) || @namespace.StartsWith("Marten.", StringComparison.Ordinal);

    public static bool IsIntegrationApi(string @namespace) =>
        string.Equals(@namespace, "Wolverine.Marten", StringComparison.Ordinal) ||
        @namespace.StartsWith("Wolverine.Marten.", StringComparison.Ordinal) ||
        string.Equals(@namespace, "Wolverine.Http.Marten", StringComparison.Ordinal) ||
        @namespace.StartsWith("Wolverine.Http.Marten.", StringComparison.Ordinal);

    public static bool IsWolverineApi(string @namespace) =>
        (string.Equals(@namespace, "Wolverine", StringComparison.Ordinal) || @namespace.StartsWith("Wolverine.", StringComparison.Ordinal)) &&
        !IsIntegrationApi(@namespace);

    /// <summary>
    /// Collects exact authored invocations and attribute applications whose symbol lives in an owned namespace.
    /// </summary>
    /// <param name="context">The analysis context.</param>
    /// <param name="isOwned">Selects the namespaces the evidence is for.</param>
    /// <param name="capability">The capability the evidence proves.</param>
    /// <param name="family">The framework family used in the evidence description.</param>
    /// <param name="includeAttributes">Whether applied attributes count as evidence in addition to invocations.</param>
    /// <returns>The evidence in discovery order; empty when the family is absent.</returns>
    /// <exception cref="DotNetSourceTreeNotMapped">An applicable use is in a tree without stable source mapping.</exception>
    public static IReadOnlyList<AdapterProbeEvidence> EvidenceFor(
        DotNetAnalysisContext context,
        Func<string, bool> isOwned,
        AdapterApiCapability capability,
        string family,
        bool includeAttributes)
    {
        return
        [
            .. context.Projects.SelectMany(project => project.AuthoredSyntaxTrees
                .Where(tree => !DotNetGeneratedSource.IsGenerated(tree))
                .SelectMany(tree => EvidenceIn(project, tree, isOwned, capability, family, includeAttributes)))
        ];
    }

    public static AdapterProbeResult Applicable(IReadOnlyList<AdapterProbeEvidence> evidence, string unsafeCode, string family)
    {
        if (evidence.Count == 0)
        {
            return new AdapterProbeNotApplicable();
        }

        if (evidence.Any(item => item.Source?.FileIdentity is null))
        {
            return UnsafeSourceMapping(unsafeCode, family);
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

    public static AdapterProbeBlocked UnsafeSourceMapping(string code, string family) => new()
    {
        Diagnostics =
        [
            new GenerationDiagnostic
            {
                Code = code,
                Severity = GenerationDiagnosticSeverity.Error,
                Outcome = GenerationDiagnosticOutcome.Unknown,
                Message = $"Applicable authored {family} source does not have authoritative stable source mapping"
            }
        ]
    };

    static IEnumerable<AdapterProbeEvidence> EvidenceIn(
        DotNetProjectCompilation project,
        SyntaxTree tree,
        Func<string, bool> isOwned,
        AdapterApiCapability capability,
        string family,
        bool includeAttributes)
    {
        var model = project.Compilation.GetSemanticModel(tree);
        var invocations = DotNetSource.AuthoredInvocationsIn(tree.GetRoot(), project)
            .Select(invocation => (Node: (SyntaxNode)invocation, Method: DotNetInvocations.MethodFor(invocation, model)))
            .Where(use => use.Method is not null && isOwned(NamespaceOf(use.Method.ReducedFrom ?? use.Method)))
            .Select(use => Evidence(
                project,
                model,
                use.Node,
                capability,
                $"Authored source invokes exact {family} API '{DotNetMethodSignatures.From(use.Method!).Name}'"));
        var attributes = includeAttributes
            ? tree.GetRoot().DescendantNodes().OfType<AttributeSyntax>()
                .Select(attribute => (Node: (SyntaxNode)attribute, Constructor: model.GetSymbolInfo(attribute).Symbol as IMethodSymbol))
                .Where(use => use.Constructor is not null && isOwned(use.Constructor.ContainingNamespace.ToDisplayString()))
                .Select(use => Evidence(
                    project,
                    model,
                    use.Node,
                    capability,
                    $"Authored source applies exact {family} attribute '{use.Constructor!.ContainingType.Name}'"))
            : [];
        return invocations.Concat(attributes);
    }

    static string NamespaceOf(IMethodSymbol method) => method.ContainingNamespace.ToDisplayString();

    static AdapterProbeEvidence Evidence(
        DotNetProjectCompilation project,
        SemanticModel model,
        SyntaxNode node,
        AdapterApiCapability capability,
        string description)
    {
        var containingSymbol = model.GetEnclosingSymbol(node.SpanStart);
        return new AdapterProbeEvidence
        {
            Description = description,
            ApiCapability = capability,
            Source = DotNetSource.RangeForProject(node.GetLocation(), project),
            Subject = containingSymbol is IMethodSymbol containingMethod
                ? DotNetMethodIdentity.SubjectFor(project, containingMethod)
                : null
        };
    }
}
