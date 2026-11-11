// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Cratis.CritterStack.Screenplay.Canonical;

namespace Cratis.CritterStack.Screenplay.for_Expectations;

public class when_checking_source_and_diagnostic_expectations : given.a_canonical_kitchen_result
{
    [Fact] void should_meet_a_source_expectation_that_the_source_contains() => Met(ExpectationKind.Source, "module Sales").ShouldBeTrue();
    [Fact] void should_not_meet_a_source_expectation_that_differs_only_by_case() => Met(ExpectationKind.Source, "module sales").ShouldBeFalse();
    [Fact] void should_not_meet_a_source_expectation_the_source_lacks() => Met(ExpectationKind.Source, "module Billing").ShouldBeFalse();
    [Fact] void should_meet_a_not_source_expectation_the_source_lacks() => Met(ExpectationKind.NotSource, "module Billing").ShouldBeTrue();
    [Fact] void should_not_meet_a_not_source_expectation_that_the_source_contains() => Met(ExpectationKind.NotSource, "module Sales").ShouldBeFalse();
    [Fact] void should_meet_the_upper_case_source_hash() => Met(ExpectationKind.SourceHash, SourceHash).ShouldBeTrue();
    [Fact] void should_meet_the_lower_case_source_hash() => Met(ExpectationKind.SourceHash, SourceHash.ToLowerInvariant()).ShouldBeTrue();
    [Fact] void should_not_meet_a_different_source_hash() => Met(ExpectationKind.SourceHash, SourceHash[..^1] + "D").ShouldBeFalse();
    [Fact] void should_not_meet_a_source_hash_prefix() => Met(ExpectationKind.SourceHash, SourceHash[..16]).ShouldBeFalse();
    [Fact] void should_meet_a_diagnostic_with_that_exact_code() => Met(ExpectationKind.Diagnostic, "MARTEN0003").ShouldBeTrue();
    [Fact] void should_not_meet_a_diagnostic_code_that_is_only_a_prefix() => Met(ExpectationKind.Diagnostic, "WOLVERINE000").ShouldBeFalse();
    [Fact] void should_not_meet_a_diagnostic_code_that_was_not_reported() => Met(ExpectationKind.Diagnostic, "MARTEN9999").ShouldBeFalse();
    [Fact] void should_meet_a_diagnostic_message_with_the_code_and_a_message_substring() => Met(ExpectationKind.DiagnosticMessage, "WOLVERINE0002|HTTP GET route '/batches/{id}'").ShouldBeTrue();
    [Fact] void should_meet_a_diagnostic_message_with_the_code_and_an_empty_substring() => Met(ExpectationKind.DiagnosticMessage, "WOLVERINE0002|").ShouldBeTrue();
    [Fact] void should_not_meet_a_diagnostic_message_that_belongs_to_another_code() => Met(ExpectationKind.DiagnosticMessage, "MARTEN0003|HTTP GET route").ShouldBeFalse();
    [Fact] void should_not_meet_a_diagnostic_message_the_code_did_not_report() => Met(ExpectationKind.DiagnosticMessage, "WOLVERINE0002|no such message").ShouldBeFalse();
    [Fact] void should_not_meet_a_diagnostic_message_with_only_a_code() => Met(ExpectationKind.DiagnosticMessage, "WOLVERINE0002").ShouldBeFalse();
    [Fact] void should_not_meet_an_expectation_kind_it_does_not_know() => new Expectation((ExpectationKind)99, "x").IsMetBy(Result).ShouldBeFalse();
}
