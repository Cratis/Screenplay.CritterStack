// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Cratis.CritterStack.Screenplay.given;
using static Cratis.CritterStack.Screenplay.for_FactParity.given.fact_vectors;

namespace Cratis.CritterStack.Screenplay.for_FactParity;

/// <summary>Source-local precedence remains adapter interpretation, not a shared eligibility or role-selection rule.</summary>
public class when_source_local_command_identity_precedence_is_emitted_as_roles : a_wolverine_command_members_application
{
    readonly List<(string Compatibility, string Atomic, string Reverse, bool Success)> _vectors = [];
    readonly List<bool> _memberParity = [];
    readonly List<AdapterContribution> _commands = [];
    readonly List<ArtifactMemberRoleFact> _identifiers = [];
    DotNetProjectCompilation _project = null!;

    void Because()
    {
        _project = new()
        {
            Name = "Members",
            ProjectPath = "/workspace/Members/Members.csproj",
            SourceRoot = "/workspace",
            Compilation = Compilation,
            AuthoredSyntaxTrees = Compilation.SyntaxTrees.ToHashSet()
        };
        Observe(Input("Attributed", "Aggregate"));
        Observe(Input("Conventional", "Aggregate"));
        Observe(Input("Conventional"));
        Observe(Input("Conventional", hasFetchedStreams: true));
        Observe(Input("Attributed", "Aggregate", linkedStreamIdentity: Member("Attributed", "Title")));
        Observe(Input("Plain"));
        Observe(Input("Plain", "Aggregate"));
    }

    [Fact] void should_cover_attribute_aggregate_convention_fetched_and_linked_precedence() => _vectors.Select(_ => _.Compatibility).ShouldEqualInOrder("key", "aggregateId", "id", string.Empty, "title", string.Empty, string.Empty);
    [Fact] void should_preserve_every_source_selected_role() => _vectors.TrueForAll(_ => _.Atomic == _.Compatibility).ShouldBeTrue();
    [Fact] void should_preserve_the_entire_role_decision_in_reverse_order() => _vectors.TrueForAll(_ => _.Reverse == _.Compatibility).ShouldBeTrue();
    [Fact] void should_verify_every_native_command() => _vectors.TrueForAll(_ => _.Success).ShouldBeTrue();

    [Fact] void should_preserve_every_materialized_member() => _memberParity.ShouldEachConformTo(_ => _);
    [Fact] void should_not_embed_compatibility_artifacts_in_wolverine() => _commands.SelectMany(_ => _.Facts).OfType<ArtifactFact>().ShouldBeEmpty();
    [Fact] void should_not_assign_persistence_roles_in_wolverine() => _commands.SelectMany(_ => _.Facts).OfType<ArtifactMemberRoleFact>().ShouldBeEmpty();
    [Fact] void should_emit_the_four_selected_identifiers_from_the_integration() => _identifiers.Count.ShouldEqual(4);
    [Fact] void should_retain_the_integration_producer_on_roles() => _identifiers.ShouldEachConformTo(_ => _.Evidence.Adapter == MartenAdapter);

    void Observe(WolverineCommandIdentityInput input)
    {
        var type = input.CommandType;
        var key = Key(_project, type, ArtifactKind.Command);
        var evidence = Evidence(_project, type, WolverineAdapter);
        var facts = new List<GenerationFact>(WolverineCommandMembers.DeclareNative(type, key, new([_project]), evidence))
        {
            Declaration(key, type, evidence),
            new ArtifactPlacementFact
            {
                Id = new() { Value = "command-vector:placement" }, Subject = key.Subject, Artifact = key, Evidence = evidence,
                Placement = new() { Module = "Members", Features = ["Identity"], Slice = type.Name, SliceKind = GenerationSliceKind.StateChange }
            }
        };
        var role = WolverineCommandMembers.IdentifyNative(input, key, Evidence(_project, type, MartenAdapter));
        if (role is not null) _identifiers.Add(role);
        var command = Contribution(WolverineAdapter, facts);
        _commands.Add(command);
        var peer = Contribution(MartenAdapter, role is null ? [] : [role]);
        var forward = Generate(command, peer);
        var reverse = Generate(peer, command);
        var properties = WolverineCommandMembers.Materialize(WolverineCommandMembers.Describe(type), WolverineCommandMembers.SelectIdentifier(input));
        _memberParity.Add(System.Text.Json.JsonSerializer.Serialize(properties) == Members(Definition(forward, key)));
        _vectors.Add((Identified(properties), Identifiers(Definition(forward, key)), Identifiers(Definition(reverse, key)), forward.IsSuccess && reverse.IsSuccess));
    }
}
