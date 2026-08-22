// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Vogen;

namespace Cratis.CritterStack.Screenplay.Canonical.VogenConcepts;

[ValueObject<Guid>]
public readonly partial record struct OrderKey
{
    static Guid NormalizeInput(Guid value) => value;

    static Validation Validate(Guid value) => Validation.Ok;
}

[ValueObject<Guid>]
public readonly partial record struct CorrelationId
{
    static Guid NormalizeInput(Guid value) => value;

    static Validation Validate(Guid value) => Validation.Ok;
}

[Instance("Unspecified", "?")]
[ValueObject<string>]
public readonly partial record struct CustomerCode
{
    const string InvalidMessage = "Customer codes cannot be blank";

    static string NormalizeInput(string value) => value;

    static Validation Validate(string value) =>
        string.IsNullOrWhiteSpace(value) ? Validation.Invalid(InvalidMessage) : Validation.Ok;
}

[ValueObject<string>]
public readonly partial record struct NormalizedCode
{
    static string NormalizeInput(string value) => value.Trim().ToUpperInvariant();

    static Validation Validate(string value) => Validation.Ok;
}

/// <summary>
/// Makes the canonical fixture fail compilation unless Vogen source generation supplies its public API.
/// </summary>
public static class VogenGeneratedSourceSentinel
{
    /// <summary>
    /// Creates and unwraps a generated value object.
    /// </summary>
    /// <param name="value">The primitive value.</param>
    /// <returns>The generated value object's primitive value.</returns>
    public static Guid RoundTrip(Guid value) => OrderKey.From(value).Value;
}
