// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Cratis.CritterStack.Screenplay.for_CritterStackScreenplayAdapter;

public class when_characterizing_marten_event_schema_configuration : given.a_marten_event_schema_configuration_application
{
    (string Expected, string Actual) _transcript;

    void Because() => _transcript = given.contribution_transcript.Compare(Contribution, "marten-event-schema-configuration");

    [Fact] void should_preserve_every_fact_and_diagnostic() => _transcript.Actual.ShouldEqual(_transcript.Expected);
}
