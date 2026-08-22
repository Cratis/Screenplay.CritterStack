// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Cratis.Screenplay.Generation;

namespace Cratis.CritterStack.Screenplay.Canonical;

enum ExpectationKind
{
    Source,
    NotSource,
    Diagnostic,
    DiagnosticMessage,
    Artifact,
    Relationship,
    Identifier,
    Grouping,
    FanOut
}

sealed record Expectation(ExpectationKind Kind, string Value)
{
    public bool IsMetBy(GeneratedScreenplayDefinition result) => Kind switch
    {
        ExpectationKind.Source => result.Source.Contains(Value, StringComparison.Ordinal),
        ExpectationKind.NotSource => !result.Source.Contains(Value, StringComparison.Ordinal),
        ExpectationKind.Diagnostic => result.Diagnostics.Any(_ => _.Code == Value),
        ExpectationKind.DiagnosticMessage => IsDiagnosticMessageIn(result, Value),
        ExpectationKind.Artifact => result.Graph.Artifacts.Any(_ => $"{_.Key.Kind}:{_.Variants[0].Definition.Name}" == Value),
        ExpectationKind.Relationship => IsRelationshipIn(result, Value),
        ExpectationKind.Identifier => IsIdentifierIn(result, Value),
        ExpectationKind.Grouping => IsGroupingIn(result, Value),
        ExpectationKind.FanOut => IsFanOutIn(result, Value),
        _ => false
    };

    public override string ToString() => $"{Kind}: {Value}";

    static bool IsDiagnosticMessageIn(GeneratedScreenplayDefinition result, string value)
    {
        var parts = value.Split('|', 2);
        return parts.Length == 2 && result.Diagnostics.Any(_ =>
            _.Code == parts[0] && _.Message.Contains(parts[1], StringComparison.Ordinal));
    }

    static bool IsRelationshipIn(GeneratedScreenplayDefinition result, string value)
    {
        var firstSpace = value.IndexOf(' ');
        if (firstSpace < 0)
        {
            return result.Graph.Relationships.Any(_ => _.Key.Kind.ToString() == value);
        }

        var arrow = value.IndexOf(" -> ", firstSpace, StringComparison.Ordinal);
        if (arrow < 0 || !Enum.TryParse<RelationshipKind>(value[..firstSpace], out var kind))
        {
            return false;
        }

        var source = value[(firstSpace + 1)..arrow];
        var target = value[(arrow + 4)..];
        return result.Graph.Relationships.Any(relationship =>
            relationship.Key.Kind == kind &&
            ArtifactMatches(result, relationship.Key.Source, source) &&
            ArtifactMatches(result, relationship.Key.Target, target));
    }

    static bool ArtifactMatches(GeneratedScreenplayDefinition result, SubjectId subject, string value)
    {
        var parts = value.Split(':', 2);
        return parts.Length == 2 &&
               Enum.TryParse<ArtifactKind>(parts[0], out var kind) &&
               result.Graph.Artifacts.Any(artifact =>
                   artifact.Key.Subject == subject &&
                   artifact.Key.Kind == kind &&
                   artifact.Variants.Any(variant => variant.Definition.Name == parts[1]));
    }

    static bool IsIdentifierIn(GeneratedScreenplayDefinition result, string value)
    {
        var parts = value.Split(':');
        return parts.Length == 3 &&
               Enum.TryParse<ArtifactKind>(parts[0], out var kind) &&
               result.Graph.Artifacts.Any(artifact =>
                   artifact.Key.Kind == kind &&
                   artifact.Variants.Any(variant =>
                       variant.Definition.Name == parts[1] &&
                       variant.Definition.Properties.Any(property => property.Name == parts[2] && property.IsIdentifier)));
    }

    static bool IsGroupingIn(GeneratedScreenplayDefinition result, string value)
    {
        var parts = value.Split('|');
        if (parts.Length != 4 || !bool.TryParse(parts[3], out var isOneToMany))
        {
            return false;
        }

        return result.Graph.Relationships.Any(relationship =>
            relationship.Key.Kind == RelationshipKind.Consumes &&
            ArtifactName(result, relationship.Key.Source, ArtifactKind.Reducer) == parts[0] &&
            ArtifactName(result, relationship.Key.Target, ArtifactKind.Event) == parts[1] &&
            relationship.Definitions.Any(definition =>
                definition.Key.Discriminator?.StartsWith("marten:identit", StringComparison.Ordinal) == true &&
                definition.TargetMember == parts[2] &&
                definition.IsCollection == isOneToMany));
    }

    static bool IsFanOutIn(GeneratedScreenplayDefinition result, string value)
    {
        var parts = value.Split('|');
        if (parts.Length != 4)
        {
            return false;
        }

        var parent = result.Graph.Artifacts
            .FirstOrDefault(_ =>
                _.Key.Kind == ArtifactKind.Event &&
                _.Variants.Any(variant => variant.Definition.Name == parts[0]))?.Key.Subject.Value;
        return parent is not null && result.Graph.Relationships.Any(relationship =>
            relationship.Key.Kind == RelationshipKind.Consumes &&
            ArtifactName(result, relationship.Key.Source, ArtifactKind.Reducer) is not null &&
            ArtifactName(result, relationship.Key.Target, ArtifactKind.Event) == parts[1] &&
            relationship.Definitions.Any(definition =>
                definition.Key.Discriminator?.StartsWith($"marten:fan-out-child:{parent}:{parts[3]}:", StringComparison.Ordinal) == true &&
                definition.SourceMember == parts[2] &&
                definition.IsCollection));
    }

    static string? ArtifactName(
        GeneratedScreenplayDefinition result,
        SubjectId subject,
        ArtifactKind kind) => result.Graph.Artifacts
        .Where(_ => _.Key.Subject == subject && _.Key.Kind == kind)
        .SelectMany(_ => _.Variants)
        .Select(_ => _.Definition.Name)
        .FirstOrDefault();
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
            "diagnostic-message" => ExpectationKind.DiagnosticMessage,
            "artifact" => ExpectationKind.Artifact,
            "relationship" => ExpectationKind.Relationship,
            "identifier" => ExpectationKind.Identifier,
            "grouping" => ExpectationKind.Grouping,
            "fan-out" => ExpectationKind.FanOut,
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
