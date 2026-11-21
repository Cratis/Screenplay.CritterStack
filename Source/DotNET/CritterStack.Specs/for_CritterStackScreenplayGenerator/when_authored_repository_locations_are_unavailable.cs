// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Cratis.CritterStack.Screenplay.for_CritterStackScreenplayGenerator;

public class when_authored_repository_locations_are_unavailable : given.an_authored_abstraction_application
{
    void Because() => Result = GenerateFrom(Projects.Select(project => project with { SourceContext = null, SourceRoot = null }), true);

    [Fact] void should_not_infer_persistence_without_dual_source_evidence() => ShouldRejectWithoutPersistence();
}
