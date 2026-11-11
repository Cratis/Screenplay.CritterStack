// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Cratis.CritterStack.Screenplay.for_CritterStackScreenplayAdapter;

public class when_characterizing_marten_document_variants : given.a_marten_document_variants_application
{
    (string Expected, string Actual) _transcript;
    (string Expected, string Actual) _martenOnlyTranscript;

    const string ExpectedDiagnostics =
        """
        MARTEN0002|Warning|Unsupported|dotnet://Documents/Documents/Documents.EventProjectionHost|Documents/Usage.cs:53:1-65:2|Event projection 'EventProjectionHost' has no authored Create return or event-bound IDocumentOperations Store, Insert, Update, Delete, or DeleteWhere operation that can be represented exactly
        MARTEN0005|Warning|Unknown|dotnet://Documents/Documents/Documents.Delta|Documents/Configuration.cs:14:9-14:77|Marten identity configuration for 'Delta' is not a direct member expression and cannot be resolved safely
        MARTEN0005|Warning|Unknown|dotnet://Documents/Documents/Documents.Epsilon|Documents/Configuration.cs:15:9-15:67|Marten identity configuration for 'Epsilon' is not a direct member expression and cannot be resolved safely
        MARTEN0005|Warning|Unsupported|dotnet://Documents/Documents/Documents.Gamma|Documents/Usage.cs:12:64-12:102|Marten identity member 'Gamma.IdentityField' cannot be represented uniquely in the emitted 'Gamma' document property shape
        MARTEN0005|Warning|Unsupported|dotnet://Documents/Documents/Documents.Iota|Documents/Configuration.cs:19:9-19:57|Marten identity member 'Iota.Code' cannot be represented uniquely in the emitted 'Iota' document property shape
        MARTEN0003|Information|Unsupported|dotnet://Documents/Documents/Documents.Alpha|Documents/Usage.cs:7:103-7:123|Marten document 'Alpha' is persisted, queried, or explicitly configured, but the current Screenplay language has no ordinary document-state declaration
        MARTEN0003|Information|Unsupported|dotnet://Documents/Documents/Documents.Beta|Documents/Usage.cs:10:78-10:98|Marten document 'Beta' is persisted, queried, or explicitly configured, but the current Screenplay language has no ordinary document-state declaration
        MARTEN0003|Information|Unsupported|dotnet://Documents/Documents/Documents.Chi|Documents/Usage.cs:78:9-78:44|Marten document 'Chi' is persisted, queried, or explicitly configured, but the current Screenplay language has no ordinary document-state declaration
        MARTEN0003|Information|Unsupported|dotnet://Documents/Documents/Documents.Delta|Documents/Configuration.cs:14:9-14:77|Marten document 'Delta' is persisted, queried, or explicitly configured, but the current Screenplay language has no ordinary document-state declaration
        MARTEN0003|Information|Unsupported|dotnet://Documents/Documents/Documents.Epsilon|Documents/Configuration.cs:16:9-16:80|Marten document 'Epsilon' is persisted, queried, or explicitly configured, but the current Screenplay language has no ordinary document-state declaration
        MARTEN0003|Information|Unsupported|dotnet://Documents/Documents/Documents.Eta|Documents/Configuration.cs:18:9-18:56|Marten document 'Eta' is persisted, queried, or explicitly configured, but the current Screenplay language has no ordinary document-state declaration
        MARTEN0003|Information|Unsupported|dotnet://Documents/Documents/Documents.Gamma|Documents/Usage.cs:12:64-12:102|Marten document 'Gamma' is persisted, queried, or explicitly configured, but the current Screenplay language has no ordinary document-state declaration
        MARTEN0003|Information|Unsupported|dotnet://Documents/Documents/Documents.Iota|Documents/Configuration.cs:19:9-19:57|Marten document 'Iota' is persisted, queried, or explicitly configured, but the current Screenplay language has no ordinary document-state declaration
        MARTEN0003|Information|Unsupported|dotnet://Documents/Documents/Documents.Kappa|Documents/Usage.cs:19:80-19:139|Marten document 'Kappa' is persisted, queried, or explicitly configured, but the current Screenplay language has no ordinary document-state declaration
        MARTEN0003|Information|Unsupported|dotnet://Documents/Documents/Documents.Lambda|Documents/Usage.cs:20:89-20:111|Marten document 'Lambda' is persisted, queried, or explicitly configured, but the current Screenplay language has no ordinary document-state declaration
        MARTEN0003|Information|Unsupported|dotnet://Documents/Documents/Documents.Mu|Documents/Usage.cs:21:46-21:82|Marten document 'Mu' is persisted, queried, or explicitly configured, but the current Screenplay language has no ordinary document-state declaration
        MARTEN0003|Information|Unsupported|dotnet://Documents/Documents/Documents.Nu|Documents/Usage.cs:22:52-22:89|Marten document 'Nu' is persisted, queried, or explicitly configured, but the current Screenplay language has no ordinary document-state declaration
        MARTEN0003|Information|Unsupported|dotnet://Documents/Documents/Documents.Omega|Documents/Configuration.cs:20:9-20:36|Marten document 'Omega' is persisted, queried, or explicitly configured, but the current Screenplay language has no ordinary document-state declaration
        MARTEN0003|Information|Unsupported|dotnet://Documents/Documents/Documents.Omicron|Documents/Usage.cs:40:9-40:31|Marten document 'Omicron' is persisted, queried, or explicitly configured, but the current Screenplay language has no ordinary document-state declaration
        MARTEN0003|Information|Unsupported|dotnet://Documents/Documents/Documents.Pi|Documents/Usage.cs:50:40-50:75|Marten document 'Pi' is persisted, queried, or explicitly configured, but the current Screenplay language has no ordinary document-state declaration
        MARTEN0003|Information|Unsupported|dotnet://Documents/Documents/Documents.Psi|Documents/Configuration.cs:21:9-21:44|Marten document 'Psi' is persisted, queried, or explicitly configured, but the current Screenplay language has no ordinary document-state declaration
        MARTEN0003|Information|Unsupported|dotnet://Documents/Documents/Documents.Sigma|Documents/Usage.cs:61:9-61:50|Marten document 'Sigma' is persisted, queried, or explicitly configured, but the current Screenplay language has no ordinary document-state declaration
        MARTEN0003|Information|Unsupported|dotnet://Documents/Documents/Documents.Tau|Documents/Usage.cs:34:15-34:47|Marten document 'Tau' is persisted, queried, or explicitly configured, but the current Screenplay language has no ordinary document-state declaration
        MARTEN0003|Information|Unsupported|dotnet://Documents/Documents/Documents.Theta|Documents/Usage.cs:17:71-17:111|Marten document 'Theta' is persisted, queried, or explicitly configured, but the current Screenplay language has no ordinary document-state declaration
        MARTEN0003|Information|Unsupported|dotnet://Documents/Documents/Documents.Zeta|Documents/Configuration.cs:17:9-17:67|Marten document 'Zeta' is persisted, queried, or explicitly configured, but the current Screenplay language has no ordinary document-state declaration
        """;

