// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Cratis.Screenplay.Generation;
using Cratis.Screenplay.Generation.DotNet;
using Cratis.Screenplay.Generation.DotNet.Vogen;
using Microsoft.CodeAnalysis;

namespace Cratis.CritterStack.Screenplay;

/// <summary>
/// Defines a generator that creates a verified Screenplay definition from composed .NET source semantics.
/// </summary>
public interface ICritterStackScreenplayGenerator
{
    /// <summary>
    /// Generates from one Roslyn compilation.
    /// </summary>
    /// <param name="compilation">The application compilation.</param>
    /// <param name="options">Generation options.</param>
    /// <returns>The generated Screenplay definition.</returns>
    GeneratedScreenplayDefinition Generate(Compilation compilation, CritterStackScreenplayOptions options);

    /// <summary>
    /// Generates from project-aware Roslyn compilations that form one application.
    /// </summary>
    /// <param name="projects">The project compilations to analyze together.</param>
    /// <param name="options">Generation options.</param>
    /// <returns>The generated Screenplay definition.</returns>
    GeneratedScreenplayDefinition Generate(
        IReadOnlyList<DotNetProjectCompilation> projects,
        CritterStackScreenplayOptions options);
}

/// <summary>
/// Generates verified Screenplay definitions from independently composed .NET source adapters.
/// </summary>
/// <param name="adapters">The independently identified source adapters.</param>
/// <param name="generator">The shared Screenplay definition generator.</param>
public sealed class CritterStackScreenplayGenerator(
    IReadOnlyList<IDotNetScreenplayAdapter> adapters,
    ScreenplayDefinitionGenerator generator) : ICritterStackScreenplayGenerator
{
    /// <summary>
    /// Initializes the generator with the default Vogen concept and Critter Stack adapters.
    /// </summary>
    public CritterStackScreenplayGenerator()
        : this(
            [new VogenConceptScreenplayAdapter(), new CritterStackScreenplayAdapter()],
            new ScreenplayDefinitionGenerator())
    {
    }

    /// <summary>
    /// Initializes the generator with one adapter and a shared generation pipeline.
    /// </summary>
    /// <param name="adapter">The source adapter.</param>
    /// <param name="generator">The shared Screenplay definition generator.</param>
    public CritterStackScreenplayGenerator(
        IDotNetScreenplayAdapter adapter,
        ScreenplayDefinitionGenerator generator)
        : this([adapter], generator)
    {
    }

    /// <summary>
    /// Initializes the generator with an externally composed adapter list and the default shared generation pipeline.
    /// </summary>
    /// <param name="adapters">The independently identified source adapters.</param>
    public CritterStackScreenplayGenerator(IReadOnlyList<IDotNetScreenplayAdapter> adapters)
        : this(adapters, new ScreenplayDefinitionGenerator())
    {
    }

    /// <inheritdoc/>
    public GeneratedScreenplayDefinition Generate(Compilation compilation, CritterStackScreenplayOptions options)
    {
        var name = compilation.AssemblyName ?? "Application";
        return Generate(
            [
                new DotNetProjectCompilation
                {
                    Name = name,
                    Compilation = compilation,
                    AuthoredSyntaxTrees = compilation.SyntaxTrees.ToHashSet()
                }
            ],
            options,
            useCompatibilityPlacement: true);
    }

    /// <inheritdoc/>
    public GeneratedScreenplayDefinition Generate(
        IReadOnlyList<DotNetProjectCompilation> projects,
        CritterStackScreenplayOptions options) =>
        Generate(
            projects,
            options,
            useCompatibilityPlacement: projects.All(_ => _.SourceContext is null));

    internal GeneratedScreenplayDefinition GenerateCompatibility(
        IReadOnlyList<DotNetProjectCompilation> projects,
        CritterStackScreenplayOptions options) =>
        Generate(projects, options, useCompatibilityPlacement: true);

    GeneratedScreenplayDefinition Generate(
        IReadOnlyList<DotNetProjectCompilation> projects,
        CritterStackScreenplayOptions options,
        bool useCompatibilityPlacement)
    {
        var context = new DotNetAnalysisContext(projects);
        var adapterOptions = new DotNetAdapterOptions
        {
            FeatureRoot = options.FeatureRoot,
            Module = options.Module,
            NamespaceSegmentsToSkip = options.NamespaceSegmentsToSkip
        };
        var contributions = adapters
            .Where(_ => _.CanAnalyze(context))
            .Select(adapter => useCompatibilityPlacement && adapter is CritterStackScreenplayAdapter critterStackAdapter
                ? critterStackAdapter.AnalyzeCompatibility(context, adapterOptions)
                : adapter.Analyze(context, adapterOptions))
            .ToArray();
        var boundContributions = ConceptTypeReferenceBinder.Bind(context, contributions);
        var domain = options.Domain ?? (projects.Count == 1 ? projects[0].Name : "Application");

        return generator.Generate(
            boundContributions,
            new ScreenplayGenerationOptions { Domain = domain });
    }
}
