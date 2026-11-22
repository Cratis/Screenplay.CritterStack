// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Globalization;

using Cratis.Screenplay.Generation;
using Cratis.Screenplay.Generation.DotNet;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Operations;

namespace Cratis.CritterStack.Screenplay.Marten;

sealed record MartenStoreRegistration(
    DotNetProjectCompilation Project,
    InvocationExpressionSyntax Invocation,
    SubjectId Subject,
    SubjectId? Marker,
    IReadOnlyList<ISymbol> Options,
    BackendOwnership Ownership,
    SourceRange? Source,
    string Explanation)
{
    /// <summary>
    /// Creates the provenance for this observation; the observation itself carries no adapter identity.
    /// </summary>
    /// <param name="adapter">The adapter taking responsibility for the emitted fact.</param>
    /// <returns>The configured evidence.</returns>
    public Evidence EvidenceFor(AdapterIdentity adapter) => new()
    {
        Adapter = adapter,
        Strength = EvidenceStrength.Configured,
        Source = Source,
        Explanation = Explanation
    };
}

sealed class MartenStoreRegistrations(DotNetAnalysisContext context, CritterStackSubjectResolver subjects)
{
    public static IReadOnlyList<MartenStoreRegistration> Observe(
        DotNetAnalysisContext context,
        CritterStackSubjectResolver subjects) => new MartenStoreRegistrations(context, subjects).Discover();

    public static SubjectId MarkerSubject(DotNetAnalysisContext context, CritterStackSubjectResolver subjects, DotNetProjectCompilation project, INamedTypeSymbol marker) =>
        context.Projects.All(candidate => candidate.SourceContext is null)
            ? context.SubjectForType(marker) ?? subjects.SubjectForType(project, marker)
            : subjects.SubjectForType(project, marker);

    internal static bool IsRegistration(IMethodSymbol method)
    {
        var candidate = method.ReducedFrom ?? method;
        return (candidate.Name == "AddMarten" || candidate.Name == "AddMartenStore") &&
               candidate.ContainingNamespace.ToDisplayString() == "Marten";
    }

    static (string Project, string Path)? SourceIdentity(DotNetProjectCompilation project, InvocationExpressionSyntax invocation)
    {
        if (project.SourceContext is { } source)
        {
            return source.Files.TryGetValue(invocation.SyntaxTree, out var file) ? (file.Identity.Project, file.Identity.Path) : null;
        }

        var path = CritterStackSource.RangeForProject(invocation.GetLocation(), project)?.Path;
        return path is not null && project.Compilation.SyntaxTrees.Count(tree =>
            CritterStackSource.RangeForProject(tree.GetRoot().GetLocation(), project)?.Path == path) == 1
            ? (project.Name, path) : null;
    }

    static SubjectId? RegistrationSubject(DotNetProjectCompilation project, InvocationExpressionSyntax invocation)
    {
        var identity = SourceIdentity(project, invocation);
        return identity is null ? null : new()
        {
            Value = $"dotnet:project/{Uri.EscapeDataString(identity.Value.Project)}/marten-store-registration/{Uri.EscapeDataString(identity.Value.Path)}@{invocation.SpanStart.ToString(CultureInfo.InvariantCulture)}:{invocation.Span.Length.ToString(CultureInfo.InvariantCulture)}"
        };
    }

    IReadOnlyList<MartenStoreRegistration> Discover() =>
    [
        .. context.Projects.SelectMany(project => project.AuthoredSyntaxTrees
            .Where(tree => !DotNetGeneratedSource.IsGenerated(tree))
            .SelectMany(tree => InTree(project, tree)))
            .OrderBy(registration => registration.Subject.Value, StringComparer.Ordinal)
    ];

    IEnumerable<MartenStoreRegistration> InTree(DotNetProjectCompilation project, SyntaxTree tree)
    {
        var model = project.Compilation.GetSemanticModel(tree);
        return tree.GetRoot().DescendantNodes().OfType<InvocationExpressionSyntax>()
            .Select(invocation => model.GetOperation(invocation)).OfType<IInvocationOperation>()
            .Where(operation => IsRegistration(operation.TargetMethod))
            .Select(operation => Registration(project, operation, model));
    }

    MartenStoreRegistration Registration(DotNetProjectCompilation project, IInvocationOperation operation, SemanticModel model)
    {
        var invocation = (InvocationExpressionSyntax)operation.Syntax;
        var subject = CanIdentify(project) ? RegistrationSubject(project, invocation) : null;
        var options = MartenStoreOptionsBinding.OptionsFor(operation, model);
        var qualifier = MartenStoreOptionsBinding.EventModelName(options, invocation, model);
        var ownership = subject is null || !qualifier.IsKnown
            ? BackendOwnership.Unknown
            : BackendOwnership.Exact(new() { Store = subject, EventModelName = qualifier.Value });
        return new(
            project,
            invocation,
            subject ?? new SubjectId { Value = DotNetMethodIdentity.SubjectFor(project, operation.TargetMethod).Value },
            Marker(project, operation.TargetMethod),
            options,
            ownership,
            CritterStackSource.RangeForProject(invocation.GetLocation(), project),
            $"Marten store registration through {operation.TargetMethod.Name}");
    }

    bool CanIdentify(DotNetProjectCompilation project) => project.SourceContext is not null || context.Projects.Count(candidate => candidate.Name == project.Name) == 1;

    SubjectId? Marker(DotNetProjectCompilation project, IMethodSymbol method) =>
        method.Name == "AddMartenStore" && method.TypeArguments.FirstOrDefault() is INamedTypeSymbol marker
            ? MarkerSubject(context, subjects, project, marker)
            : null;
}
