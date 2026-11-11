// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Cratis.CritterStack.Screenplay.for_CritterStackScreenplayAdapter;

public class when_characterizing_projection_side_effects : given.a_projection_side_effect_application
{
    (string Expected, string Actual) _configured;
    (string Expected, string Actual) _unconfigured;

    void Because()
    {
        _configured = given.contribution_transcript.Compare(Contribution, "marten-projection-side-effects");
        _unconfigured = given.contribution_transcript.Compare(UnconfiguredContribution, "marten-projection-side-effects-unconfigured");
    }

    [Fact] void should_preserve_every_configured_fact_and_diagnostic() => _configured.Actual.ShouldEqual(_configured.Expected);
    [Fact] void should_preserve_every_unconfigured_fact_and_diagnostic() => _unconfigured.Actual.ShouldEqual(_unconfigured.Expected);
}
