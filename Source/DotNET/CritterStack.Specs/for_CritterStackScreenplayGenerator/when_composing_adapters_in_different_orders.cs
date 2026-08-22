// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Cratis.CritterStack.Screenplay.for_CritterStackScreenplayGenerator;

public class when_composing_adapters_in_different_orders : given.a_composed_vogen_critter_stack_application
{
    GeneratedScreenplayDefinition _first = null!;
    GeneratedScreenplayDefinition _second = null!;

    void Because()
    {
        var options = new CritterStackScreenplayOptions { Domain = "Ordering" };
        _first = new CritterStackScreenplayGenerator(
            [new VogenConceptScreenplayAdapter(), new CritterStackScreenplayAdapter()]).Generate([Project], options);
        _second = new CritterStackScreenplayGenerator(
            [new CritterStackScreenplayAdapter(), new VogenConceptScreenplayAdapter()]).Generate([Project], options);
    }

    [Fact] void should_generate_byte_identical_source() => _second.Source.ShouldEqual(_first.Source);
    [Fact] void should_generate_identical_diagnostics() => _second.Diagnostics.ShouldEqual(_first.Diagnostics);
    [Fact] void should_keep_both_adapter_identities() => _first.Graph.Artifacts.SelectMany(_ => _.Variants).SelectMany(_ => _.Evidence).Select(_ => _.Adapter.Id).Distinct().ShouldContainOnly("vogen", "cratis.critter-stack");
}
