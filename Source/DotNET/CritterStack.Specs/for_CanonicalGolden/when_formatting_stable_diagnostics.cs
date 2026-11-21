// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Cratis.CritterStack.Screenplay.Canonical;

namespace Cratis.CritterStack.Screenplay.for_CanonicalGolden;

public class when_formatting_stable_diagnostics : Specification
{
    readonly string _sourceRoot = Path.GetFullPath(Path.Combine("canonical", "checkout"));
    GenerationDiagnostic[] _diagnostics = [];
    string _formatted = null!;

    void Establish()
    {
        _diagnostics =
        [
            new()
            {
                Code = "Z002",
                Severity = GenerationDiagnosticSeverity.Warning,
                Outcome = GenerationDiagnosticOutcome.Unsupported,
                Message = $"Other {_sourceRoot.Replace('/', '\\')}/src/Last.cs",
                Source = new SourceRange { Path = "src/Last.cs", StartLine = 1, StartColumn = 1, EndLine = 1, EndColumn = 2 }
            },
            new()
            {
                Code = "A001",
                Severity = GenerationDiagnosticSeverity.Error,
                Outcome = GenerationDiagnosticOutcome.Conflict,
                Message = $"Failed at {_sourceRoot.Replace('\\', '/')}/src/First.cs  ",
                Source = new SourceRange { Path = "src/First.cs", StartLine = 2, StartColumn = 1, EndLine = 2, EndColumn = 2 }
            }
        ];
    }

    void Because() => _formatted = CanonicalGolden.StableDiagnostics(_diagnostics, _sourceRoot);

    [Fact] void should_sort_and_preserve_display_paths_and_replace_checkout_paths_in_messages() =>
        _formatted.ShouldEqual("A001 Error Conflict | src/First.cs | Failed at <workspace>/src/First.cs\nZ002 Warning Unsupported | src/Last.cs | Other <workspace>/src/Last.cs\n");
}
