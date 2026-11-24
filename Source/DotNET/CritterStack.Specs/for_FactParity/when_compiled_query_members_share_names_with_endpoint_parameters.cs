// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Cratis.CritterStack.Screenplay.given;
using static Cratis.CritterStack.Screenplay.for_FactParity.given.fact_vectors;

namespace Cratis.CritterStack.Screenplay.for_FactParity;

/// <summary>
/// Primitive HTTP parameters are intentionally filtered by the current adapter. The independent compiled-plan
/// surface, including fields, must still transfer its ordered members without borrowing the compatibility query.
/// </summary>
public class when_compiled_query_members_share_names_with_endpoint_parameters : a_compiled_query_member_vector_application
{
    ArtifactKey _query = null!;
    ArtifactDefinition _compatibility = null!;
    GeneratedScreenplayDefinition _forward = null!;
    GeneratedScreenplayDefinition _reverse = null!;
    AdapterContribution _inputs = null!;

    void Because()
    {
        var plan = Type(Project, "Vector.StudentsByName");
        var endpoint = Type(Project, "Vector.StudentEndpoints").GetMembers("Search").OfType<IMethodSymbol>().Single();
        var method = DotNetMethodIdentity.SubjectFor(Project, endpoint);
        _query = new() { Subject = new() { Value = $"{method.Value}:query" }, Kind = ArtifactKind.Query };
        var evidence = Evidence(Project, endpoint, WolverineAdapter);
        _compatibility = CompatibilityDefinition(Contribution, _query);
        var entryPoint = Contribution(WolverineAdapter,
        [
            new ArtifactDeclarationFact
            {
                Id = new() { Value = "input-vector:query" }, Subject = _query.Subject, Evidence = evidence,
                Definition = new() { Artifact = _query, Name = "Search", File = evidence.Source?.Path }
            },
            new EntryPointFact
            {
                Id = new() { Value = "input-vector:entry" }, Subject = method, Evidence = evidence,
                Definition = new() { Method = method, Artifact = _query }
            },
            new EntryPointInputTransferFact
            {
                Id = new() { Value = "input-vector:transfer" }, Subject = method, Evidence = evidence,
                Definition = new() { Method = method, Artifact = _query, Kind = RelationshipKind.Reads }
            },
            new ArtifactPlacementFact
            {
                Id = new() { Value = "input-vector:placement" }, Subject = _query.Subject, Artifact = _query, Evidence = evidence,
                Placement = new() { Module = "Vector", Features = ["Students"], Slice = "Search", SliceKind = GenerationSliceKind.StateView }
            }
        ]);
        var resultType = Type(Project, "Vector.StudentResult");
        var resultKey = Key(Project, resultType, ArtifactKind.ReadModel);
        var declaration = Project.SubjectForType(plan);
        var members = new[] { "Name", "Tag", "Take" }.Select(name => plan.GetMembers(name).Single()).ToArray();
        _inputs = Contribution(MartenAdapter,
        [
            new ArtifactUsageDeclarationFact
            {
                Id = new() { Value = "input-vector:mapping" }, Subject = declaration, Evidence = Evidence(Project, plan, MartenAdapter),
                Definition = new() { Declaration = declaration, Artifact = resultKey }
            },
            new MethodArtifactUsageFact
            {
                Id = new() { Value = "input-vector:usage" }, Subject = method, Evidence = Evidence(Project, plan, MartenAdapter),
                Definition = new() { Method = method, Declaration = declaration, Kind = RelationshipKind.Reads, ExecutionOrder = 0 }
            },
            .. members.SelectMany((member, order) => ObserveMember(member, declaration, order)),
            .. Surface(Project, resultType, resultKey, MartenAdapter),
            new RelationshipFact
            {
                Id = new() { Value = "input-vector:returns" }, Subject = _query.Subject, Evidence = evidence,
                Definition = new() { Key = new() { Source = _query.Subject, Kind = RelationshipKind.Returns, Target = resultKey.Subject } }
            }
        ]);
        _forward = Generate(_inputs, entryPoint);
        _reverse = Generate(entryPoint, _inputs);
    }

    [Fact] void should_preserve_the_compatibility_query_key() => Definition(_forward, _query).Key.ShouldEqual(_compatibility.Key);
    [Fact] void should_preserve_member_order_types_and_flags() => Members(Definition(_forward, _query)).ShouldEqual(Members(_compatibility));
    [Fact] void should_preserve_the_name_member_from_the_compiled_plan() => Definition(_forward, _query).Properties.Single(_ => _.Name == "name").Type.Name.ShouldEqual("Int");
    [Fact] void should_preserve_the_field_after_the_properties() => Definition(_forward, _query).Properties.Select(_ => _.Name).ShouldEqualInOrder("name", "tag", "take");
    [Fact] void should_preserve_the_reverse_order_surface() => Members(Definition(_reverse, _query)).ShouldEqual(Members(_compatibility));
    [Fact] void should_retain_every_input_observation() => _inputs.Facts.OfType<DeclarationInputMemberTypeUseFact>().Count().ShouldEqual(3);
    [Fact] void should_verify_both_atomic_orders() => _forward.Diagnostics.Concat(_reverse.Diagnostics).Where(_ => _.Severity == GenerationDiagnosticSeverity.Error).Select(_ => $"{_.Code}: {_.Message}").ShouldBeEmpty();

    IReadOnlyList<GenerationFact> ObserveMember(ISymbol symbol, SubjectId declaration, int order)
    {
        var type = symbol is IPropertySymbol property ? property.Type : ((IFieldSymbol)symbol).Type;
        var member = new DeclarationMemberKey { Declaration = declaration, Name = $"{char.ToLowerInvariant(symbol.Name[0])}{symbol.Name[1..]}" };
        var evidence = Evidence(Project, symbol, MartenAdapter);
        return
        [
            new DeclarationInputMemberDeclarationFact
            {
                Id = new() { Value = $"input-vector:{symbol.Name}:declaration" }, Subject = declaration, Evidence = evidence,
                Definition = new() { Member = member, DeclarationOrder = order }
            },
            new DeclarationInputMemberTypeUseFact
            {
                Id = new() { Value = $"input-vector:{symbol.Name}:type" }, Subject = declaration, Evidence = evidence,
                Definition = new() { Member = member, Type = DotNetTypeShapes.TypeUseFor(type, new([Project])) }
            }
        ];
    }
}
