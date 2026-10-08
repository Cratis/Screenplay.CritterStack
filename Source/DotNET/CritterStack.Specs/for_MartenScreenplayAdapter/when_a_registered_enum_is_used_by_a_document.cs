// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Cratis.CritterStack.Screenplay.for_MartenScreenplayAdapter;

public class when_a_registered_enum_is_used_by_a_document : Screenplay.given.an_enum_application
{
    AdapterContribution _contribution = null!;
    GeneratedScreenplayDefinition _result = null!;

    void Establish() => Project = CreateProject(registerEnum: true);

    void Because()
    {
        var adapter = new MartenScreenplayAdapter();
        _contribution = adapter.Analyze(new([Project]), new());
        _result = new ScreenplayDefinitionGenerator().Generate([_contribution], new ScreenplayGenerationOptions { Domain = "Ordering" });
    }

    [Fact] void should_reuse_the_registered_concept() => _contribution.Facts.OfType<ArtifactFact>().Count(_ => _.Definition.Key.Kind == ArtifactKind.Concept).ShouldEqual(1);
    [Fact] void should_preserve_the_registration_evidence() => _contribution.Facts.OfType<ArtifactFact>().Single(_ => _.Definition.Key.Kind == ArtifactKind.Concept).Evidence.Strength.ShouldEqual(EvidenceStrength.Configured);
    [Fact] void should_add_one_exact_representation() => _contribution.Facts.OfType<ConceptRepresentationFact>().Single().Evidence.Strength.ShouldEqual(EvidenceStrength.Exact);
    [Fact] void should_emit_a_usable_enum_for_direct_adapter_consumers() => _result.Source.ShouldContain("concept Priority : Enum");
    [Fact] void should_succeed_without_conflicting_facts() => _result.IsSuccess.ShouldBeTrue();
    [Fact] void should_bind_the_document_property() => _contribution.Facts.OfType<ArtifactFact>().Single(_ => _.Definition.Key.Kind == ArtifactKind.Document).Definition.Properties.Single(_ => _.Name == "priority").Type.Subject.ShouldEqual(_contribution.Facts.OfType<ConceptRepresentationFact>().Single().Subject);
}
