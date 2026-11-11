// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;

namespace Cratis.CritterStack.Screenplay.given;

/// <summary>
/// Renders an adapter contribution as ordered plain-text lines so characterization specs can pin every fact and
/// diagnostic against a committed golden file. Set CRATIS_REGENERATE_GOLDEN=1 to rewrite the golden files.
/// </summary>
public static class contribution_transcript
{
    public static string Render(AdapterContribution contribution) => string.Join(
        '\n',
        [
            .. contribution.Facts.Select(_ => "fact " + Describe(_)),
            .. contribution.Diagnostics.Select(_ => "diagnostic " + Describe(_))
        ]) + "\n";

    public static (string Expected, string Actual) Compare(AdapterContribution contribution, string name, [CallerFilePath] string callerFilePath = "")
    {
        var actual = Render(contribution);
        if (Environment.GetEnvironmentVariable("CRATIS_REGENERATE_GOLDEN") == "1")
        {
            var sourceGolden = Path.Combine(Path.GetDirectoryName(Path.GetDirectoryName(callerFilePath)), "Golden", name + ".txt");
            Directory.CreateDirectory(Path.GetDirectoryName(sourceGolden));
            File.WriteAllText(sourceGolden, actual);
        }

        return (File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Golden", name + ".txt")), actual);
    }

    static string Describe(GenerationFact fact) =>
        $"{DefinitionOf(fact)} | id={fact.Id.Value} | subject={fact.Subject.Value} | {fact.Evidence.Strength} | {fact.Evidence.Explanation} | {Location(fact.Evidence.Source)}";

    static string Describe(GenerationDiagnostic diagnostic) =>
        $"{diagnostic.Code} {diagnostic.Severity} {diagnostic.Outcome} | {Location(diagnostic.Source)} | subject={diagnostic.Subject?.Value} | {diagnostic.Message}";

    static string DefinitionOf(GenerationFact fact) => fact switch
    {
        ArtifactFact artifact =>
            $"artifact {artifact.Definition.Key.Kind} {artifact.Definition.Name} file={artifact.Definition.File} properties=[{string.Join(',', artifact.Definition.Properties.Select(_ => _.Name))}]",
        RelationshipFact relationship =>
            $"relationship {relationship.Definition.Key.Kind} {relationship.Definition.Key.Source.Value} -> {relationship.Definition.Key.Target.Value} discriminator={relationship.Definition.Key.Discriminator}",
        _ => fact.GetType().Name
    };

    static string Location(SourceRange? source) => source is null
        ? "no-source"
        : $"{source.Path}:{source.StartLine}:{source.StartColumn}";
}
