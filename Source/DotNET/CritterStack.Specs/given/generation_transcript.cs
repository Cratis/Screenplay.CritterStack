// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;

namespace Cratis.CritterStack.Screenplay.given;

/// <summary>
/// Renders a generated screenplay as plain-text lines (diagnostics in emission order, then relationships in
/// ordinal order) so characterization specs can pin every diagnostic against a committed golden file.
/// Set CRATIS_REGENERATE_GOLDEN=1 to rewrite the golden files.
/// </summary>
public static class generation_transcript
{
    public static string Render(GeneratedScreenplayDefinition result) => string.Join(
        '\n',
        [
            .. result.Diagnostics.Select(_ => "diagnostic " + Describe(_)),
            .. result.Graph.Relationships
                .Select(_ => $"relationship {_.Key.Kind} {_.Key.Source.Value} -> {_.Key.Target.Value} discriminator={_.Key.Discriminator}")
                .Order(StringComparer.Ordinal)
        ]) + "\n";

    public static (string Expected, string Actual) Compare(GeneratedScreenplayDefinition result, string name, [CallerFilePath] string callerFilePath = "")
    {
        var actual = Render(result);
        if (Environment.GetEnvironmentVariable("CRATIS_REGENERATE_GOLDEN") == "1")
        {
            var sourceGolden = Path.Combine(Path.GetDirectoryName(Path.GetDirectoryName(callerFilePath)), "Golden", name + ".txt");
            Directory.CreateDirectory(Path.GetDirectoryName(sourceGolden));
            File.WriteAllText(sourceGolden, actual);
        }

        return (File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Golden", name + ".txt")), actual);
    }

    static string Describe(GenerationDiagnostic diagnostic) =>
        $"{diagnostic.Code} {diagnostic.Severity} {diagnostic.Outcome} | {Location(diagnostic.Source)} | subject={diagnostic.Subject?.Value} | {diagnostic.Message}";

    static string Location(SourceRange? source) => source is null
        ? "no-source"
        : $"{source.Path}:{source.StartLine}:{source.StartColumn}";
}
