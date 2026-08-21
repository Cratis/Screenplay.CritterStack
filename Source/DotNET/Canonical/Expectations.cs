// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Cratis.Screenplay.Generation;

namespace Cratis.CritterStack.Screenplay.Canonical;

enum ExpectationKind
{
    Source,
    NotSource,
    Diagnostic,
    Artifact,
    Relationship
}

sealed record Expectation(ExpectationKind Kind, string Value)
{
    public bool IsMetBy(GeneratedScreenplayDefinition result) => Kind switch
    {
        ExpectationKind.Source => result.Source.Contains(Value, StringComparison.Ordinal),
        ExpectationKind.NotSource => !result.Source.Contains(Value, StringComparison.Ordinal),
        ExpectationKind.Diagnostic => result.Diagnostics.Any(_ => _.Code == Value),
        ExpectationKind.Artifact => result.Graph.Artifacts.Any(_ => $"{_.Key.Kind}:{_.Variants[0].Definition.Name}" == Value),
        ExpectationKind.Relationship => result.Graph.Relationships.Any(_ => _.Key.Kind.ToString() == Value),
        _ => false
    };

    public override string ToString() => $"{Kind}: {Value}";
}

static class Expectations
{
    public static IReadOnlyList<Expectation> Read(string path) =>
    [
        .. File.ReadAllLines(path)
            .Select(_ => _.Trim())
            .Where(_ => _.Length > 0 && !_.StartsWith('#'))
            .Select(Parse)
    ];

    static Expectation Parse(string value)
    {
        var separator = value.IndexOf(':');
        if (separator < 1)
        {
            throw new InvalidExpectation(value);
        }

        var kind = value[..separator] switch
        {
            "source" => ExpectationKind.Source,
            "not-source" => ExpectationKind.NotSource,
            "diagnostic" => ExpectationKind.Diagnostic,
            "artifact" => ExpectationKind.Artifact,
            "relationship" => ExpectationKind.Relationship,
            _ => throw new InvalidExpectation(value)
        };
        return new(kind, value[(separator + 1)..].Trim());
    }
}

/// <summary>
/// The exception that is thrown when a canonical source expectation cannot be parsed.
/// </summary>
/// <param name="value">The invalid expectation.</param>
sealed class InvalidExpectation(string value) : Exception($"Invalid canonical expectation '{value}'");
