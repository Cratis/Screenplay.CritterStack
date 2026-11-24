// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using static Cratis.CritterStack.Screenplay.for_FactParity.given.fact_vectors;

namespace Cratis.CritterStack.Screenplay.for_CompiledQueryMemberVector;

public class when_emitting_source_native_input_members : Specification
{
    const string Source =
        """
        #nullable enable
        namespace Marten.Events.CodeGeneration { public sealed class MartenIgnoreAttribute : System.Attribute; }
        namespace NativePlans
        {
            public enum Status { Active, Inactive }
            public class ParentPlan
            {
                public int Shadow;
                public System.Guid Inherited { get; set; }
            }
            public class Plan : ParentPlan
            {
                public new long Shadow;
                public Status? Status { get; set; }
                public string[]? Tags { get; set; }
                public decimal Total;
                [Marten.Events.CodeGeneration.MartenIgnore] public string Ignored { get; set; } = "";
                public static string Cached { get; set; } = "";
                private string Hidden { get; set; } = "";
                public string WriteOnly { set { } }
                public string PrivateGetter { private get; set; } = "";
                public string this[int index] => "";
            }
        }
        """;

    IReadOnlyList<ISymbol> _symbols = [];
    IReadOnlyList<GenerationFact> _facts = [];
    Evidence _evidence = null!;
    SubjectId _declaration = null!;
    TypeUseDefinition _derivedType = null!;

    void Because()
    {
        var project = Project(Source);
        var type = Type(project, "NativePlans.Plan");
        var context = new DotNetAnalysisContext([project]);
        _declaration = project.SubjectForType(type);
        _evidence = Evidence(project, type, new WolverineMartenScreenplayAdapter().Identity);
        _symbols = MartenCompiledQueryDiscovery.ParameterSymbolsOf(type);
        _facts = MartenCompiledQueryDiscovery.InputFacts(type, _declaration, context, _evidence);
        _derivedType = DotNetTypeShapes.TypeUseFor(((IFieldSymbol)type.GetMembers("Shadow").Single()).Type, context);
    }

    [Fact] void should_observe_only_public_readable_unignored_instance_members_including_inherited_members() => _symbols.Select(_ => _.Name).ShouldEqualInOrder("Inherited", "Shadow", "Shadow", "Status", "Tags", "Total");
    [Fact] void should_preserve_the_compatibility_first_member_wins_order() => _facts.OfType<DeclarationInputMemberDeclarationFact>().Select(_ => _.Definition.Member.Name).ShouldEqualInOrder("inherited", "shadow", "status", "tags", "total");
    [Fact] void should_emit_one_exact_type_use_per_distinct_member_name() => _facts.OfType<DeclarationInputMemberTypeUseFact>().Count().ShouldEqual(5);
    [Fact] void should_prefer_the_derived_hidden_member_type() => TypeUse("shadow").Name.ShouldEqual(_derivedType.Name);
    [Fact] void should_keep_the_source_subject_on_named_inputs() => TypeUse("status").ObservedTypeSubject.ShouldNotBeNull();
    [Fact] void should_keep_optional_inputs() => TypeUse("status").Shape.ShouldContain(TypeUseShapeKind.Optional);
    [Fact] void should_keep_optional_collection_inputs() => TypeUse("tags").Shape.ShouldEqualInOrder(TypeUseShapeKind.Optional, TypeUseShapeKind.Collection, TypeUseShapeKind.Named);
    [Fact] void should_only_allow_concept_peers_to_bind_source_type_references() => _facts.OfType<TypeUseBindingPolicyFact>().Single().Definition.Policy.ShouldEqual(TypeUseBindingPolicyKind.ConceptOnlyWithUnboundFallback);
    [Fact] void should_keep_the_declaration_subject_on_all_input_facts() => _facts.ShouldEachConformTo(_ => _.Subject == _declaration);
    [Fact] void should_keep_the_source_evidence_on_all_input_facts() => _facts.ShouldEachConformTo(_ => _.Evidence == _evidence);
    [Fact] void should_not_repeat_fact_ids_when_member_names_are_hidden() => _facts.Select(_ => _.Id).Distinct().Count().ShouldEqual(_facts.Count);
    [Fact] void should_not_construct_compatibility_artifacts() => _facts.OfType<ArtifactFact>().ShouldBeEmpty();

    TypeUseDefinition TypeUse(string name) => _facts.OfType<DeclarationInputMemberTypeUseFact>().Single(_ => _.Definition.Member.Name == name).Definition.Type;
}
