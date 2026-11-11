// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Cratis.Screenplay.Generation;
using Cratis.Screenplay.Generation.DotNet;
using Microsoft.CodeAnalysis;

namespace Cratis.CritterStack.Screenplay.Wolverine;

sealed record WolverineSagaCorrelationScope(
    DotNetProjectCompilation Project,
    AdapterIdentity Adapter,
    INamedTypeSymbol SagaType,
    INamedTypeSymbol MessageType,
    IReadOnlyList<ISymbol> Members);

/// <summary>
/// Selects the message member Wolverine correlates a saga message with. The sources are tried in precedence order and the
/// first one that either selects a member or proves the selection ambiguous wins:
/// [SagaIdentity], then [SagaIdentityFrom], then the full saga name convention, then the suffix-stripped saga name, then
/// SagaId, then a case-insensitive Id.
/// </summary>
static class WolverineSagaCorrelations
{
    public static WolverineSagaCorrelation For(
        DotNetProjectCompilation project,
        AdapterIdentity adapter,
        INamedTypeSymbol sagaType,
        INamedTypeSymbol messageType,
        IReadOnlyList<WolverineSagaRoleMethod> allRoles)
    {
        var scope = new WolverineSagaCorrelationScope(project, adapter, sagaType, messageType, [.. MessageMembers(messageType, project)]);
        return Declared(scope, allRoles)
            ?? Attributed(scope)
            ?? Conventional(scope)
            ?? new(null, null);
    }

    static WolverineSagaCorrelation? Attributed(WolverineSagaCorrelationScope scope) => Resolve(
        scope,
        MatchMember(scope.Members, member => DotNetSymbols.HasAttribute(member, WellKnownTypes.WolverineSagaIdentityAttribute)),
        "multiple public [SagaIdentity] members are visible",
        EvidenceStrength.Exact,
        "Exact Wolverine [SagaIdentity] message member");

    static WolverineSagaCorrelation? Declared(WolverineSagaCorrelationScope scope, IReadOnlyList<WolverineSagaRoleMethod> allRoles)
    {
        var specified = allRoles
            .Where(role => SymbolEqualityComparer.Default.Equals(role.MessageType, scope.MessageType))
            .SelectMany(role => role.Method.Parameters.Select(SagaIdentityFrom))
            .OfType<(string MemberName, IParameterSymbol Parameter)>()
            .ToArray();
        var specifiedNames = specified.Select(_ => _.MemberName).Distinct(StringComparer.Ordinal).ToArray();
        return specifiedNames.Length switch
        {
            0 => FullName(scope),
            1 => SpecifiedName(scope, specifiedNames[0], specified[0].Parameter),
            _ => UnresolvedCorrelation(
                scope.SagaType,
                scope.MessageType,
                "conflicting [SagaIdentityFrom] member names are declared across parameters in the same message chain",
                SagaIdentityFromEvidence(scope.Project, scope.Adapter, specified[0].Parameter))
        };
    }

    static WolverineSagaCorrelation? SpecifiedName(WolverineSagaCorrelationScope scope, string name, IParameterSymbol parameter)
    {
        var specifiedMember = MatchMember(scope.Members, member => string.Equals(member.Name, name, StringComparison.Ordinal));
        if (specifiedMember.IsAmbiguous)
        {
            return UnresolvedCorrelation(scope.SagaType, scope.MessageType, $"multiple public members match explicit [SagaIdentityFrom] name '{name}'");
        }

        return specifiedMember.Member is null
            ? null
            : new(LowerFirst(specifiedMember.Member.Name), SagaIdentityFromEvidence(scope.Project, scope.Adapter, parameter));
    }

    static WolverineSagaCorrelation? FullName(WolverineSagaCorrelationScope scope) => ByName(scope, $"{scope.SagaType.Name}Id");

    static WolverineSagaCorrelation? Conventional(WolverineSagaCorrelationScope scope)
    {
        var suffixStrippedName = $"{scope.SagaType.Name.Replace("Saga", string.Empty, StringComparison.InvariantCultureIgnoreCase)}Id";
        foreach (var expectedName in new[] { suffixStrippedName, "SagaId" }.Distinct(StringComparer.Ordinal))
        {
            if (ByName(scope, expectedName) is { } correlation)
            {
                return correlation;
            }
        }

        return Resolve(
            scope,
            MatchMember(scope.Members, member => string.Equals(member.Name, "Id", StringComparison.OrdinalIgnoreCase)),
            "multiple public members match Wolverine's case-insensitive Id convention",
            EvidenceStrength.Conventional,
            "Wolverine case-insensitive Id saga correlation convention");
    }

