// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Cratis.Screenplay.Generation;
using Cratis.Screenplay.Generation.DotNet;
using Microsoft.CodeAnalysis;

namespace Cratis.CritterStack.Screenplay;

/// <summary>
/// Defines a generator that creates a verified Screenplay definition from Marten and Wolverine source.
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
/// Generates verified Screenplay definitions from Marten and Wolverine source.
/// </summary>
/// <param name="adapter">The Critter Stack source adapter.</param>
/// <param name="generator">The shared Screenplay definition generator.</param>
public sealed class CritterStackScreenplayGenerator(
    IDotNetScreenplayAdapter adapter,
    ScreenplayDefinitionGenerator generator) : ICritterStackScreenplayGenerator
{
    /// <summary>
    /// Initializes the generator with the default Critter Stack adapter and shared generation pipeline.
    /// </summary>
    public CritterStackScreenplayGenerator()
        : this(new CritterStackScreenplayAdapter(), new ScreenplayDefinitionGenerator())
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
                    Compilation = compilation
                }
            ],
            options);
    }

    /// <inheritdoc/>
    public GeneratedScreenplayDefinition Generate(
        IReadOnlyList<DotNetProjectCompilation> projects,
        CritterStackScreenplayOptions options)
    {
        var context = new DotNetAnalysisContext(projects);
        var contribution = adapter.Analyze(
            context,
            new DotNetAdapterOptions
            {
                Module = options.Module,
                NamespaceSegmentsToSkip = options.NamespaceSegmentsToSkip
            });
        var domain = options.Domain ?? (projects.Count == 1 ? projects[0].Name : "Application");

        return generator.Generate(
            [contribution],
            new ScreenplayGenerationOptions { Domain = domain });
    }
}
