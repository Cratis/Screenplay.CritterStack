// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Cratis.Screenplay.Generation;
using Cratis.Screenplay.Generation.DotNet;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Cratis.CritterStack.Screenplay.Marten;

static class MartenDocumentSource
{
    static readonly HashSet<string> _configurationMethods = ["For", "RegisterDocumentType"];

    public static string SiteFor(SyntaxNode syntax, DotNetProjectCompilation project) => SiteFor(syntax.GetLocation(), project);

    public static string SiteFor(Location location, DotNetProjectCompilation project) =>
        $"{Uri.EscapeDataString(CritterStackSource.RangeForProject(location, project)?.Path ?? string.Empty)}:{location.SourceSpan.Start}:{location.SourceSpan.Length}";

    public static bool IsObservedUsage(RelationshipKind? kind, IMethodSymbol method, InvocationExpressionSyntax invocation, SemanticModel semanticModel) =>
        !IsIgnoredUsage(kind, invocation, semanticModel) &&
        (kind is not null || _configurationMethods.Contains(method.Name));

    public static OmissionDiagnosticEvidencePreference PreferenceFor(IMethodSymbol method)
    {
        if (MartenDocumentMembers.IsIdentityConfiguration(method)) return OmissionDiagnosticEvidencePreference.IdentityConfiguration;
        return _configurationMethods.Contains(method.Name)
            ? OmissionDiagnosticEvidencePreference.ExplicitRegistration
            : OmissionDiagnosticEvidencePreference.UseSite;
    }

    public static Evidence IdentityEvidence(
        DotNetProjectCompilation project,
        AdapterIdentity adapter,
        InvocationExpressionSyntax invocation,
        INamedTypeSymbol documentType) => EvidenceAt(project, adapter, invocation) with
        {
            Strength = EvidenceStrength.Configured,
            Explanation = $"Marten document identity configured through Schema.For<{documentType.Name}>().Identity(...)"
        };

    public static Evidence UsageEvidence(
        DotNetProjectCompilation project,
        AdapterIdentity adapter,
        InvocationExpressionSyntax invocation,
        string methodName) => EvidenceAt(project, adapter, invocation) with
        {
            Explanation = $"Marten document usage through {methodName}"
        };

    public static RelationshipKind? RelationshipKindFor(string methodName)
    {
        if (MartenSessionReadDiscovery.IsDocumentReadMethod(methodName)) return RelationshipKind.Reads;
        return methodName switch
        {
            "Insert" or "Store" => RelationshipKind.Stores,
            "Update" => RelationshipKind.Updates,
            "Delete" or "DeleteWhere" => RelationshipKind.Deletes,
            _ => null
        };
    }

    public static INamedTypeSymbol? DocumentTypeFrom(
        IMethodSymbol method,
        InvocationExpressionSyntax invocation,
        SemanticModel semanticModel) => MartenSessionReadDiscovery.DocumentTypeFrom(method, invocation, semanticModel);

    public static bool IsMarten(IMethodSymbol method, bool includeIntegrationApis) => MartenSessionReadDiscovery.IsMarten(method, includeIntegrationApis);

    public static bool IsSourceType(INamedTypeSymbol type) => type.TypeKind != TypeKind.Error && type.Locations.Any(_ => _.IsInSource);

    static Evidence EvidenceAt(DotNetProjectCompilation project, AdapterIdentity adapter, InvocationExpressionSyntax invocation) => new()
    {
        Adapter = adapter,
        Strength = EvidenceStrength.Exact,
        Source = CritterStackSource.RangeForProject(invocation.GetLocation(), project)
    };

    static bool IsIgnoredUsage(RelationshipKind? kind, InvocationExpressionSyntax invocation, SemanticModel semanticModel) =>
        (kind is RelationshipKind.Stores or RelationshipKind.Updates or RelationshipKind.Deletes &&
         IsInEventProjection(invocation, semanticModel)) ||
        (kind is not null && IsInUnresolvedCustomProcessor(invocation, semanticModel));

    static bool IsInEventProjection(InvocationExpressionSyntax invocation, SemanticModel semanticModel) =>
        semanticModel.GetEnclosingSymbol(invocation.SpanStart) is IMethodSymbol containingMethod &&
        DotNetSymbols.IsOrInheritsFrom(containingMethod.ContainingType, WellKnownTypes.MartenEventProjection);

    static bool IsInUnresolvedCustomProcessor(InvocationExpressionSyntax invocation, SemanticModel semanticModel) =>
        semanticModel.GetEnclosingSymbol(invocation.SpanStart) is IMethodSymbol containingMethod &&
        MartenConfigurationDiscovery.IsUnresolvedProcessorType(containingMethod.ContainingType);
}
