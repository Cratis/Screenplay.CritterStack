// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Cratis.CritterStack.Screenplay.for_CritterStackScreenplayAdapter;

public class when_characterizing_projection_side_effect_variants : given.a_projection_side_effect_variants_application
{
    (string Expected, string Actual) _transcript;

    void Because() => _transcript = given.contribution_transcript.Compare(VariantContribution, "marten-projection-side-effect-variants");

    [Fact] void should_preserve_every_fact_and_diagnostic() => _transcript.Actual.ShouldEqual(_transcript.Expected);
}
