// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Cratis.CritterStack.Screenplay.for_CritterStackScreenplayGenerator;

public class when_authored_repository_following_is_disabled : given.an_authored_abstraction_application
{
    protected override bool FollowAuthoredAbstractions => false;

    void Because() => Generate();

    [Fact] void should_not_discover_hidden_event_production() => Productions.ShouldBeEmpty();
    [Fact] void should_not_emit_the_opt_in_diagnostic() => Unresolved.ShouldBeEmpty();
    [Fact] void should_keep_the_default_output_byte_identical() => Transcript(new CritterStackScreenplayGenerator().Generate(Projects, new CritterStackScreenplayOptions { Domain = "Authored" })).ShouldEqual(Transcript(Result));
}
