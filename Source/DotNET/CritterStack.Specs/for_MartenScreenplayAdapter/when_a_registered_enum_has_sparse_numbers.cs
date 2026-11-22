// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Cratis.CritterStack.Screenplay.for_MartenScreenplayAdapter;

public class when_a_registered_enum_has_sparse_numbers : Screenplay.given.an_enum_application
{
    AdapterContribution _contribution = null!;
    GeneratedScreenplayDefinition _result = null!;

    void Establish() => Project = CreateProject(declaration: "public enum Priority { Urgent = 10, Normal = 20, Low = 30 }", registerEnum: true);

    void Because()
    {
        _contribution = new MartenScreenplayAdapter().Analyze(new([Project]), new());
        _result = new ScreenplayDefinitionGenerator().Generate([_contribution], new ScreenplayGenerationOptions { Domain = "Ordering" });
    }

    [Fact] void should_keep_the_registered_concept() => _contribution.Facts.OfType<ArtifactFact>().Count(_ => _.Definition.Key.Kind == ArtifactKind.Concept).ShouldEqual(1);
    [Fact] void should_carry_the_numbers() => _contribution.Facts.OfType<ConceptRepresentationFact>().Single().Definition.EnumerationNumbers!.SequenceEqual([10, 20, 30]).ShouldBeTrue();
    [Fact] void should_not_report_a_numbering_warning() => _contribution.Diagnostics.Any(_ => _.Code == CritterStackDiagnosticCodes.EnumNumbersNotPreserved).ShouldBeFalse();
    [Fact] void should_emit_the_numbers() => _result.Source.ShouldContain("urgent = 10");
}
