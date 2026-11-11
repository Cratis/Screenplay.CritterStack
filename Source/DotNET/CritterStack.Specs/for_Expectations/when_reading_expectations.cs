// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Cratis.CritterStack.Screenplay.Canonical;

namespace Cratis.CritterStack.Screenplay.for_Expectations;

public class when_reading_expectations : Specification
{
    string _path = null!;
    IReadOnlyList<string> _valid = null!;
    IReadOnlyList<string> _kinds = null!;
    Exception _unknownKind = null!;
    Exception _missingSeparator = null!;
    Exception _leadingSeparator = null!;
    Exception _firstOfSeveral = null!;

    void Establish() => _path = Path.Combine(Path.GetTempPath(), $"expectations-{Guid.NewGuid():N}.expected");

    void Because()
    {
        _valid = Read(
            "# a comment",
            string.Empty,
            "  source: module Sales  ",
            "not-source:module Billing",
            "diagnostic-message: WOLVERINE0002|route: '/batches'",
            "   ",
            "# source: ignored");
        _kinds = Read(
            "source: a",
            "not-source: b",
            "source-sha256: c",
            "diagnostic: d",
            "diagnostic-message: e",
            "artifact: f",
            "artifact-count: g",
            "evidence: h",
            "evidence-count: i",
            "relationship: j",
            "relationship-count: k",
            "identifier: l",
            "not-identifier: m",
            "grouping: n",
            "fan-out: o");
        _unknownKind = Catch.Exception(() => Read("bogus: x"));
        _missingSeparator = Catch.Exception(() => Read("source"));
        _leadingSeparator = Catch.Exception(() => Read(":x"));
        _firstOfSeveral = Catch.Exception(() => Read("source: ok", "wrong: first", "also-wrong: second"));
        File.Delete(_path);
    }

    [Fact]
    void should_read_the_expectations_in_file_order_skipping_comments_and_blank_lines() => _valid.ShouldEqual(
        "Source: module Sales",
        "NotSource: module Billing",
        "DiagnosticMessage: WOLVERINE0002|route: '/batches'");
    [Fact]
    void should_read_every_kind_in_file_order() => _kinds.ShouldEqual(
        "Source: a",
        "NotSource: b",
        "SourceHash: c",
        "Diagnostic: d",
        "DiagnosticMessage: e",
        "Artifact: f",
        "ArtifactCount: g",
        "Evidence: h",
        "EvidenceCount: i",
        "Relationship: j",
        "RelationshipCount: k",
        "Identifier: l",
        "NotIdentifier: m",
        "Grouping: n",
        "FanOut: o");
    [Fact] void should_reject_an_unknown_kind_with_the_invalid_expectation_exception() => _unknownKind.ShouldBeOfExactType<InvalidExpectation>();
    [Fact] void should_name_the_unknown_kind_line_in_the_message() => _unknownKind.Message.ShouldEqual("Invalid canonical expectation 'bogus: x'");
    [Fact] void should_reject_a_line_without_a_separator() => _missingSeparator.ShouldBeOfExactType<InvalidExpectation>();
    [Fact] void should_name_the_line_without_a_separator_in_the_message() => _missingSeparator.Message.ShouldEqual("Invalid canonical expectation 'source'");
    [Fact] void should_reject_a_line_that_starts_with_the_separator() => _leadingSeparator.ShouldBeOfExactType<InvalidExpectation>();
    [Fact] void should_name_the_line_that_starts_with_the_separator_in_the_message() => _leadingSeparator.Message.ShouldEqual("Invalid canonical expectation ':x'");
    [Fact] void should_report_the_first_invalid_line_when_several_are_invalid() => _firstOfSeveral.Message.ShouldEqual("Invalid canonical expectation 'wrong: first'");

    IReadOnlyList<string> Read(params string[] lines)
    {
        File.WriteAllLines(_path, lines);
        return [.. Expectations.Read(_path).Select(_ => _.ToString())];
    }
}
