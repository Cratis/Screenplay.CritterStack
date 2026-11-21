// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Cratis.CritterStack.Screenplay.Canonical;
using NSubstitute;

namespace Cratis.CritterStack.Screenplay.for_CanonicalGolden;

[Collection("Canonical golden environment")]
public class when_verifying_mismatched_goldens_with_actual_outputs : given.golden_fixtures
{
    readonly TextWriter _error = Substitute.For<TextWriter>();
    bool _matches;
    string _sourceAtMismatch = null!;
    string _diagnosticsAtMismatch = null!;
    string _goldenSource = null!;
    string _goldenDiagnostics = null!;

    async Task Establish()
    {
        await File.WriteAllTextAsync(Prefix + ".play.txt", "domain Original\n");
        await File.WriteAllTextAsync(Prefix + ".diagnostics.txt", "old diagnostics\n");
        _error.WriteLineAsync(Arg.Any<string>()).Returns(async _ =>
        {
            _sourceAtMismatch = await File.ReadAllTextAsync(Path.Combine(ActualDirectory, "Sample.play.txt"));
            _diagnosticsAtMismatch = await File.ReadAllTextAsync(Path.Combine(ActualDirectory, "Sample.diagnostics.txt"));
        });
    }

    async Task Because()
    {
        var previousActual = Environment.GetEnvironmentVariable(CanonicalGolden.ActualDirectoryVariable);
        var previousRegenerate = Environment.GetEnvironmentVariable(CanonicalGolden.RegenerateVariable);
        try
        {
            Environment.SetEnvironmentVariable(CanonicalGolden.ActualDirectoryVariable, ActualDirectory);
            Environment.SetEnvironmentVariable(CanonicalGolden.RegenerateVariable, null);
            _matches = await CanonicalGolden.Verify(Prefix, Result, DirectoryPath, _error);
        }
        finally
        {
            Environment.SetEnvironmentVariable(CanonicalGolden.ActualDirectoryVariable, previousActual);
            Environment.SetEnvironmentVariable(CanonicalGolden.RegenerateVariable, previousRegenerate);
        }

        _goldenSource = await File.ReadAllTextAsync(Prefix + ".play.txt");
        _goldenDiagnostics = await File.ReadAllTextAsync(Prefix + ".diagnostics.txt");
    }

    [Fact] void should_fail_verification() => _matches.ShouldBeFalse();
    [Fact] void should_write_source_before_reporting_a_mismatch() => _sourceAtMismatch.ShouldEqual("domain Kitchen\n");
    [Fact] void should_write_diagnostics_before_reporting_a_mismatch() => _diagnosticsAtMismatch.ShouldEqual("\n");
    [Fact] void should_not_rewrite_the_source_golden() => _goldenSource.ShouldEqual("domain Original\n");
    [Fact] void should_not_rewrite_the_diagnostic_golden() => _goldenDiagnostics.ShouldEqual("old diagnostics\n");
}
