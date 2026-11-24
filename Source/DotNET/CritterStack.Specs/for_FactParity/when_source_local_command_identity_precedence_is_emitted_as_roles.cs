// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Cratis.CritterStack.Screenplay.given;
using static Cratis.CritterStack.Screenplay.for_FactParity.given.fact_vectors;

namespace Cratis.CritterStack.Screenplay.for_FactParity;

/// <summary>Source-local precedence remains adapter interpretation, not a shared eligibility or role-selection rule.</summary>
public class when_source_local_command_identity_precedence_is_emitted_as_roles : a_wolverine_command_members_application
{
    readonly List<(string Compatibility, string Atomic, string Reverse, bool Success)> _vectors = [];
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
        Observe(Input("Attributed", "Aggregate"), "key");
        Observe(Input("Conventional", "Aggregate"), "aggregateId");
        Observe(Input("Conventional"), "id");
        Observe(Input("Conventional", hasFetchedStreams: true), null);
        Observe(Input("Attributed", "Aggregate", linkedStreamIdentity: Member("Attributed", "Title")), "title");
    }

    [Fact] void should_cover_attribute_aggregate_convention_fetched_and_linked_precedence() => _vectors.Select(_ => _.Compatibility).ShouldEqualInOrder("key", "aggregateId", "id", string.Empty, "title");
    [Fact] void should_preserve_every_source_selected_role() => _vectors.TrueForAll(_ => _.Atomic == _.Compatibility).ShouldBeTrue();
    [Fact] void should_preserve_the_entire_role_decision_in_reverse_order() => _vectors.TrueForAll(_ => _.Reverse == _.Compatibility).ShouldBeTrue();
    [Fact] void should_verify_every_native_command() => _vectors.TrueForAll(_ => _.Success).ShouldBeTrue();

    void Observe(WolverineCommandIdentityInput input, string? observedMember)
    {
        var type = input.CommandType;
        var key = Key(_project, type, ArtifactKind.Command);
        var evidence = Evidence(_project, type, WolverineAdapter);
        var facts = new List<GenerationFact>(Surface(_project, type, key, WolverineAdapter))
        {
            new ArtifactPlacementFact
            {
                Id = new() { Value = "command-vector:placement" }, Subject = key.Subject, Artifact = key, Evidence = evidence,
                Placement = new() { Module = "Members", Features = ["Identity"], Slice = type.Name, SliceKind = GenerationSliceKind.StateChange }
            }
        };
        if (observedMember is not null)
        {
            facts.Add(Role(key, observedMember, evidence));
        }
        var command = Contribution(WolverineAdapter, facts);
        var peer = Contribution(MartenAdapter, []);
        var forward = Generate(command, peer);
        var reverse = Generate(peer, command);
        var compatibility = Identified(WolverineCommandMembers.Materialize(WolverineCommandMembers.Describe(type), WolverineCommandMembers.SelectIdentifier(input)));
        _vectors.Add((compatibility, Identifiers(Definition(forward, key)), Identifiers(Definition(reverse, key)), forward.IsSuccess && reverse.IsSuccess));
    }
}
