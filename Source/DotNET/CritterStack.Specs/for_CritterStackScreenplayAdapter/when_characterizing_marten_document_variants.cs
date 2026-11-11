// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Cratis.CritterStack.Screenplay.for_CritterStackScreenplayAdapter;

public class when_characterizing_marten_document_variants : given.a_marten_document_variants_application
{
    (string Expected, string Actual) _transcript;
    (string Expected, string Actual) _martenOnlyTranscript;

    void Because()
    {
        _transcript = given.contribution_transcript.Compare(Contribution, "marten-document-variants");
        _martenOnlyTranscript = given.contribution_transcript.Compare(MartenOnlyContribution, "marten-document-variants-marten-only");
    }

    [Fact] void should_resolve_the_identity_property_of_every_document() => string.Join(';', Identifiers).ShouldEqual("Alpha:id;Beta:key;Chi:id;Delta:;Epsilon:code;Eta:name;Gamma:;Iota:;Kappa:id;Lambda:id;Mu:id;Nu:id;Omega:id;Omicron:id;Pi:id;Psi:id;Sigma:id;Tau:id;Theta:;Zeta:code");
    [Fact] void should_preserve_every_fact_and_diagnostic() => _transcript.Actual.ShouldEqual(_transcript.Expected);
    [Fact] void should_preserve_every_fact_and_diagnostic_for_the_marten_only_adapter() => _martenOnlyTranscript.Actual.ShouldEqual(_martenOnlyTranscript.Expected);
}
