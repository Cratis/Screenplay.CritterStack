// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Cratis.CritterStack.Screenplay.for_CritterStackScreenplayAdapter;

public class when_the_enum_is_owned_by_another_analyzed_project : given.an_enum_application
{
    DotNetProjectCompilation _contracts = null!;
    AdapterContribution _forward = null!;
    AdapterContribution _reversed = null!;

    void Establish()
    {
        var enumeration = Project.Compilation.SyntaxTrees.Single(_ => _.FilePath.EndsWith("/Priority.cs", StringComparison.Ordinal));
        var compilation = CSharpCompilation.Create("Contracts", [enumeration], Project.Compilation.References, new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
        _contracts = new DotNetProjectCompilation
        {
            Name = "Contracts",
            Compilation = compilation,
            AuthoredSyntaxTrees = new HashSet<SyntaxTree> { enumeration },
            SourceContext = DotNetSourcePaths.Create(
                "Contracts/Contracts",
                new DotNetSourcePathPolicy { DisplayRoot = DotNetSourceDisplayRoot.Workspace, CasePolicy = DotNetSourcePathCasePolicy.Ordinal },
                [new DotNetSourceDocument { SyntaxTree = enumeration, ProjectRelativePath = "Priority.cs", WorkspaceRelativePath = "Contracts/Priority.cs" }])
        };
        Project = Project with
        {
            Compilation = Project.Compilation.RemoveSyntaxTrees(enumeration).AddReferences(compilation.ToMetadataReference()),
            AuthoredSyntaxTrees = Project.AuthoredSyntaxTrees.Where(_ => _ != enumeration).ToHashSet()
        };
        Project.Compilation.GetDiagnostics().Where(_ => _.Severity == DiagnosticSeverity.Error).ShouldBeEmpty();
    }

    void Because()
    {
        var adapter = new CritterStackScreenplayAdapter();
        _forward = adapter.Analyze(new([Project, _contracts]), new());
        _reversed = adapter.Analyze(new([_contracts, Project]), new());
    }

    [Fact] void should_resolve_the_concept_to_its_exact_owner() => Representation(_forward).Subject.ShouldEqual(_contracts.SubjectForType(_contracts.Compilation.GetTypeByMetadataName("Ordering.Priority")!));
    [Fact] void should_locate_the_owning_project_declaration() => Representation(_forward).Evidence.Source.FileIdentity.ShouldEqual(new SourceFileIdentity { Project = "Contracts/Contracts", Path = "Priority.cs" });
    [Fact] void should_keep_fact_identity_stable_across_project_order() => Representation(_reversed).Id.ShouldEqual(Representation(_forward).Id);
    [Fact] void should_keep_member_order_stable_across_project_order() => Representation(_reversed).Definition.EnumerationValues.SequenceEqual(Representation(_forward).Definition.EnumerationValues).ShouldBeTrue();
    [Fact] void should_bind_properties_to_the_owning_project_subject() => _forward.Facts.OfType<ArtifactFact>().SelectMany(_ => _.Definition.Properties).Where(_ => _.Name == "priority").All(_ => _.Type.Subject == Representation(_forward).Subject).ShouldBeTrue();

    static ConceptRepresentationFact Representation(AdapterContribution contribution) => contribution.Facts.OfType<ConceptRepresentationFact>().Single();
}
