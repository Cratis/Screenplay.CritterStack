// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Reflection;

namespace Cratis.CritterStack.Screenplay.for_CritterStackSource;

public class when_formatting_canonical_source_policy_and_operational_diagnostics : Specification
{
    const string PhysicalRoot = "/physical/checkout";
    static readonly Type _canonicalRunner = Assembly.Load("Cratis.CritterStack.Screenplay.Canonical")
        .GetType("Cratis.CritterStack.Screenplay.Canonical.CanonicalRunner", throwOnError: true)!;
    string _policyLine = null!;
    string _operationalMessage = null!;

    void Because()
    {
        var syntaxTree = CSharpSyntaxTree.ParseText(
            "namespace Application; public sealed class Marker;",
            path: $"{PhysicalRoot}/Application/Marker.cs");
        var sourceContext = DotNetSourcePaths.Create(
            "Application/Application",
            new DotNetSourcePathPolicy
            {
                Version = 1,
                DisplayRoot = DotNetSourceDisplayRoot.Workspace,
                CasePolicy = DotNetSourcePathCasePolicy.Ordinal
            },
            [
                new DotNetSourceDocument
                {
                    SyntaxTree = syntaxTree,
                    ProjectRelativePath = "Marker.cs",
                    WorkspaceRelativePath = "Application/Marker.cs"
                }
            ]);

        _policyLine = (string)Invoke("SourceContextPolicyLine", sourceContext);
        _operationalMessage = (string)Invoke(
            "SanitizeOperationalMessage",
            $"Failed to load {PhysicalRoot}/Application/Application.csproj",
            PhysicalRoot);
    }

    [Fact] void should_not_include_the_physical_root_in_the_source_policy_line() =>
        _policyLine.ShouldNotContain(PhysicalRoot);

    [Fact] void should_report_only_logical_source_policy_values() =>
        _policyLine.ShouldContain("project=Application/Application");

    [Fact] void should_sanitize_the_workspace_root_in_operational_messages() =>
        _operationalMessage.ShouldEqual("Failed to load <workspace>/Application/Application.csproj");

    static object Invoke(string method, params object[] arguments) =>
        _canonicalRunner
            .GetMethod(method, BindingFlags.Static | BindingFlags.NonPublic)!
            .Invoke(null, arguments)!;
}
