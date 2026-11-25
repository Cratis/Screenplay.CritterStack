// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Cratis.Screenplay.Generation;
using Cratis.Screenplay.Generation.DotNet;
using Microsoft.CodeAnalysis;

namespace Cratis.CritterStack.Screenplay.Marten;

sealed record MartenSourceArtifact
{
    public required string Id { get; init; }
    public required SubjectId Subject { get; init; }
    public required ArtifactKind Kind { get; init; }
    public required string Name { get; init; }
    public required Evidence Evidence { get; init; }
    public string? File { get; init; }
    public INamedTypeSymbol? SourceType { get; init; }

    public void AddTo(MartenCollectedFacts collected)
    {
        var key = collected.Key(Subject, Kind);
        if (collected.NativeContext is not { } context)
        {
            collected.Facts.Add(new MartenArtifactFact
            {
                Id = Id,
                Subject = Subject,
                Kind = Kind,
                Name = Name,
                File = File,
                Evidence = Evidence,
                Properties = SourceType is null ? [] : DotNetTypeShapes.PropertiesOf(SourceType)
            }.ToFact(collected.Ownership));
            return;
        }

        collected.Facts.Add(new ArtifactDeclarationFact
        {
            Id = new() { Value = BackendOccurrenceKeys.FactId(Id, key.Scope) },
            Subject = Subject,
            Evidence = Evidence,
            Definition = new() { Artifact = key, Name = Name, File = File }
        });
        if (SourceType is not null)
        {
            var memberEvidence = CritterStackSource.EvidenceFor(SourceType, Evidence.Adapter, collected.Project, EvidenceStrength.Exact, "Declared Marten artifact members");
            var members = DotNetTypeUseFacts.Emit(SourceType, key, context, memberEvidence);
            collected.Facts.AddRange(members);
            collected.Facts.AddRange(members.OfType<ArtifactMemberTypeUseFact>()
                .Where(fact => fact.Definition.Type.ObservedTypeSubject is not null)
                .Select(fact => new TypeUseBindingPolicyFact
                {
                    Id = new() { Value = $"{fact.Id.Value}:binding" },
                    Subject = Subject,
                    Evidence = memberEvidence,
                    Definition = new() { TypeUse = fact.Id, Policy = TypeUseBindingPolicyKind.ConceptOnlyWithUnboundFallback }
                }));
        }
    }
}
