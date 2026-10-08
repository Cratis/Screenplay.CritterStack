// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Cratis.CritterStack.Screenplay.for_CritterStackScreenplayAdapter;

public class when_an_enum_has_flags : given.an_enum_application
{
    const string FlagsDeclaration = "[System.Flags] public enum Priority { None = 0, Normal = 1, Urgent = 2 }";
    AdapterContribution _contribution = null!;

    void Establish() => Project = CreateProject(declaration: FlagsDeclaration);

    void Because() => _contribution = new CritterStackScreenplayAdapter().Analyze(new([Project]), new());

    [Fact] void should_not_emit_a_representation() => _contribution.Facts.OfType<ConceptRepresentationFact>().ShouldBeEmpty();
    [Fact] void should_not_nominate_a_concept() => _contribution.Facts.OfType<ArtifactFact>().Any(_ => _.Definition.Key.Kind == ArtifactKind.Concept).ShouldBeFalse();
    [Fact] void should_report_flags_once() => _contribution.Diagnostics.Count(_ => _.Code == "CRITTERSTACK0001").ShouldEqual(1);
    [Fact] void should_report_an_unsupported_warning() => Diagnostic.Severity.ShouldEqual(GenerationDiagnosticSeverity.Warning);
    [Fact] void should_explain_the_unsupported_combinations() => Diagnostic.Outcome.ShouldEqual(GenerationDiagnosticOutcome.Unsupported);
    [Fact] void should_locate_the_full_authored_declaration() => Diagnostic.Source.ShouldEqual(new SourceRange { Path = "Ordering/Priority.cs", FileIdentity = new SourceFileIdentity { Project = "Ordering/Ordering", Path = "Priority.cs" }, StartLine = 2, StartColumn = 1, EndLine = 2, EndColumn = FlagsDeclaration.Length + 1 });
    [Fact] void should_retain_the_field_type_without_a_concept_binding() => _contribution.Facts.OfType<ArtifactFact>().SelectMany(_ => _.Definition.Properties).Where(_ => _.Name == "priority").All(_ => _.Type.Name == "Priority" && _.Type.Subject is null).ShouldBeTrue();

    GenerationDiagnostic Diagnostic => _contribution.Diagnostics.Single(_ => _.Code == "CRITTERSTACK0001");
}
