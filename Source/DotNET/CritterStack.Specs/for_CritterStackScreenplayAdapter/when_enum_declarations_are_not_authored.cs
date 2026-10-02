// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Cratis.CritterStack.Screenplay.for_CritterStackScreenplayAdapter;

public class when_enum_declarations_are_not_authored : given.an_enum_application
{
    IReadOnlyList<AdapterContribution> _contributions = null!;

    void Because() => _contributions =
    [
        Analyze(CreateProject(authoredEnum: false)),
        Analyze(CreateProject(enumPath: "Priority.g.cs")),
        Analyze(CreateProject(declaration: "// no authored enum", propertyType: "System.DayOfWeek"))
    ];

    [Fact] void should_not_emit_enum_concepts() => _contributions.SelectMany(_ => _.Facts).OfType<ArtifactFact>().Any(_ => _.Definition.Key.Kind == ArtifactKind.Concept).ShouldBeFalse();
    [Fact] void should_not_emit_enum_representations() => _contributions.SelectMany(_ => _.Facts).OfType<ConceptRepresentationFact>().ShouldBeEmpty();
    [Fact] void should_still_emit_the_artifacts_using_the_types() => _contributions.All(_ => _.Facts.OfType<ArtifactFact>().Any(artifact => artifact.Definition.Name == "PlaceOrder")).ShouldBeTrue();

    static AdapterContribution Analyze(DotNetProjectCompilation project) => new CritterStackScreenplayAdapter().Analyze(new([project]), new());
}
