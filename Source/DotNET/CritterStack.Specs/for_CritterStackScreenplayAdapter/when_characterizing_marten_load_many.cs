// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Cratis.CritterStack.Screenplay.for_CritterStackScreenplayAdapter;

public class when_characterizing_marten_load_many : given.a_marten_load_many_application
{
    const string ExpectedDiagnostics =
        """
        MARTEN0003|Information|Unsupported|dotnet://Docs/Docs/Docs.Async|Docs/Docs.cs:11:9-11:56|Marten document 'Async' is persisted, queried, or explicitly configured, but the current Screenplay language has no ordinary document-state declaration
        MARTEN0003|Information|Unsupported|dotnet://Docs/Docs/Docs.Sync|Docs/Docs.cs:10:9-10:50|Marten document 'Sync' is persisted, queried, or explicitly configured, but the current Screenplay language has no ordinary document-state declaration
        """;

    const string ExpectedFacts =
        """
        marten:handler:dotnet://Docs/Docs/Docs.Reads#method:M%3ADocs.Reads.Many%28Marten.IQuerySession%29|Exact|Marten document usage through LoadMany|Docs/Docs.cs:10:9-10:50
        marten:Reads:dotnet://Docs/Docs/Docs.Reads#method:M%3ADocs.Reads.Many%28Marten.IQuerySession%29:dotnet://Docs/Docs/Docs.Sync:235|Exact|Marten document usage through LoadMany|Docs/Docs.cs:10:9-10:50
        marten:handler:dotnet://Docs/Docs/Docs.Reads#method:M%3ADocs.Reads.Many%28Marten.IQuerySession%29|Exact|Marten document usage through LoadManyAsync|Docs/Docs.cs:11:9-11:56
        marten:Reads:dotnet://Docs/Docs/Docs.Reads#method:M%3ADocs.Reads.Many%28Marten.IQuerySession%29:dotnet://Docs/Docs/Docs.Async:286|Exact|Marten document usage through LoadManyAsync|Docs/Docs.cs:11:9-11:56
        marten:document:dotnet://Docs/Docs/Docs.Async|Exact|Marten document usage through LoadManyAsync|Docs/Docs.cs:11:9-11:56
        marten:document:dotnet://Docs/Docs/Docs.Sync|Exact|Marten document usage through LoadMany|Docs/Docs.cs:10:9-10:50
        """;

    [Fact] void should_preserve_full_diagnostic_source_ranges() => string.Join('\n', given.diagnostic_ranges.Of(Contribution)).ShouldEqual(ExpectedDiagnostics);
    [Fact] void should_preserve_every_fact_with_full_source_ranges() => string.Join('\n', given.diagnostic_ranges.FactsOf(Contribution)).ShouldEqual(ExpectedFacts);
}
