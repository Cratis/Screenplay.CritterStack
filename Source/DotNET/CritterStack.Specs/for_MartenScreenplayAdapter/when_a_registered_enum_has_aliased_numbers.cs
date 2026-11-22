// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Cratis.CritterStack.Screenplay.for_MartenScreenplayAdapter;

public class when_a_registered_enum_has_aliased_numbers : Screenplay.given.an_enum_application
{
    AdapterContribution _marten = null!;
    AdapterContribution _critter = null!;
    GeneratedScreenplayDefinition _martenFirst = null!;
    GeneratedScreenplayDefinition _critterFirst = null!;

    void Establish() => Project = CreateProject(declaration: "public enum Priority { Urgent = 1, Normal = 1, Low = 2 }", registerEnum: true);

    void Because()
    {
        _marten = new MartenScreenplayAdapter().Analyze(new([Project]), new());
        _critter = new CritterStackScreenplayAdapter().Analyze(new([Project]), new());
        var options = new ScreenplayGenerationOptions { Domain = "Ordering" };
        _martenFirst = new ScreenplayDefinitionGenerator().Generate([_marten, _critter], options);
        _critterFirst = new ScreenplayDefinitionGenerator().Generate([_critter, _marten], options);
    }

    [Fact] void should_report_the_numbering_once() => _marten.Diagnostics.Count(_ => _.Code == CritterStackDiagnosticCodes.EnumNumbersNotPreserved).ShouldEqual(1);
    [Fact] void should_not_contribute_a_marten_concept() => _marten.Facts.OfType<ArtifactFact>().Any(_ => _.Definition.Key.Kind == ArtifactKind.Concept).ShouldBeFalse();
    [Fact] void should_not_contribute_a_representation() => _marten.Facts.OfType<ConceptRepresentationFact>().ShouldBeEmpty();
    [Fact] void should_not_reference_a_concept_from_the_document() => _marten.Facts.OfType<ArtifactFact>().Single(_ => _.Definition.Key.Kind == ArtifactKind.Document).Definition.Properties.Single(_ => _.Name == "priority").Type.Subject.ShouldBeNull();
    [Fact] void should_not_contribute_a_concept_from_the_compatibility_adapter() => _critter.Facts.OfType<ArtifactFact>().Any(_ => _.Definition.Key.Kind == ArtifactKind.Concept).ShouldBeFalse();
    [Fact] void should_not_declare_the_concept_when_marten_comes_first() => _martenFirst.Source.ShouldNotContain("concept Priority");
    [Fact] void should_not_declare_the_concept_when_marten_comes_last() => _critterFirst.Source.ShouldNotContain("concept Priority");
}
