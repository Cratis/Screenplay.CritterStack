// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Cratis.CritterStack.Screenplay.for_CritterStackScreenplayAdapter;

/// <summary>
/// Pins the full ordered diagnostics and saga facts of one isolated saga per behavior: correlation precedence
/// ([SagaIdentity], then [SagaIdentityFrom], then the saga-name conventions, then SagaId, then Id), role admission and
/// rejection reasons, creation and chain rules, role ordering across files and completion reporting. The expected
/// lines are the checked-in golden file wolverine-saga-variants.txt, captured from the pre-refactor code.
/// </summary>
public class when_characterizing_wolverine_saga_variants : given.a_wolverine_saga_variants_application
{
    string[] _expected = [];

    void Because()
    {
        Analyze();
        _expected = File.ReadAllLines(Path.Combine(AppContext.BaseDirectory, "Golden", "wolverine-saga-variants.txt"));
    }

    [Fact]
    void should_report_the_diagnostics_in_the_pinned_order() =>
        string.Join('\n', Lines.Where(_ => _.StartsWith("diagnostic ", StringComparison.Ordinal)))
            .ShouldEqual(string.Join('\n', _expected.Where(_ => _.StartsWith("diagnostic ", StringComparison.Ordinal))));

    [Fact]
    void should_report_the_saga_facts_in_the_pinned_order() =>
        string.Join('\n', Lines.Where(_ => !_.StartsWith("diagnostic ", StringComparison.Ordinal)))
            .ShouldEqual(string.Join('\n', _expected.Where(_ => !_.StartsWith("diagnostic ", StringComparison.Ordinal))));

    [Fact] void should_keep_the_golden_non_vacuous() => _expected.Length.ShouldEqual(Lines.Count);

    [Fact]
    void should_prefer_the_saga_identity_attribute_over_saga_identity_from() =>
        For(Lines, "AttrFromSaga").Any(_ => _.Contains("Exact Wolverine [SagaIdentity] message member", StringComparison.Ordinal) && _.EndsWith("|chosen", StringComparison.Ordinal)).ShouldBeTrue();

    [Fact]
    void should_not_fall_back_to_the_full_name_convention_when_saga_identity_from_names_a_missing_member() =>
        For(Lines, "FromMissingSaga").Any(_ => _.EndsWith("convention 'FromMissingId'|Saga/Correlation.cs:31:86-31:132|fromMissingId", StringComparison.Ordinal)).ShouldBeTrue();
}
