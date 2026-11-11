// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Cratis.CritterStack.Screenplay.for_CritterStackScreenplayGenerator;

public class when_characterizing_wolverine_dcb_chain_competition : given.a_wolverine_dcb_competition_application
{
    (string Expected, string Actual) _transcript;

    void Because() => _transcript = given.generation_transcript.Compare(
        new CritterStackScreenplayGenerator().Generate([Project], new CritterStackScreenplayOptions { Domain = "Accounts" }),
        "wolverine-dcb-chain-competition");

    [Fact] void should_preserve_every_diagnostic_and_relationship() => _transcript.Actual.ShouldEqual(_transcript.Expected);
}
