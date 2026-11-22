// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Cratis.Screenplay.Generation;

namespace Cratis.CritterStack.Screenplay.Wolverine;

/// <summary>
/// The inputs of one artifact fact.
/// </summary>
/// <param name="Id">The fact id.</param>
/// <param name="Key">The artifact key, which also carries the subject.</param>
/// <param name="Name">The artifact name.</param>
/// <param name="File">The authored source file, when known.</param>
/// <param name="Properties">The artifact properties.</param>
/// <param name="Evidence">The evidence for the artifact.</param>
sealed record WolverineArtifactFact(
    string Id,
    ArtifactKey Key,
    string Name,
    string? File,
    IReadOnlyList<PropertyDefinition> Properties,
    Evidence Evidence);

/// <summary>
/// The inputs of one relationship fact.
/// </summary>
/// <param name="Id">The fact id.</param>
/// <param name="Source">The relationship source subject.</param>
/// <param name="Kind">The relationship kind.</param>
/// <param name="Target">The relationship target subject.</param>
/// <param name="Evidence">The evidence for the relationship.</param>
sealed record WolverineRelationshipFact(
    string Id,
    SubjectId Source,
    RelationshipKind Kind,
    SubjectId Target,
    Evidence Evidence)
{
    /// <summary>
    /// Gets the source member, when the relationship is member-specific.
    /// </summary>
    public string? SourceMember { get; init; }

    /// <summary>
    /// Gets the target member, when the relationship is member-specific.
    /// </summary>
    public string? TargetMember { get; init; }

    /// <summary>
    /// Gets the discriminator that distinguishes parallel relationships of one kind.
    /// </summary>
    public string? Discriminator { get; init; }

    /// <summary>
    /// Gets whether the relationship targets a collection.
    /// </summary>
    public bool IsCollection { get; init; }

    /// <summary>
    /// Gets whether the relationship is optional.
    /// </summary>
    public bool IsOptional { get; init; }

    public BackendScopeKey? SourceScope { get; init; }

    public BackendScopeKey? TargetScope { get; init; }

    /// <summary>
    /// Gets whether the source is not bound to the owning backend scope.
    /// </summary>
    public bool UnscopedSource { get; init; }

    /// <summary>
    /// Gets the ownership of the target, when it differs from the owning backend.
    /// </summary>
    public BackendOwnership? TargetOwnership { get; init; }
}
