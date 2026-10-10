// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Cratis.CritterStack.Screenplay.Marten;
using Cratis.Screenplay.Generation;
using Cratis.Screenplay.Generation.DotNet;
using Microsoft.CodeAnalysis;

namespace Cratis.CritterStack.Screenplay.Wolverine;

internal static partial class WolverineFacts
{
    static bool IsIgnored(ISymbol symbol) =>
        DotNetSymbols.HasAttribute(symbol, WellKnownTypes.WolverineIgnoreAttribute) ||
        DotNetSymbols.HasAttribute(symbol, WellKnownTypes.WolverineLegacyIgnoreAttribute);

    static bool IsPublicSourceType(INamedTypeSymbol type, DotNetProjectCompilation project) =>
        type.DeclaredAccessibility == Accessibility.Public && type.Locations.Any(_ => IsAuthoredSourceLocation(_, project));

    static bool IsPublicSourceMethod(IMethodSymbol method, DotNetProjectCompilation project) =>
        method.DeclaredAccessibility == Accessibility.Public &&
        method.MethodKind == MethodKind.Ordinary &&
        method.Locations.Any(_ => IsAuthoredSourceLocation(_, project));

    static bool IsAuthoredSourceLocation(Location location, DotNetProjectCompilation project) =>
        location.IsInSource &&
        location.SourceTree is not null &&
        project.AuthoredSyntaxTrees.Contains(location.SourceTree) &&
        !DotNetGeneratedSource.IsGenerated(location.SourceTree);

    static bool IsSourceType(ITypeSymbol type) =>
        type is INamedTypeSymbol { TypeKind: not TypeKind.Enum } named && named.Locations.Any(_ => _.IsInSource);

    static bool IsAuthoredEnum(ITypeSymbol type) =>
        type is INamedTypeSymbol { TypeKind: TypeKind.Enum } named && named.Locations.Any(_ => _.IsInSource);

    static INamedTypeSymbol? MessageElementType(ITypeSymbol type) => type switch
    {
        IArrayTypeSymbol { ElementType: INamedTypeSymbol element } when IsSourceType(element) => element,
        INamedTypeSymbol named when IsSourceType(named) => named,
        _ => null
    };

    static bool IsEventPayloadType(ITypeSymbol type) =>
        type is INamedTypeSymbol named &&
        type.SpecialType == SpecialType.None &&
        (WolverineReturnConsequences.IsTimeoutMessage(named) || !WolverineReturnTypes.IsSpecialReturn(named)) &&
        !DotNetSubjectIds.MetadataName(named.OriginalDefinition).StartsWith("System.", StringComparison.Ordinal);

    static bool IsInfrastructureParameter(ITypeSymbol type)
    {
        if (WolverineEventStreams.IsEventStream(type))
        {
            return true;
        }

        if (type.SpecialType != SpecialType.None)
        {
            return true;
        }

        return type is INamedTypeSymbol named &&
               IsInfrastructureMetadataName(DotNetSubjectIds.MetadataName(named.OriginalDefinition));
    }

    static bool IsInfrastructureMetadataName(string metadataName) =>
        _infrastructureNamespacePrefixes.Any(prefix => metadataName.StartsWith(prefix, StringComparison.Ordinal)) ||
        metadataName == WellKnownTypes.JasperFxEventStoreOperations ||
        metadataName == "System.Threading.CancellationToken";

    static ArtifactFact Artifact(
        string id,
        ArtifactKey key,
        string name,
        string? file,
        IReadOnlyList<PropertyDefinition> properties,
        Evidence evidence) => new()
        {
            Id = new FactId { Value = id },
            Subject = key.Subject,
            Definition = new ArtifactDefinition
            {
                Key = key,
                Name = name,
                File = file,
                Properties = properties
            },
            Evidence = evidence
        };

    static RelationshipFact Relationship(
        string id,
        SubjectId source,
        RelationshipKind kind,
        SubjectId target,
        Evidence evidence,
        string? sourceMember = null,
        string? targetMember = null,
        string? discriminator = null,
        bool isCollection = false,
        bool isOptional = false) => new()
        {
            Id = new FactId { Value = id },
            Subject = source,
            Definition = new RelationshipDefinition
            {
                Key = new RelationshipKey
                {
                    Kind = kind,
                    Source = source,
                    Target = target,
                    Discriminator = discriminator
                },
                SourceMember = sourceMember,
                TargetMember = targetMember,
                IsCollection = isCollection,
                IsOptional = isOptional
            },
            Evidence = evidence
        };

    static Evidence MethodEvidence(
        IMethodSymbol method,
        DotNetProjectCompilation project,
        AdapterIdentity adapter,
        EvidenceStrength strength,
        string explanation) =>
        CritterStackSource.EvidenceFor(method, adapter, project, strength, explanation);

    static SubjectId MethodSubject(DotNetProjectCompilation project, IMethodSymbol method, string role) => new()
    {
        Value = $"{DotNetMethodIdentity.SubjectFor(project, method).Value}:{role}"
    };

    static string? SourceFileOf(ISymbol symbol, DotNetProjectCompilation project) =>
        CritterStackSource.EvidenceFor(symbol, new AdapterIdentity { Id = "source", Version = "1" }, project, EvidenceStrength.Exact).Source?.Path;

    static string LowerFirst(string value) => value.Length == 0
        ? value
        : $"{char.ToLowerInvariant(value[0])}{value[1..]}";
}
