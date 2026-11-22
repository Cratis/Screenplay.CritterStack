// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Cratis.Screenplay.Generation;
using Cratis.Screenplay.Generation.DotNet;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Cratis.CritterStack.Screenplay.Marten;

static class MartenUnboundStoreConfiguration
{
    public static IReadOnlyList<GenerationDiagnostic> Discover(
        DotNetAnalysisContext context,
        IReadOnlyList<MartenStoreRegistration> registrations) =>
    [
        .. context.Projects.SelectMany(project => project.AuthoredSyntaxTrees
            .Where(tree => !DotNetGeneratedSource.IsGenerated(tree))
            .SelectMany(tree => InTree(project, tree, registrations)))
    ];

    static IEnumerable<GenerationDiagnostic> InTree(
        DotNetProjectCompilation project,
        SyntaxTree tree,
        IReadOnlyList<MartenStoreRegistration> registrations)
    {
        var model = project.Compilation.GetSemanticModel(tree);
        foreach (var assignment in tree.GetRoot().DescendantNodes().OfType<AssignmentExpressionSyntax>()
                     .Where(assignment => MartenStoreOptionsBinding.IsNameAssignment(assignment, model)))
        {
            if (registrations.Any(registration => registration.Project == project &&
                    registration.Invocation.SyntaxTree == assignment.SyntaxTree &&
                    MartenStoreOptionsBinding.IsAssignmentFor(assignment, registration.Invocation, registration.Options, model)))
            {
                continue;
            }

            var subject = model.GetEnclosingSymbol(assignment.SpanStart) is IMethodSymbol method
                ? DotNetMethodIdentity.SubjectFor(project, method)
                : new SubjectId { Value = project.Name };
            yield return BackendOwnership.Unknown.Diagnostic(
                subject,
                CritterStackSource.RangeForProject(assignment.GetLocation(), project),
                "Marten EventModelName assignment has no proven store registration owner");
        }
    }
}
