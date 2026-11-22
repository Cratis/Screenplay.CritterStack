// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Cratis.Screenplay.Generation;
using Cratis.Screenplay.Generation.DotNet;
using Microsoft.CodeAnalysis;

namespace Cratis.CritterStack.Screenplay.Marten;

/// <summary>
/// Describes one artifact fact the Marten adapters emit, so call sites name every value instead of passing long argument lists.
/// </summary>
sealed record MartenArtifactFact
{
    public required string Id { get; init; }

    public required SubjectId Subject { get; init; }

    public required ArtifactKind Kind { get; init; }

    public required string Name { get; init; }

    public string? File { get; init; }

    public IReadOnlyList<PropertyDefinition> Properties { get; init; } = [];

    public required Evidence Evidence { get; init; }

    public ArtifactFact ToFact(BackendOwnership? ownership = null) => new()
    {
        Id = new FactId { Value = BackendOccurrenceKeys.FactId(Id, BackendOccurrenceKeys.Artifact(Subject, Kind, ownership ?? BackendOwnership.Legacy).Scope) },
        Subject = Subject,
        Definition = new ArtifactDefinition
        {
            Key = BackendOccurrenceKeys.Artifact(Subject, Kind, ownership ?? BackendOwnership.Legacy),
            Name = Name,
            File = File,
            Properties = Properties
        },
        Evidence = Evidence
    };
}

/// <summary>
/// Describes one relationship fact the Marten adapters emit, so call sites name every value instead of passing long argument lists.
/// </summary>
sealed record MartenRelationshipFact
{
    public required string Id { get; init; }

    public required SubjectId Source { get; init; }

    public required RelationshipKind Kind { get; init; }

    public required SubjectId Target { get; init; }

    public required Evidence Evidence { get; init; }

    public string? SourceMember { get; init; }

    public string? TargetMember { get; init; }

    public string? Discriminator { get; init; }

    public bool IsCollection { get; init; }

    public RelationshipFact ToFact(BackendScopeKey? sourceScope = null, BackendScopeKey? targetScope = null) => new()
    {
        Id = new FactId { Value = BackendOccurrenceKeys.RelationshipId(Id, sourceScope, targetScope) },
        Subject = Source,
        Definition = new RelationshipDefinition
        {
            Key = new RelationshipKey
            {
                Kind = Kind,
                Source = Source,
                Target = Target,
                Discriminator = Discriminator,
                SourceScope = sourceScope,
                TargetScope = targetScope
            },
            SourceMember = SourceMember,
            TargetMember = TargetMember,
            IsCollection = IsCollection
        },
        Evidence = Evidence
    };
}

static class MartenSourceFiles
{
    public static string? FileOf(ISymbol symbol, DotNetProjectCompilation project) =>
        CritterStackSource.EvidenceFor(symbol, new AdapterIdentity { Id = "source", Version = "1" }, project, EvidenceStrength.Exact).Source?.Path;
}
