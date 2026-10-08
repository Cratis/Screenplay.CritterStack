// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Cratis.CritterStack.Screenplay.for_CritterStackScreenplayGenerator;

public class when_regenerating_a_community_kitchen_from_reordered_source : given.a_community_kitchen_application
{
    GeneratedScreenplayDefinition _first = null!;
    GeneratedScreenplayDefinition _second = null!;
    GeneratedScreenplayDefinition _flatFirst = null!;
    GeneratedScreenplayDefinition _flatSecond = null!;

    void Because()
    {
        var options = new CritterStackScreenplayOptions { Domain = "CommunityKitchen" };
        _first = new CritterStackScreenplayGenerator().Generate([CreateFeatureFolderProject()], options);
        _second = new CritterStackScreenplayGenerator().Generate([CreateFeatureFolderProject(reverse: true)], options);
        _flatFirst = new CritterStackScreenplayGenerator().Generate([CreateFlatProject()], options);
        _flatSecond = new CritterStackScreenplayGenerator().Generate([CreateFlatProject()], options);
    }

    [Fact] void should_print_byte_identical_source() => _second.Source.ShouldEqual(_first.Source);
    [Fact] void should_report_the_same_diagnostics() =>
        _second.Diagnostics.Select(_ => $"{_.Code}|{_.Message}").ShouldEqual(_first.Diagnostics.Select(_ => $"{_.Code}|{_.Message}"));
    [Fact] void should_print_byte_identical_source_for_a_flat_namespace() => _flatSecond.Source.ShouldEqual(_flatFirst.Source);
}
