// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Cratis.CritterStack.Screenplay.for_CritterStackScreenplayGenerator;

public class when_an_authored_repository_body_is_generated : given.an_authored_abstraction_application
{
    protected override bool GeneratedImplementation => true;

    void Because() => Generate();

    [Fact] void should_not_follow_a_body_outside_the_authored_trees() => ShouldRejectWithoutPersistence();
}
