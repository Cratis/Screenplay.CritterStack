// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Cratis.Screenplay.Generation;
using Cratis.Screenplay.Generation.DotNet;
using Microsoft.CodeAnalysis;

namespace Cratis.CritterStack.Screenplay.Marten;

internal static partial class MartenCompiledQueryDiscovery
{
    public static IReadOnlyList<GenerationFact> InputFacts(
        INamedTypeSymbol planType,
        SubjectId declaration,
        DotNetAnalysisContext context,
        Evidence evidence)
    {
        var members = ParameterSymbolsOf(planType)
            .GroupBy(member => LowerFirst(member.Name), StringComparer.Ordinal)
            .Select(group => group.First());
        return [.. members.SelectMany((symbol, order) => InputMemberFacts(symbol, order, new(declaration, context, evidence)))];
    }

    static List<GenerationFact> InputMemberFacts(ISymbol symbol, int order, InputContext context)
    {
        var member = new DeclarationMemberKey { Declaration = context.Declaration, Name = LowerFirst(symbol.Name) };
        var prefix = $"{context.Evidence.Adapter.Id}:compiled-query-input:{context.Declaration.Value}:{order}:{symbol.Name}";
        var typeUse = new DeclarationInputMemberTypeUseFact
        {
            Id = new() { Value = $"{prefix}:type" },
            Subject = context.Declaration,
            Evidence = context.Evidence,
            Definition = new() { Member = member, Type = DotNetTypeShapes.TypeUseFor(TypeOf(symbol), context.Analysis) }
        };
        var facts = new List<GenerationFact>
        {
            new DeclarationInputMemberDeclarationFact
            {
                Id = new() { Value = $"{prefix}:declaration" },
                Subject = context.Declaration,
                Evidence = context.Evidence,
                Definition = new() { Member = member, DeclarationOrder = order }
            },
            typeUse
        };
        if (typeUse.Definition.Type.ObservedTypeSubject is not null)
        {
            facts.Add(new TypeUseBindingPolicyFact
            {
                Id = new() { Value = $"{prefix}:binding" },
                Subject = context.Declaration,
                Evidence = context.Evidence,
                Definition = new() { TypeUse = typeUse.Id, Policy = TypeUseBindingPolicyKind.ConceptOnlyWithUnboundFallback }
            });
        }

        return facts;
    }

    sealed record InputContext(SubjectId Declaration, DotNetAnalysisContext Analysis, Evidence Evidence);
}
