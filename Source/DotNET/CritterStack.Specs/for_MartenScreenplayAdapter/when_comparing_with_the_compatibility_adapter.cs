// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Text.Json;
using Cratis.CritterStack.Screenplay.given;

namespace Cratis.CritterStack.Screenplay.for_MartenScreenplayAdapter;

/// <summary>Preserves complete artifact diagnostic parity after Cratis/Screenplay.Generation#136.</summary>
public class when_comparing_with_the_compatibility_adapter : given.a_marten_adapter_context
{
    AdapterContribution _marten = null!;
    AdapterContribution _compatibility = null!;
    GeneratedScreenplayDefinition _native = null!;
    GeneratedScreenplayDefinition _legacy = null!;

    void Because()
    {
        var adapterOptions = new DotNetAdapterOptions { Module = "Students" };
        _marten = Adapter.Analyze(Context, adapterOptions);
        _compatibility = new CritterStackScreenplayAdapter().Analyze(Context, adapterOptions);
        var generator = new ScreenplayDefinitionGenerator();
        var options = new ScreenplayGenerationOptions { Domain = "Students" };
        _native = generator.Generate([_marten], options);
        _legacy = generator.Generate([_compatibility], options);
    }

    [Fact] void should_keep_the_same_marten_artifacts() => Artifacts(_native).ShouldEqual(Artifacts(_legacy));
    [Fact] void should_keep_the_same_marten_relationships() => Relationships(_marten).ShouldEqual(Relationships(_compatibility));
    [Fact] void should_keep_the_same_generated_source() => _native.Source.ShouldEqual(_legacy.Source);
    [Fact] void should_keep_complete_diagnostic_parity() => diagnostic_parity.Meaning(_native.Diagnostics).ShouldEqual(diagnostic_parity.Meaning(_legacy.Diagnostics));
    [Fact] void should_locate_the_native_artifact_warning_at_the_store_occurrence() => DocumentOmission(_native).Source.ShouldEqual(Range(5, 83, 105));
    [Fact] void should_keep_the_compatibility_artifact_warning_at_the_store_occurrence() => DocumentOmission(_legacy).Source.ShouldEqual(Range(5, 83, 105));

    static GenerationDiagnostic DocumentOmission(GeneratedScreenplayDefinition result) =>
        result.Diagnostics.Single(IsDocumentOmission);

    static bool IsDocumentOmission(GenerationDiagnostic diagnostic) =>
        diagnostic.Code == "GEN0004" && diagnostic.Subject?.Value == "dotnet://Students/Students/Students.Student";

    static SourceRange Range(int line, int start, int end) => new()
    {
        Path = "Students/Student.cs",
        FileIdentity = new SourceFileIdentity { Project = "Students/Students", Path = "Student.cs" },
        StartLine = line,
        StartColumn = start,
        EndLine = line,
        EndColumn = end
    };

    static string[] Artifacts(GeneratedScreenplayDefinition result) =>
        [.. result.Graph.Artifacts.SelectMany(artifact => artifact.Variants).Select(variant => JsonSerializer.Serialize(variant.Definition)).Order(StringComparer.Ordinal)];

    static string[] Relationships(AdapterContribution contribution) =>
        [.. contribution.Facts.OfType<RelationshipFact>().Select(fact => JsonSerializer.Serialize(fact.Definition.Key)).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal)];
}
