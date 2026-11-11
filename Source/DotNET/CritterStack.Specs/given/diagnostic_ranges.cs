// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Cratis.CritterStack.Screenplay.given;

/// <summary>
/// Renders the full ordered diagnostics of a contribution, including the end line and column of every source range,
/// so characterization specs can pin them as literal tuples (the golden transcripts only hold the start position).
/// </summary>
public static class diagnostic_ranges
{
    public static string[] Of(AdapterContribution contribution) =>
        [.. contribution.Diagnostics.Select(_ => $"{_.Code}|{_.Severity}|{_.Outcome}|{_.Subject?.Value}|{Range(_.Source)}|{_.Message}")];

    public static string[] FactsOf(AdapterContribution contribution) =>
        [.. contribution.Facts.Select(_ => $"{_.Id.Value}|{_.Evidence.Strength}|{_.Evidence.Explanation}|{Range(_.Evidence.Source)}")];

    static string Range(SourceRange? source) => source is null
        ? "no-source"
        : $"{source.Path}:{source.StartLine}:{source.StartColumn}-{source.EndLine}:{source.EndColumn}";
}
