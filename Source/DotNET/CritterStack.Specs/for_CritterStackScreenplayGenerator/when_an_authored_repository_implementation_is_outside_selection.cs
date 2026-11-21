// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Cratis.CritterStack.Screenplay.for_CritterStackScreenplayGenerator;

public class when_an_authored_repository_implementation_is_outside_selection : given.an_authored_abstraction_application
{
    protected override bool IncludeImplementation => false;

    void Because() => Generate();

    [Fact] void should_not_treat_an_unselected_compilation_as_authored_evidence() => ShouldRejectWithoutPersistence();
}
