// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Cratis.CritterStack.Screenplay.for_CritterStackScreenplayGenerator;

public class when_composing_vogen_and_critter_stack_by_default : given.a_composed_vogen_critter_stack_application
{
    GeneratedScreenplayDefinition _result = null!;

    void Because() => _result = new CritterStackScreenplayGenerator().Generate(
        [Project],
        new CritterStackScreenplayOptions { Domain = "Ordering" });

    [Fact] void should_succeed() => _result.IsSuccess.ShouldBeTrue();
    [Fact] void should_generate_the_vogen_uuid_concept() => _result.Source.ShouldContain("concept OrderId : Uuid");
    [Fact] void should_generate_the_vogen_text_concept() => _result.Source.ShouldContain("concept CustomerCode : String");
    [Fact] void should_resolve_the_command_concept_usage() => _result.Source.ShouldContain("id OrderId");
    [Fact] void should_preserve_nullable_concept_usage() => _result.Source.ShouldContain("referralCode CustomerCode?");
    [Fact] void should_preserve_the_named_validation_rule() => _result.Source.ShouldContain("rule Validate");
    [Fact] void should_preserve_the_constant_validation_message() => _result.Source.ShouldContain("message \"Customer codes cannot be blank\"");
    [Fact] void should_keep_vogen_concept_provenance() => EvidenceFor(ArtifactKind.Concept, "CustomerCode").Single().Adapter.Id.ShouldEqual("vogen");
    [Fact] void should_keep_critter_stack_command_provenance() => EvidenceFor(ArtifactKind.Command, "PlaceOrder").Single().Adapter.Id.ShouldEqual("cratis.critter-stack");
    [Fact] void should_keep_the_vogen_display_path() => EvidenceFor(ArtifactKind.Concept, "CustomerCode").Single().Source.Path.ShouldEqual("Ordering/Application.cs");
    [Fact] void should_keep_the_critter_stack_display_path() => EvidenceFor(ArtifactKind.Command, "PlaceOrder").Single().Source.Path.ShouldEqual("Ordering/Application.cs");
    [Fact] void should_attach_stable_source_identity_to_vogen_evidence() => EvidenceFor(ArtifactKind.Concept, "CustomerCode").Single().Source.FileIdentity.ShouldEqual(SourceIdentity);
    [Fact] void should_attach_stable_source_identity_to_critter_stack_evidence() => EvidenceFor(ArtifactKind.Command, "PlaceOrder").Single().Source.FileIdentity.ShouldEqual(SourceIdentity);
    [Fact] void should_exclude_generated_only_vogen_declarations() => _result.Graph.Artifacts.Any(_ => _.Variants.Any(variant => variant.Definition.Name == "GeneratedOnly")).ShouldBeFalse();

    static SourceFileIdentity SourceIdentity => new() { Project = "Ordering/Ordering", Path = "Application.cs" };

    IReadOnlyList<Evidence> EvidenceFor(ArtifactKind kind, string name) => _result.Graph.Artifacts
        .Single(_ => _.Key.Kind == kind && _.Variants.Any(variant => variant.Definition.Name == name))
        .Variants
        .Single()
        .Evidence;
}
