// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Cratis.CritterStack.Screenplay.for_CritterStackScreenplayGenerator;

public class when_generating_without_vogen_evidence : given.a_critter_stack_application
{
    GeneratedScreenplayDefinition _composed = null!;
    GeneratedScreenplayDefinition _critterStackOnly = null!;

    void Because()
    {
        var options = new CritterStackScreenplayOptions { Domain = "Banking" };
        _composed = new CritterStackScreenplayGenerator().Generate(Context.Projects, options);
        _critterStackOnly = new CritterStackScreenplayGenerator(
            new CritterStackScreenplayAdapter(),
            new ScreenplayDefinitionGenerator()).Generate(Context.Projects, options);
    }

    [Fact] void should_generate_byte_identical_source() => _composed.Source.ShouldEqual(_critterStackOnly.Source);
    [Fact] void should_generate_identical_diagnostics() => _composed.Diagnostics.ShouldEqual(_critterStackOnly.Diagnostics);
    [Fact] void should_not_add_vogen_provenance() => _composed.Graph.Artifacts.SelectMany(_ => _.Variants).SelectMany(_ => _.Evidence).Any(_ => _.Adapter.Id == "vogen").ShouldBeFalse();
}
