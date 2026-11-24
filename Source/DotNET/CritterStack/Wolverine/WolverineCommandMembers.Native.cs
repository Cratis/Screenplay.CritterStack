// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Cratis.Screenplay.Generation;
using Cratis.Screenplay.Generation.DotNet;
using Microsoft.CodeAnalysis;

namespace Cratis.CritterStack.Screenplay.Wolverine;

internal static partial class WolverineCommandMembers
{
    public static IReadOnlyList<GenerationFact> DeclareNative(
        INamedTypeSymbol commandType,
        ArtifactKey artifact,
        DotNetAnalysisContext context,
        Evidence evidence)
    {
        var members = DotNetTypeUseFacts.Emit(commandType, artifact, context, evidence);
        return
        [
            .. members,
            .. members.OfType<ArtifactMemberTypeUseFact>()
                .Where(fact => fact.Definition.Type.ObservedTypeSubject is not null)
                .Select(fact => new TypeUseBindingPolicyFact
                {
                    Id = new() { Value = $"{fact.Id.Value}:binding" },
                    Subject = artifact.Subject,
                    Evidence = evidence,
                    Definition = new() { TypeUse = fact.Id, Policy = TypeUseBindingPolicyKind.ConceptOnlyWithUnboundFallback }
                })
        ];
    }

    public static ArtifactMemberRoleFact? IdentifyNative(
        WolverineCommandIdentityInput input,
        ArtifactKey artifact,
        Evidence evidence)
    {
        var identifier = SelectIdentifier(input);
        if (identifier.Name is not { } name ||
            !DeclaresMember(input.CommandType, name))
        {
            return null;
        }

        return new()
        {
            Id = new() { Value = BackendOccurrenceKeys.FactId($"{evidence.Adapter.Id}:command-identifier:{artifact.Subject.Value}", artifact.Scope) },
            Subject = artifact.Subject,
            Evidence = evidence,
            Definition = new()
            {
                Member = new() { Artifact = artifact, Name = name },
                Role = ArtifactMemberRoleKind.EventSourceIdentifier
            }
        };
    }

    static bool DeclaresMember(INamedTypeSymbol type, string name) => type.GetMembers()
        .OfType<IPropertySymbol>()
        .Any(property => !property.IsStatic && !property.IsIndexer &&
                         property.DeclaredAccessibility == Accessibility.Public &&
                         property.GetMethod?.DeclaredAccessibility == Accessibility.Public &&
                         WolverineSagaFacts.LowerFirst(property.Name) == name);
}
