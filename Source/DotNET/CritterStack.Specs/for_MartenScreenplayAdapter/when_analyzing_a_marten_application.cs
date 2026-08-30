// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Cratis.CritterStack.Screenplay.for_MartenScreenplayAdapter;

public class when_analyzing_a_marten_application : given.a_marten_adapter_context
{
    AdapterContribution _contribution = null!;

    void Because() => _contribution = Adapter.Analyze(Context, new DotNetAdapterOptions { Module = "Students" });

    [Fact] void should_use_the_atomic_producer() => _contribution.Adapter.ShouldEqual(new AdapterIdentity { Id = "marten", Version = "1.0.0" });
    [Fact] void should_scope_every_fact_id_to_the_atomic_producer() => _contribution.Facts.All(fact => fact.Id.Value.StartsWith("marten:", StringComparison.Ordinal)).ShouldBeTrue();
    [Fact] void should_retain_atomic_evidence_on_every_fact() => _contribution.Facts.All(fact => fact.Evidence.Adapter == _contribution.Adapter).ShouldBeTrue();
    [Fact] void should_retain_stable_source_identity_on_located_evidence() => _contribution.Facts.Where(fact => fact.Evidence.Source is not null).All(fact => fact.Evidence.Source!.FileIdentity is not null).ShouldBeTrue();
    [Fact] void should_emit_unique_fact_ids() => _contribution.Facts.Select(fact => fact.Id.Value).Distinct(StringComparer.Ordinal).Count().ShouldEqual(_contribution.Facts.Count);
    [Fact] void should_emit_no_wolverine_diagnostics() => _contribution.Diagnostics.Any(diagnostic => diagnostic.Code.StartsWith("WOLVERINE", StringComparison.Ordinal)).ShouldBeFalse();
}
