// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Cratis.CritterStack.Screenplay.Canonical;

namespace Cratis.CritterStack.Screenplay.given;

/// <summary>
/// A multi-stream grouping graph and its diagnostics, wrapped as a generated definition so canonical expectations
/// are checked against literal values.
/// </summary>
public class a_canonical_multi_stream_result : a_marten_multi_stream_grouping_application
{
    GeneratedScreenplayDefinition _result = null!;

    void Establish() => _result = new GeneratedScreenplayDefinition
    {
        Application = null!,
        Source = string.Empty,
        Diagnostics = Contribution.Diagnostics,
        Graph = Graph
    };

    /// <summary>
    /// Checks one expectation against the result.
    /// </summary>
    /// <param name="kind">The expectation kind.</param>
    /// <param name="value">The expectation value.</param>
    /// <returns><see langword="true"/> when the expectation is met.</returns>
    private protected bool Met(ExpectationKind kind, string value) => new Expectation(kind, value).IsMetBy(_result);
}
