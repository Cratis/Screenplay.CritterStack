// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Cratis.Screenplay.Generation;

namespace Cratis.CritterStack.Screenplay.Canonical;

/// <summary>
/// Compares a generated canonical model and its diagnostics with checked-in golden fixtures.
/// </summary>
/// <remarks>
/// A golden prefix names two fixtures: <c language="text">&lt;prefix&gt;.play.txt</c> holds the normalized model and
/// <c language="text">&lt;prefix&gt;.diagnostics.txt</c> holds the stable diagnostic lines. Set <c language="text">CRATIS_REGENERATE_GOLDEN=1</c> to rewrite them.
/// </remarks>
static class CanonicalGolden
{
    /// <summary>
    /// The environment variable that rewrites golden fixtures instead of comparing them.
    /// </summary>
    public const string RegenerateVariable = "CRATIS_REGENERATE_GOLDEN";

    /// <summary>
    /// The environment variable naming the directory for actual outputs without rewriting golden fixtures.
    /// </summary>
    public const string ActualDirectoryVariable = "CRATIS_CANONICAL_ACTUAL_DIR";

    /// <summary>
    /// Normalizes text so that line endings and trailing whitespace do not affect comparison.
    /// </summary>
    /// <param name="text">The text to normalize.</param>
    /// <returns>The text with LF line endings, no trailing whitespace and exactly one trailing newline.</returns>
    public static string Normalize(string text)
    {
        var lines = text.Replace("\r\n", "\n", StringComparison.Ordinal).Replace('\r', '\n').Split('\n').Select(_ => _.TrimEnd());
        return $"{string.Join('\n', lines).TrimEnd('\n')}\n";
    }

    /// <summary>
    /// Formats diagnostics as sorted, path-stable lines.
    /// </summary>
    /// <param name="diagnostics">The diagnostics to format.</param>
    /// <param name="sourceRoot">The checkout root, which is replaced by a placeholder wherever it appears.</param>
    /// <returns>The normalized diagnostic lines.</returns>
    public static string StableDiagnostics(IEnumerable<GenerationDiagnostic> diagnostics, string sourceRoot) =>
        Normalize(string.Join(
            '\n',
            diagnostics
                .Select(_ => $"{_.Code} {_.Severity} {_.Outcome} | {_.Source?.Path} | {CanonicalRunner.SanitizeOperationalMessage(_.Message, sourceRoot)}")
                .Order(StringComparer.Ordinal)));

    /// <summary>
    /// Describes the first difference between golden and generated text.
    /// </summary>
    /// <param name="expected">The normalized golden text.</param>
    /// <param name="actual">The normalized generated text.</param>
    /// <returns>A description of the first differing line, or null when the texts are equal.</returns>
    public static string? FirstDifference(string expected, string actual)
    {
        if (string.Equals(expected, actual, StringComparison.Ordinal))
        {
            return null;
        }

        var expectedLines = expected.Split('\n');
        var actualLines = actual.Split('\n');
        var line = Enumerable.Range(0, Math.Max(expectedLines.Length, actualLines.Length))
            .First(_ => !string.Equals(LineAt(expectedLines, _), LineAt(actualLines, _), StringComparison.Ordinal));
        return $"line {line + 1}: expected '{LineAt(expectedLines, line) ?? "<end of file>"}' but generated '{LineAt(actualLines, line) ?? "<end of file>"}'";
    }

    /// <summary>
    /// Compares the generated model and diagnostics with the golden fixtures, or rewrites the fixtures when regeneration is requested.
    /// </summary>
    /// <param name="goldenPrefix">The path prefix of the two fixtures.</param>
    /// <param name="result">The generation result.</param>
    /// <param name="sourceRoot">The checkout root of the generated application.</param>
    /// <param name="error">Receives one line per mismatch.</param>
    /// <returns>True when the generated output matches both fixtures or they were rewritten.</returns>
    public static async Task<bool> Verify(string goldenPrefix, GeneratedScreenplayDefinition result, string sourceRoot, TextWriter error)
    {
        var fixtures = new (string Path, string Actual)[]
        {
            ($"{goldenPrefix}.play.txt", Normalize(result.Source)),
            ($"{goldenPrefix}.diagnostics.txt", StableDiagnostics(result.Diagnostics, sourceRoot))
        };
        await WriteActualOutputs(fixtures);
        if (Environment.GetEnvironmentVariable(RegenerateVariable) == "1")
        {
            foreach (var (path, actual) in fixtures)
            {
                await File.WriteAllTextAsync(path, actual);
            }

            return true;
        }

        var matches = true;
        foreach (var (path, actual) in fixtures)
        {
            var expected = File.Exists(path) ? Normalize(await File.ReadAllTextAsync(path)) : string.Empty;
            if (FirstDifference(expected, actual) is { } difference)
            {
                await error.WriteLineAsync($"verification: golden-diff={Path.GetFileName(path)} {difference}");
                matches = false;
            }
        }

        return matches;
    }

    static async Task WriteActualOutputs(IEnumerable<(string Path, string Actual)> fixtures)
    {
        var directory = Environment.GetEnvironmentVariable(ActualDirectoryVariable);
        if (string.IsNullOrWhiteSpace(directory))
        {
            return;
        }

        Directory.CreateDirectory(directory);
        foreach (var (path, actual) in fixtures)
        {
            await File.WriteAllTextAsync(Path.Combine(directory, Path.GetFileName(path)), actual);
        }
    }

    static string? LineAt(string[] lines, int index) => index < lines.Length ? lines[index] : null;
}
