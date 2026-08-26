// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#if DEBUG
using Cratis.CritterStack.Screenplay.Canonical;

namespace Cratis.CritterStack.Screenplay.for_CanonicalRunner;

public class when_selecting_the_application_project_reference_closure : Specification
{
    static readonly string _workspaceRoot = Path.Combine(Path.GetPathRoot(Environment.CurrentDirectory)!, "canonical-workspace");
    static readonly string[] _expectedSelection = ["TripBuildingService", "TripDomain", "TripDomain.Foundation"];
    string[] _selection = null!;
    string[] _permutedSelection = null!;
    Exception _outsideWorkspaceError = null!;
    Exception _permutedOutsideWorkspaceError = null!;

    void Because()
    {
        _selection = SelectProjects(reverseInsertion: false, includeOutsideWorkspaceDependency: false);
        _permutedSelection = SelectProjects(reverseInsertion: true, includeOutsideWorkspaceDependency: false);
        _outsideWorkspaceError = Catch.Exception(() => SelectProjects(reverseInsertion: false, includeOutsideWorkspaceDependency: true));
        _permutedOutsideWorkspaceError = Catch.Exception(() => SelectProjects(reverseInsertion: true, includeOutsideWorkspaceDependency: true));
    }

    [Fact] void should_select_the_root_and_its_transitive_domain_dependencies_in_logical_path_order() => _selection.SequenceEqual(_expectedSelection).ShouldBeTrue();
    [Fact] void should_keep_the_order_stable_when_project_and_reference_insertion_order_changes() => _permutedSelection.SequenceEqual(_selection).ShouldBeTrue();
    [Fact] void should_not_select_an_unrelated_reverse_dependent() => _selection.ShouldNotContain("UnrelatedHost");
    [Fact] void should_not_select_spec_or_test_projects_even_when_they_are_dependencies() => _selection.Intersect(["TripBuildingService.Specs", "TripBuildingService.Tests"], StringComparer.Ordinal).ShouldBeEmpty();
    [Fact] void should_reject_a_transitive_project_outside_the_workspace() => _outsideWorkspaceError.ShouldBeOfExactType<InvalidDotNetSourcePath>();
    [Fact] void should_reject_a_transitive_project_outside_the_workspace_regardless_of_insertion_order() => _permutedOutsideWorkspaceError.ShouldBeOfExactType<InvalidDotNetSourcePath>();

    static string[] SelectProjects(bool reverseInsertion, bool includeOutsideWorkspaceDependency)
    {
        using var workspace = new AdhocWorkspace();
        var projects = new List<SyntheticProject>
        {
            new("TripBuildingService", "Applications/TripBuildingService/TripBuildingService.csproj"),
            new("TripDomain", "Domains/TripDomain/TripDomain.csproj"),
            new("TripDomain.Foundation", "Domains/TripDomain/Z.Foundation/TripDomain.Foundation.csproj"),
            new("UnrelatedHost", "Applications/UnrelatedHost/UnrelatedHost.csproj"),
            new("TripBuildingService.Specs", "Specs/TripBuildingService.Specs/TripBuildingService.Specs.csproj"),
            new("TripBuildingService.Tests", "Tests/TripBuildingService.Tests/TripBuildingService.Tests.csproj")
        };
        if (includeOutsideWorkspaceDependency)
        {
            projects.Add(new("TripDomain.Shared", "../canonical-shared/TripDomain.Shared/TripDomain.Shared.csproj"));
        }
        var projectIds = new Dictionary<string, ProjectId>(StringComparer.Ordinal);
        var solution = workspace.CurrentSolution;
        foreach (var project in reverseInsertion ? projects.AsEnumerable().Reverse() : projects)
        {
            var projectId = ProjectId.CreateNewId(project.Name);
            projectIds.Add(project.Name, projectId);
            solution = solution.AddProject(ProjectInfo.Create(
                projectId,
                VersionStamp.Create(),
                project.Name,
                project.Name,
                LanguageNames.CSharp,
                filePath: Path.Combine(_workspaceRoot, project.RelativePath)));
        }

        var references = new List<SyntheticReference>
        {
            new("TripBuildingService", "TripDomain"),
            new("TripDomain", "TripDomain.Foundation"),
            new("UnrelatedHost", "TripBuildingService"),
            new("TripBuildingService", "TripBuildingService.Specs"),
            new("TripDomain", "TripBuildingService.Tests")
        };
        if (includeOutsideWorkspaceDependency)
        {
            references.Add(new("TripDomain.Foundation", "TripDomain.Shared"));
        }
        foreach (var reference in reverseInsertion ? references.AsEnumerable().Reverse() : references)
        {
            solution = solution.AddProjectReference(
                projectIds[reference.Project],
                new ProjectReference(projectIds[reference.Dependency]));
        }

        var rootProject = solution.GetProject(projectIds["TripBuildingService"])!;
        return
        [
            .. CanonicalRunner
                .SelectApplicationProjectReferenceClosure(rootProject, _workspaceRoot)
                .Select(project => project.Name)
        ];
    }

    readonly record struct SyntheticProject(string Name, string RelativePath);
    readonly record struct SyntheticReference(string Project, string Dependency);
}
#endif
