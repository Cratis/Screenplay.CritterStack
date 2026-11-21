// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Cratis.CritterStack.Screenplay.for_CritterStackScreenplayGenerator;

public class when_attribute_middleware_stage_resolution_keeps_dcb_production : given.a_wolverine_dcb_competition_application
{
    GeneratedScreenplayDefinition _result = null!;

    void Because() => _result = new CritterStackScreenplayGenerator().Generate([Project], new CritterStackScreenplayOptions { Domain = "Accounts" });

    [Fact] void should_resolve_the_before_stages_on_all_four_dcb_chains() => _result.Diagnostics.Count(_ => _.Code == WolverineDiagnosticCodes.CompoundStageOmitted).ShouldEqual(4);
    [Fact] void should_keep_ordered_dcb_production_declarative() => Produces("Ordered", "OrderedDone").ShouldBeTrue();
    [Fact] void should_keep_reversed_dcb_production_declarative() => Produces("Reversed", "ReversedDone").ShouldBeTrue();
    [Fact] void should_keep_partial_dcb_production_declarative() => Produces("Split", "SplitDone").ShouldBeTrue();
    [Fact] void should_keep_the_before_locations_in_both_source_files() => _result.Diagnostics.Where(_ => _.Code == WolverineDiagnosticCodes.CompoundStageOmitted).Select(_ => _.Source.Path).Distinct().ShouldContainOnly("Accounts/Types.cs", "Accounts/CompanionZ.cs");

    bool Produces(string command, string @event) => _result.Graph.Relationships.Any(_ =>
        _.Key.Kind == RelationshipKind.Produces && _.Key.Discriminator == "declarative" &&
        _.Key.Source.Value.EndsWith($".{command}", StringComparison.Ordinal) && _.Key.Target.Value.EndsWith($".{@event}", StringComparison.Ordinal));
}
