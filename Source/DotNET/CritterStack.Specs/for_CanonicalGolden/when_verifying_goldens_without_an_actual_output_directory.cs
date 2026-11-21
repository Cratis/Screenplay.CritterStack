// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Cratis.CritterStack.Screenplay.Canonical;

namespace Cratis.CritterStack.Screenplay.for_CanonicalGolden;

[Collection("Canonical golden environment")]
public class when_verifying_goldens_without_an_actual_output_directory : given.golden_fixtures
{
    bool _matches;
    string[] _files = [];
    string[] _directories = [];
    string _goldenSource = null!;
    string _goldenDiagnostics = null!;

    async Task Establish()
    {
        await File.WriteAllTextAsync(Prefix + ".play.txt", "domain Kitchen\n");
        await File.WriteAllTextAsync(Prefix + ".diagnostics.txt", "\n");
    }

    async Task Because()
    {
        var previousActual = Environment.GetEnvironmentVariable(CanonicalGolden.ActualDirectoryVariable);
        var previousRegenerate = Environment.GetEnvironmentVariable(CanonicalGolden.RegenerateVariable);
        try
        {
            Environment.SetEnvironmentVariable(CanonicalGolden.ActualDirectoryVariable, null);
            Environment.SetEnvironmentVariable(CanonicalGolden.RegenerateVariable, null);
            _matches = await CanonicalGolden.Verify(Prefix, Result, DirectoryPath, Error);
        }
        finally
        {
            Environment.SetEnvironmentVariable(CanonicalGolden.ActualDirectoryVariable, previousActual);
            Environment.SetEnvironmentVariable(CanonicalGolden.RegenerateVariable, previousRegenerate);
        }

        _files = Directory.GetFiles(DirectoryPath);
        _directories = Directory.GetDirectories(DirectoryPath);
        _goldenSource = await File.ReadAllTextAsync(Prefix + ".play.txt");
        _goldenDiagnostics = await File.ReadAllTextAsync(Prefix + ".diagnostics.txt");
    }

    [Fact] void should_match() => _matches.ShouldBeTrue();
    [Fact] void should_not_create_other_files() => _files.Length.ShouldEqual(2);
    [Fact] void should_not_create_an_output_directory() => _directories.ShouldBeEmpty();
    [Fact] void should_not_rewrite_the_source_golden() => _goldenSource.ShouldEqual("domain Kitchen\n");
    [Fact] void should_not_rewrite_the_diagnostic_golden() => _goldenDiagnostics.ShouldEqual("\n");
}
