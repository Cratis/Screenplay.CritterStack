// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Cratis.Screenplay.Generation;
using Cratis.Screenplay.Generation.DotNet;
using Microsoft.CodeAnalysis;

namespace Cratis.CritterStack.Screenplay.Wolverine;

internal static partial class WolverineFacts
{
    public static IReadOnlyList<IParameterSymbol> QueryParameterSymbols(IMethodSymbol method) =>
        [.. method.Parameters.Where(parameter => !IsInfrastructureParameter(parameter.Type) && !IsSourceType(parameter.Type))];

    public static IReadOnlyList<GenerationFact> QueryMemberFacts(
        IMethodSymbol method,
        ArtifactKey artifact,
        DotNetAnalysisContext analysis,
        Evidence evidence)
    {
        var parameters = QueryParameterSymbols(method);
        var context = new QueryMemberContext(artifact, analysis, evidence, QueryIdentifier(parameters));
        return
        [
            .. parameters.GroupBy(parameter => LowerFirst(parameter.Name), StringComparer.Ordinal)
                .Select(group => group.First())
                .SelectMany(parameter => QueryParameterFacts(parameter, context))
        ];
    }

    static IParameterSymbol? QueryIdentifier(IReadOnlyList<IParameterSymbol> parameters) =>
        parameters.FirstOrDefault(parameter => !IsAuthoredEnum(parameter.Type));

    static List<GenerationFact> QueryParameterFacts(IParameterSymbol parameter, QueryMemberContext context)
    {
        var member = new ArtifactMemberKey { Artifact = context.Artifact, Name = LowerFirst(parameter.Name) };
        var prefix = BackendOccurrenceKeys.FactId(
            $"{context.Evidence.Adapter.Id}:query-parameter:{context.Artifact.Subject.Value}:{parameter.Ordinal}", context.Artifact.Scope);
        var typeUse = new ArtifactMemberTypeUseFact
        {
            Id = new() { Value = $"{prefix}:type" },
            Subject = context.Artifact.Subject,
            Evidence = context.Evidence,
            Definition = new() { Member = member, Type = DotNetTypeShapes.TypeUseFor(parameter.Type, context.Analysis) }
        };
        var facts = new List<GenerationFact>
        {
            new ArtifactMemberDeclarationFact
            {
                Id = new() { Value = $"{prefix}:declaration" }, Subject = context.Artifact.Subject, Evidence = context.Evidence,
                Definition = new() { Member = member, DeclarationOrder = parameter.Ordinal }
            },
            typeUse
        };
        if (typeUse.Definition.Type.ObservedTypeSubject is not null)
        {
            facts.Add(new TypeUseBindingPolicyFact
            {
                Id = new() { Value = $"{prefix}:binding" },
                Subject = context.Artifact.Subject,
                Evidence = context.Evidence,
                Definition = new() { TypeUse = typeUse.Id, Policy = TypeUseBindingPolicyKind.ConceptOnlyWithUnboundFallback }
            });
        }
        if (SymbolEqualityComparer.Default.Equals(parameter, context.Identifier))
        {
            facts.Add(new ArtifactMemberRoleFact
            {
                Id = new() { Value = $"{prefix}:identifier" },
                Subject = context.Artifact.Subject,
                Evidence = context.Evidence,
                Definition = new() { Member = member, Role = ArtifactMemberRoleKind.EventSourceIdentifier }
            });
        }

        return facts;
    }

    sealed record QueryMemberContext(ArtifactKey Artifact, DotNetAnalysisContext Analysis, Evidence Evidence, IParameterSymbol? Identifier);
}
