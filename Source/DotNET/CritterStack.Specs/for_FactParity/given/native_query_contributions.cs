// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using static Cratis.CritterStack.Screenplay.for_FactParity.given.fact_vectors;

namespace Cratis.CritterStack.Screenplay.for_FactParity.given;

sealed record native_query_contributions(AdapterContribution Compatibility, AdapterContribution Marten, AdapterContribution Wolverine)
{
    public static native_query_contributions Observe(DotNetProjectCompilation source)
    {
        var project = WithSourceContext(source);
        var context = new DotNetAnalysisContext([project]);
        var subjects = new CritterStackSubjectResolver(context);
        var (_, ownership) = MartenStoreScopeDiscovery.DiscoverOwnership(context, MartenAdapter, subjects);
        var compiled = MartenCompiledQueryDiscovery.EmitFacts(project, context, MartenAdapter, ownership);
        var documents = new MartenScreenplayAdapter().Analyze(context, new());
        return new(
            new CritterStackScreenplayAdapter().Analyze(context, new()),
            documents with { Facts = [.. documents.Facts, .. compiled.Facts], Diagnostics = [.. documents.Diagnostics, .. compiled.Diagnostics] },
            WolverineFactsFor(project, context));
    }

    static DotNetProjectCompilation WithSourceContext(DotNetProjectCompilation project) => project with
    {
        SourceContext = project.SourceContext ?? DotNetSourcePaths.Create(
            $"{project.Name}/{project.Name}",
            new() { Version = 1, DisplayRoot = DotNetSourceDisplayRoot.Workspace, CasePolicy = DotNetSourcePathCasePolicy.Ordinal },
            [.. project.AuthoredSyntaxTrees.Select(tree => new DotNetSourceDocument
            {
                SyntaxTree = tree,
                ProjectRelativePath = Path.GetFileName(tree.FilePath),
                WorkspaceRelativePath = Path.GetRelativePath(project.SourceRoot, tree.FilePath).Replace('\\', '/')
            })])
    };

    static AdapterContribution WolverineFactsFor(DotNetProjectCompilation project, DotNetAnalysisContext context)
    {
        var emitter = WolverineFacts.QueryArtifacts(project, context, new(), WolverineAdapter);
        var emitted = WolverineFacts.ObserveEntryPoints(project, new(true, true, []))
            .Where(entry => entry.Endpoint is { IsQuery: true })
            .Select(entry => WolverineFacts.ObserveQuery(project, entry.Endpoint!))
            .OfType<WolverineQueryObservation>()
            .Select(query => emitter.Emit(query, BackendOwnership.Legacy))
            .ToArray();
        List<GenerationDiagnostic> diagnostics = [.. emitted.SelectMany(result => result.Diagnostics)];
        var placement = CritterStackSourcePlacement.Derive(context, new(), emitted.SelectMany(result => result.Placements!), diagnostics);
        return new() { Adapter = WolverineAdapter, Facts = [.. emitted.SelectMany(result => result.Facts), .. placement], Diagnostics = diagnostics };
    }
}
