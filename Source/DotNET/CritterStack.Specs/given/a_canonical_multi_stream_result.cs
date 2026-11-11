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
    readonly Dictionary<(ExpectationKind, string), bool> _met = [];
    GeneratedScreenplayDefinition _result = null!;

    void Establish() => _result = new GeneratedScreenplayDefinition
    {
        Application = null!,
        Source = string.Empty,
        Diagnostics = Contribution.Diagnostics,
        Graph = Graph
    };

    /// <summary>
    /// Evaluates expectations against the result, so assertions only read captured outcomes.
    /// </summary>
    /// <param name="cases">The expectation kinds and values to evaluate.</param>
    private protected void Evaluate(params (ExpectationKind Kind, string Value)[] cases)
    {
        foreach (var (kind, value) in cases)
        {
            _met[(kind, value)] = new Expectation(kind, value).IsMetBy(_result);
        }
    }

    /// <summary>
    /// Gets the captured outcome of one expectation evaluated by <see cref="Evaluate"/>.
    /// </summary>
    /// <param name="kind">The expectation kind.</param>
    /// <param name="value">The expectation value.</param>
    /// <returns><see langword="true"/> when the expectation was met.</returns>
    private protected bool Met(ExpectationKind kind, string value) => _met[(kind, value)];
}
