// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Cratis.CritterStack.Screenplay.for_CritterStackScreenplayGenerator;

public class when_using_compatibility_placement_with_exact_cross_project_subjects : given.a_cross_project_source_placement_application
{
    GeneratedScreenplayDefinition _compilationFacade = null!;
    GeneratedScreenplayDefinition _contextCompatibility = null!;
    SubjectId _domainCommand = null!;
    GeneratedScreenplayDefinition _legacyProjectFacade = null!;
    SubjectId _legacyProjectSubject = null!;
    GeneratedScreenplayDefinition _strictProjectFacade = null!;

    void Because()
    {
        var projects = CreateProjects();
        var options = new CritterStackScreenplayOptions
        {
            Domain = "Ordering",
            FeatureRoot = "Source",
            NamespaceSegmentsToSkip = 1
        };
        var generator = new CritterStackScreenplayGenerator();
        _domainCommand = projects.Domain.SubjectForType(projects.Domain.Compilation.GetTypeByMetadataName("Domain.Orders.SubmitOrder")!);
        _legacyProjectSubject = projects.Application.SubjectForType(projects.Application.Compilation.GetTypeByMetadataName("Domain.Orders.SubmitOrder")!);
        _strictProjectFacade = generator.Generate([projects.Application, projects.Domain], options);
        _contextCompatibility = generator.GenerateCompatibility([projects.Application, projects.Domain], options);

        var legacyProjects = CreateProjects(includeSourceContexts: false);
        _legacyProjectFacade = generator.Generate([legacyProjects.Application, legacyProjects.Domain], options);
        _compilationFacade = generator.Generate(legacyProjects.Application.Compilation, options);
    }

    [Fact] void should_keep_the_public_project_aware_path_strict() => Placement(_strictProjectFacade, _domainCommand).EffectiveVariants.Single().Evidence.Single().Explanation.ShouldContain("Host-owned source structure provides the strict Screenplay placement");
    [Fact] void should_keep_the_exact_cross_project_subject_with_internal_compatibility_placement() => Artifact(_contextCompatibility, ArtifactKind.Command, "SubmitOrder").Key.Subject.ShouldEqual(_domainCommand);
    [Fact] void should_use_legacy_placement_with_the_exact_cross_project_subject() => Placement(_contextCompatibility, _domainCommand).EffectiveVariants.Single().Placement.Module.ShouldEqual("Application");
    [Fact] void should_keep_the_all_no_context_public_project_path_legacy() => Artifact(_legacyProjectFacade, ArtifactKind.Command, "SubmitOrder").Key.Subject.ShouldEqual(_legacyProjectSubject);
    [Fact] void should_keep_the_compilation_facade_legacy() => Artifact(_compilationFacade, ArtifactKind.Command, "SubmitOrder").Key.Subject.ShouldEqual(_legacyProjectSubject);
    [Fact] void should_keep_public_legacy_placement_for_all_no_context_projects() => Placement(_legacyProjectFacade, _legacyProjectSubject).EffectiveVariants.Single().Placement.Module.ShouldEqual("Application");
    [Fact] void should_keep_public_legacy_placement_for_the_compilation_facade() => Placement(_compilationFacade, _legacyProjectSubject).EffectiveVariants.Single().Placement.Module.ShouldEqual("Application");

    static ResolvedArtifact Artifact(GeneratedScreenplayDefinition result, ArtifactKind kind, string name) =>
        result.Graph.Artifacts.Single(_ =>
            _.Key.Kind == kind &&
            _.Variants.Any(variant => variant.Definition.Name == name));

    static ResolvedArtifactPlacement Placement(GeneratedScreenplayDefinition result, SubjectId subject) =>
        result.Graph.Placements.Single(_ => _.Artifact == new ArtifactKey { Kind = ArtifactKind.Command, Subject = subject });
}
