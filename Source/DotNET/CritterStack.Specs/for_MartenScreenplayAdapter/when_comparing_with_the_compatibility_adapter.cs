// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Text.Json;

namespace Cratis.CritterStack.Screenplay.for_MartenScreenplayAdapter;

public class when_comparing_with_the_compatibility_adapter : given.a_marten_adapter_context
{
    AdapterContribution _atomic = null!;
    AdapterContribution _compatibility = null!;

    void Because()
    {
        var options = new DotNetAdapterOptions { Module = "Students" };
        _atomic = Adapter.Analyze(Context, options);
        _compatibility = new CritterStackScreenplayAdapter().Analyze(Context, options);
    }

    [Fact] void should_keep_the_same_marten_fact_payloads() => NormalizedCompatibilityFacts().Select(Serialize).ShouldEqual(_atomic.Facts.Select(Serialize));
    [Fact] void should_keep_the_same_marten_diagnostics() => _compatibility.Diagnostics.ShouldEqual(_atomic.Diagnostics);

    GenerationFact[] NormalizedCompatibilityFacts() =>
    [
        .. _compatibility.Facts.Select(fact => fact with
        {
            Evidence = fact.Evidence with { Adapter = _atomic.Adapter }
        })
    ];

    static string Serialize(GenerationFact fact) => JsonSerializer.Serialize(fact, fact.GetType());
}