    const string ExpectedMartenOnlyDiagnostics =
        """
        MARTEN0002|Warning|Unsupported|dotnet://Documents/Documents/Documents.EventProjectionHost|Documents/Usage.cs:53:1-65:2|Event projection 'EventProjectionHost' has no authored Create return or event-bound IDocumentOperations Store, Insert, Update, Delete, or DeleteWhere operation that can be represented exactly
        MARTEN0005|Warning|Unknown|dotnet://Documents/Documents/Documents.Delta|Documents/Configuration.cs:14:9-14:77|Marten identity configuration for 'Delta' is not a direct member expression and cannot be resolved safely
        MARTEN0005|Warning|Unknown|dotnet://Documents/Documents/Documents.Epsilon|Documents/Configuration.cs:15:9-15:67|Marten identity configuration for 'Epsilon' is not a direct member expression and cannot be resolved safely
        MARTEN0005|Warning|Unsupported|dotnet://Documents/Documents/Documents.Gamma|Documents/Usage.cs:12:64-12:102|Marten identity member 'Gamma.IdentityField' cannot be represented uniquely in the emitted 'Gamma' document property shape
        MARTEN0005|Warning|Unsupported|dotnet://Documents/Documents/Documents.Iota|Documents/Configuration.cs:19:9-19:57|Marten identity member 'Iota.Code' cannot be represented uniquely in the emitted 'Iota' document property shape
        MARTEN0003|Information|Unsupported|dotnet://Documents/Documents/Documents.Alpha|Documents/Usage.cs:7:103-7:123|Marten document 'Alpha' is persisted, queried, or explicitly configured, but the current Screenplay language has no ordinary document-state declaration
        MARTEN0003|Information|Unsupported|dotnet://Documents/Documents/Documents.Beta|Documents/Usage.cs:10:78-10:98|Marten document 'Beta' is persisted, queried, or explicitly configured, but the current Screenplay language has no ordinary document-state declaration
        MARTEN0003|Information|Unsupported|dotnet://Documents/Documents/Documents.Chi|Documents/Usage.cs:78:9-78:44|Marten document 'Chi' is persisted, queried, or explicitly configured, but the current Screenplay language has no ordinary document-state declaration
        MARTEN0003|Information|Unsupported|dotnet://Documents/Documents/Documents.Delta|Documents/Configuration.cs:14:9-14:77|Marten document 'Delta' is persisted, queried, or explicitly configured, but the current Screenplay language has no ordinary document-state declaration
        MARTEN0003|Information|Unsupported|dotnet://Documents/Documents/Documents.Epsilon|Documents/Configuration.cs:16:9-16:80|Marten document 'Epsilon' is persisted, queried, or explicitly configured, but the current Screenplay language has no ordinary document-state declaration
        MARTEN0003|Information|Unsupported|dotnet://Documents/Documents/Documents.Eta|Documents/Configuration.cs:18:9-18:56|Marten document 'Eta' is persisted, queried, or explicitly configured, but the current Screenplay language has no ordinary document-state declaration
        MARTEN0003|Information|Unsupported|dotnet://Documents/Documents/Documents.Gamma|Documents/Usage.cs:12:64-12:102|Marten document 'Gamma' is persisted, queried, or explicitly configured, but the current Screenplay language has no ordinary document-state declaration
        MARTEN0003|Information|Unsupported|dotnet://Documents/Documents/Documents.Iota|Documents/Configuration.cs:19:9-19:57|Marten document 'Iota' is persisted, queried, or explicitly configured, but the current Screenplay language has no ordinary document-state declaration
        MARTEN0003|Information|Unsupported|dotnet://Documents/Documents/Documents.Kappa|Documents/Usage.cs:19:80-19:139|Marten document 'Kappa' is persisted, queried, or explicitly configured, but the current Screenplay language has no ordinary document-state declaration
        MARTEN0003|Information|Unsupported|dotnet://Documents/Documents/Documents.Lambda|Documents/Usage.cs:20:89-20:111|Marten document 'Lambda' is persisted, queried, or explicitly configured, but the current Screenplay language has no ordinary document-state declaration
        MARTEN0003|Information|Unsupported|dotnet://Documents/Documents/Documents.Omega|Documents/Configuration.cs:20:9-20:36|Marten document 'Omega' is persisted, queried, or explicitly configured, but the current Screenplay language has no ordinary document-state declaration
        MARTEN0003|Information|Unsupported|dotnet://Documents/Documents/Documents.Omicron|Documents/Usage.cs:40:9-40:31|Marten document 'Omicron' is persisted, queried, or explicitly configured, but the current Screenplay language has no ordinary document-state declaration
        MARTEN0003|Information|Unsupported|dotnet://Documents/Documents/Documents.Pi|Documents/Usage.cs:50:40-50:75|Marten document 'Pi' is persisted, queried, or explicitly configured, but the current Screenplay language has no ordinary document-state declaration
        MARTEN0003|Information|Unsupported|dotnet://Documents/Documents/Documents.Psi|Documents/Configuration.cs:21:9-21:44|Marten document 'Psi' is persisted, queried, or explicitly configured, but the current Screenplay language has no ordinary document-state declaration
        MARTEN0003|Information|Unsupported|dotnet://Documents/Documents/Documents.Sigma|Documents/Usage.cs:61:9-61:50|Marten document 'Sigma' is persisted, queried, or explicitly configured, but the current Screenplay language has no ordinary document-state declaration
        MARTEN0003|Information|Unsupported|dotnet://Documents/Documents/Documents.Tau|Documents/Usage.cs:34:15-34:47|Marten document 'Tau' is persisted, queried, or explicitly configured, but the current Screenplay language has no ordinary document-state declaration
        MARTEN0003|Information|Unsupported|dotnet://Documents/Documents/Documents.Theta|Documents/Usage.cs:17:71-17:111|Marten document 'Theta' is persisted, queried, or explicitly configured, but the current Screenplay language has no ordinary document-state declaration
        MARTEN0003|Information|Unsupported|dotnet://Documents/Documents/Documents.Zeta|Documents/Configuration.cs:17:9-17:67|Marten document 'Zeta' is persisted, queried, or explicitly configured, but the current Screenplay language has no ordinary document-state declaration
        DOTNETSP0009|Error|Unsupported||no-source|Project 'Documents' has no host-supplied source context
        """;

