// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Cratis.CritterStack.Screenplay.Canonical;

namespace Cratis.CritterStack.Screenplay.for_Expectations;

public class when_checking_a_fan_out_whose_child_is_not_a_collection : given.a_marten_multi_stream_grouping_application
{
    const string FanOut = "OrderImported|LineImported|lines|after-grouping";

    bool _collectionMet;
    bool _singularMet;

    void Because()
    {
        _collectionMet = IsFanOutMet(Graph);

        var singular = new AdapterContribution
        {
            Adapter = Contribution.Adapter,
            Diagnostics = Contribution.Diagnostics,
            Facts =
            [
                .. Contribution.Facts.Select(fact => fact is RelationshipFact relationship &&
                    relationship.Definition.Key.Discriminator?.StartsWith("marten:fan-out-child:", StringComparison.Ordinal) == true
                        ? relationship with { Definition = relationship.Definition with { IsCollection = false } }
                        : fact)
            ]
        };
        _singularMet = IsFanOutMet(new GenerationResolver().Resolve([singular]));
    }

    [Fact] void should_meet_the_fan_out_while_its_child_is_a_collection() => _collectionMet.ShouldBeTrue();
    [Fact] void should_not_meet_the_fan_out_when_its_child_is_not_a_collection() => _singularMet.ShouldBeFalse();

    bool IsFanOutMet(ResolvedApplicationGraph graph) => new Expectation(ExpectationKind.FanOut, FanOut).IsMetBy(
        new GeneratedScreenplayDefinition
        {
            Application = null!,
            Source = string.Empty,
            Diagnostics = Contribution.Diagnostics,
            Graph = graph
        });
}
