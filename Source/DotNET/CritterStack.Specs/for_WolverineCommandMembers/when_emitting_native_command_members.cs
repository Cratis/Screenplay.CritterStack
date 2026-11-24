// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using static Cratis.CritterStack.Screenplay.for_FactParity.given.fact_vectors;

namespace Cratis.CritterStack.Screenplay.for_WolverineCommandMembers;

public class when_emitting_native_command_members : Specification
{
    const string Source =
        """
        #nullable enable
        namespace NativeCommands;
        public record CustomerId(System.Guid Value);
        public enum Status { Open, Closed }
        public class BaseCommand { public string Inherited { get; set; } = ""; }
        public class ShipOrder : BaseCommand
        {
            public string Title { get; set; } = "";
            public CustomerId Id { get; set; } = new(System.Guid.Empty);
            public CustomerId? Alternate { get; set; }
            public System.Collections.Generic.List<CustomerId> Related { get; set; } = [];
            public Status Status { get; set; }
            public static string Static { get; set; } = "";
            public string WriteOnly { set { } }
            private string Hidden { get; set; } = "";
            public string PrivateGetter { private get; set; } = "";
            public string this[int index] => "";
        }
        """;

    IReadOnlyList<GenerationFact> _facts = [];
    IReadOnlyList<GenerationFact> _scopedFacts = [];
    GeneratedScreenplayDefinition _result = null!;
    ArtifactKey _key = null!;
    ArtifactDefinition _compatibility = null!;
    ArtifactMemberRoleFact _role = null!;
    ArtifactMemberRoleFact _scopedRole = null!;

    void Because()
    {
        var project = Project(Source);
        var type = Type(project, "NativeCommands.ShipOrder");
        _key = Key(project, type, ArtifactKind.Command);
        var evidence = Evidence(project, type, WolverineAdapter);
        var context = new DotNetAnalysisContext([project]);
        _facts = WolverineCommandMembers.DeclareNative(type, _key, context, evidence);
        var scoped = _key with { Scope = new() { Store = new() { Value = "other-store" } } };
        _scopedFacts = WolverineCommandMembers.DeclareNative(type, scoped, context, evidence);
        var input = new WolverineCommandIdentityInput(type, null, [], false, null);
        _role = WolverineCommandMembers.IdentifyNative(input, _key, Evidence(project, type, MartenAdapter));
        _scopedRole = WolverineCommandMembers.IdentifyNative(input, scoped, Evidence(project, type, MartenAdapter));
        _result = Generate(Contribution(WolverineAdapter, [Declaration(_key, type, evidence), .. _facts]));
        _compatibility = new() { Key = _key, Name = type.Name, Properties = DotNetTypeShapes.PropertiesOf(type) };
    }

    [Fact] void should_preserve_the_compatibility_member_shapes_without_a_concept_peer() => Members(Definition(_result, _key)).ShouldEqual(Members(_compatibility));
    [Fact] void should_keep_only_the_declared_public_readable_instance_properties() => _facts.OfType<ArtifactMemberDeclarationFact>().Select(_ => _.Definition.Member.Name).ShouldEqualInOrder("title", "id", "alternate", "related", "status");
    [Fact] void should_emit_one_exact_type_use_per_property() => _facts.OfType<ArtifactMemberTypeUseFact>().Count().ShouldEqual(5);
    [Fact] void should_keep_the_source_subject_on_wrapped_types() => TypeUse("id").Definition.Type.ObservedTypeSubject.ShouldNotBeNull();
    [Fact] void should_keep_nullable_source_types() => TypeUse("alternate").Definition.Type.Shape.ShouldContain(TypeUseShapeKind.Optional);
    [Fact] void should_keep_collection_source_types() => TypeUse("related").Definition.Type.Shape.ShouldContain(TypeUseShapeKind.Collection);
    [Fact] void should_only_allow_concept_peers_to_bind_source_type_references() => _facts.OfType<TypeUseBindingPolicyFact>().ShouldEachConformTo(_ => _.Definition.Policy == TypeUseBindingPolicyKind.ConceptOnlyWithUnboundFallback);
    [Fact] void should_cover_every_source_type_use_with_a_binding_policy() => _facts.OfType<TypeUseBindingPolicyFact>().Count().ShouldEqual(4);
    [Fact] void should_leave_identifier_roles_to_the_integration_emitter() => _facts.OfType<ArtifactMemberRoleFact>().ShouldBeEmpty();
    [Fact] void should_select_the_conventional_identifier_in_the_integration() => _role.Definition.Member.Name.ShouldEqual("id");
    [Fact] void should_not_collide_member_fact_ids_between_store_scopes() => _facts.Select(_ => _.Id).Intersect(_scopedFacts.Select(_ => _.Id)).ShouldBeEmpty();
    [Fact] void should_not_collide_role_fact_ids_between_store_scopes() => _role.Id.ShouldNotEqual(_scopedRole.Id);
    [Fact] void should_preserve_the_scope_on_the_role_target() => _scopedRole.Definition.Member.Artifact.Scope.Store.Value.ShouldEqual("other-store");

    ArtifactMemberTypeUseFact TypeUse(string member) => _facts.OfType<ArtifactMemberTypeUseFact>().Single(_ => _.Definition.Member.Name == member);
}