    void Because()
    {
        _transcript = given.contribution_transcript.Compare(Contribution, "marten-document-variants");
        _martenOnlyTranscript = given.contribution_transcript.Compare(MartenOnlyContribution, "marten-document-variants-marten-only");
    }

    [Fact] void should_resolve_the_identity_property_of_every_document() => string.Join(';', Identifiers).ShouldEqual("Alpha:id;Beta:key;Chi:id;Delta:;Epsilon:code;Eta:name;Gamma:;Iota:;Kappa:id;Lambda:id;Mu:id;Nu:id;Omega:id;Omicron:id;Pi:id;Psi:id;Sigma:id;Tau:id;Theta:;Zeta:code");
    [Fact] void should_preserve_every_fact_and_diagnostic() => _transcript.Actual.ShouldEqual(_transcript.Expected);
    [Fact] void should_preserve_every_fact_and_diagnostic_for_the_marten_only_adapter() => _martenOnlyTranscript.Actual.ShouldEqual(_martenOnlyTranscript.Expected);
    [Fact] void should_preserve_full_diagnostic_source_ranges() => string.Join('\n', given.diagnostic_ranges.Of(Contribution)).ShouldEqual(ExpectedDiagnostics);
    [Fact] void should_preserve_full_diagnostic_source_ranges_for_the_marten_only_adapter() => string.Join('\n', given.diagnostic_ranges.Of(MartenOnlyContribution)).ShouldEqual(ExpectedMartenOnlyDiagnostics);
}