    static WolverineSagaCorrelation? ByName(WolverineSagaCorrelationScope scope, string name) => Resolve(
        scope,
        MatchMember(scope.Members, member => string.Equals(member.Name, name, StringComparison.Ordinal)),
        $"multiple public members match Wolverine convention '{name}'",
        EvidenceStrength.Conventional,
        $"Wolverine saga correlation member convention '{name}'");

    static WolverineSagaCorrelation? Resolve(
        WolverineSagaCorrelationScope scope,
        WolverineSagaMemberMatch match,
        string ambiguousReason,
        EvidenceStrength strength,
        string explanation)
    {
        if (match.IsAmbiguous)
        {
            return UnresolvedCorrelation(scope.SagaType, scope.MessageType, ambiguousReason);
        }

        return match.Member is null
            ? null
            : CorrelationFromMember(scope.Project, scope.Adapter, match.Member, strength, explanation);
    }

    static (string MemberName, IParameterSymbol Parameter)? SagaIdentityFrom(IParameterSymbol parameter)
    {
        var attribute = parameter.GetAttributes().FirstOrDefault(candidate =>
            candidate.AttributeClass is not null &&
            DotNetSubjectIds.MetadataName(candidate.AttributeClass.OriginalDefinition) == WellKnownTypes.WolverineSagaIdentityFromAttribute);
        return attribute?.ConstructorArguments.FirstOrDefault().Value is string memberName
            ? (memberName, parameter)
            : null;
    }

    static Evidence SagaIdentityFromEvidence(
        DotNetProjectCompilation project,
        AdapterIdentity adapter,
        IParameterSymbol parameter) => CritterStackSource.EvidenceFor(
            parameter,
            adapter,
            project,
            EvidenceStrength.Exact,
            "Exact Wolverine [SagaIdentityFrom] handler parameter");

    static WolverineSagaCorrelation CorrelationFromMember(
        DotNetProjectCompilation project,
        AdapterIdentity adapter,
        ISymbol member,
        EvidenceStrength strength,
        string explanation) => new(
            LowerFirst(member.Name),
            CritterStackSource.EvidenceFor(member, adapter, project, strength, explanation));

    static WolverineSagaCorrelation UnresolvedCorrelation(
        INamedTypeSymbol sagaType,
        INamedTypeSymbol messageType,
        string reason,
        Evidence? evidence = null) => new(
            null,
            evidence,
            $"Saga handler for '{sagaType.Name}' cannot safely select an authored correlation member for '{messageType.Name}' because {reason}; correlation remains runtime-resolved");

    static WolverineSagaMemberMatch MatchMember(IEnumerable<ISymbol> members, Func<ISymbol, bool> predicate)
    {
        var matches = members.Where(predicate).Take(2).ToArray();
        return new(matches.Length == 1 ? matches[0] : null, matches.Length > 1);
    }

    static IEnumerable<ISymbol> MessageMembers(INamedTypeSymbol messageType, DotNetProjectCompilation project)
    {
        var hierarchy = TypeHierarchy(messageType).ToArray();
        return hierarchy
            .SelectMany(type => type.GetMembers().OfType<IFieldSymbol>())
            .Where(member => WolverineSagaFacts.IsPublicAuthoredMember(member, project))
            .Cast<ISymbol>()
            .Concat(hierarchy
                .SelectMany(type => type.GetMembers().OfType<IPropertySymbol>())
                .Where(member => !member.IsIndexer && WolverineSagaFacts.IsPublicAuthoredMember(member, project)));
    }

    static IEnumerable<INamedTypeSymbol> TypeHierarchy(INamedTypeSymbol type)
    {
        for (var current = type; current is not null; current = current.BaseType)
        {
            yield return current;
        }
    }

    static string LowerFirst(string value) => WolverineSagaFacts.LowerFirst(value);
}
