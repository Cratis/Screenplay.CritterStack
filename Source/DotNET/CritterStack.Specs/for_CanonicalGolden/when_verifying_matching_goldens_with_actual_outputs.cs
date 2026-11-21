// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Cratis.CritterStack.Screenplay.Canonical;

namespace Cratis.CritterStack.Screenplay.for_CanonicalGolden;

[Collection("Canonical golden environment")]
public class when_verifying_matching_goldens_with_actual_outputs : given.golden_fixtures
{
    bool _matches;
    string _actualSource = null!;
    string _actualDiagnostics = null!;
    string _goldenSource = null!;
    string _goldenDiagnostics = null!;

    async Task Establish()
    {
        await File.WriteAllTextAsync(Prefix + ".play.txt", "domain Kitchen \r\n");
        await File.WriteAllTextAsync(Prefix + ".diagnostics.txt", "\r\n");
    }

    async Task Because()
    {
        var previousActual = Environment.GetEnvironmentVariable(CanonicalGolden.ActualDirectoryVariable);
        var previousRegenerate = Environment.GetEnvironmentVariable(CanonicalGolden.RegenerateVariable);
        try
        {
            Environment.SetEnvironmentVariable(CanonicalGolden.ActualDirectoryVariable, ActualDirectory);
            Environment.SetEnvironmentVariable(CanonicalGolden.RegenerateVariable, null);
            _matches = await CanonicalGolden.Verify(Prefix, Result, DirectoryPath, Error);
        }
        finally
        {
            Environment.SetEnvironmentVariable(CanonicalGolden.ActualDirectoryVariable, previousActual);
            Environment.SetEnvironmentVariable(CanonicalGolden.RegenerateVariable, previousRegenerate);
        }

        _actualSource = await File.ReadAllTextAsync(Path.Combine(ActualDirectory, "Sample.play.txt"));
        _actualDiagnostics = await File.ReadAllTextAsync(Path.Combine(ActualDirectory, "Sample.diagnostics.txt"));
        _goldenSource = await File.ReadAllTextAsync(Prefix + ".play.txt");
        _goldenDiagnostics = await File.ReadAllTextAsync(Prefix + ".diagnostics.txt");
    }

    [Fact] void should_match() => _matches.ShouldBeTrue();
    [Fact] void should_write_normalized_source() => _actualSource.ShouldEqual("domain Kitchen\n");
    [Fact] void should_write_normalized_diagnostics() => _actualDiagnostics.ShouldEqual("\n");
    [Fact] void should_not_rewrite_the_source_golden() => _goldenSource.ShouldEqual("domain Kitchen \r\n");
    [Fact] void should_not_rewrite_the_diagnostic_golden() => _goldenDiagnostics.ShouldEqual("\r\n");
    [Fact] void should_not_report_a_mismatch() => Error.ToString().ShouldEqual(string.Empty);
}
