// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Text.Json;
using static Cratis.CritterStack.Screenplay.for_FactParity.given.fact_vectors;
using diagnostic_parity = Cratis.CritterStack.Screenplay.given.diagnostic_parity;

namespace Cratis.CritterStack.Screenplay.for_FactParity.given;

/// <summary>
/// Compares the native effect-free HTTP command fragment, run through the real Wolverine descriptor, identity and probe in the public
/// described-roster generator, to the compatibility oracle with full diagnostics.
/// </summary>
public abstract class a_native_http_command_comparison : Specification
{
    protected const string Framework =
        """
        #nullable enable
        namespace Wolverine
        {
            public sealed class WolverineOptions;
            public sealed class WolverineIgnoreAttribute : System.Attribute;
        }
        namespace Wolverine.Http
        {
            public sealed class WolverinePostAttribute(string route) : System.Attribute;
            public sealed class WolverinePutAttribute(string route) : System.Attribute;
            public sealed class WolverineDeleteAttribute(string route) : System.Attribute;
        }
        """;

    protected GeneratedScreenplayDefinition Expected { get; private set; } = null!;

    protected GeneratedScreenplayDefinition Public { get; private set; } = null!;

    protected AdapterContribution Native { get; private set; } = null!;

    protected AdapterContributionAdmissionResult Admission { get; private set; } = null!;

    protected void Compare(string application)
    {
        var project = WithSourceContext(Project($"{Framework}\n{application}"));
        var context = new DotNetAnalysisContext([project]);
        var bridge = new real_wolverine_descriptor_bridge();
        Native = bridge.Analyze(context, new());
        Admission = AdapterContributionAdmission.Admit(bridge.Descriptor, Native);
        Expected = Generate(new CritterStackScreenplayAdapter().Analyze(context, new()));
        Public = new CritterStackScreenplayGenerator([bridge]).Generate([project], new CritterStackScreenplayOptions { Domain = "FactParity" });
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

    // The native binder resolves a concept-bound enum use to TargetArtifactKind Concept where the compatibility reader leaves it null
    // (same precedent as a_marten_concept_comparison); only that one field is normalized, and its exact shape is pinned separately.
    protected string Artifacts(GeneratedScreenplayDefinition result) =>
        JsonSerializer.Serialize(result.Graph.Artifacts.SelectMany(artifact => artifact.Variants).Select(variant => variant.Definition))
            .Replace("\"TargetArtifactKind\":1", "\"TargetArtifactKind\":null", StringComparison.Ordinal);

    protected bool ConceptTargetsAreOnlyBoundSubjects(GeneratedScreenplayDefinition result) =>
        Commands(result).SelectMany(_ => _.Properties).All(_ => _.Type.TargetArtifactKind is null || (_.Type.Subject is not null && _.Type.TargetArtifactKind == ArtifactKind.Concept));

    protected string Relationships(GeneratedScreenplayDefinition result) =>
        JsonSerializer.Serialize(result.Graph.Relationships.SelectMany(relationship => relationship.Definitions));

    protected string[] Diagnostics(GeneratedScreenplayDefinition result) => diagnostic_parity.Meaning(result.Diagnostics);

    protected IEnumerable<ArtifactDefinition> Commands(GeneratedScreenplayDefinition result) =>
        result.Graph.Artifacts.SelectMany(artifact => artifact.Variants).Select(variant => variant.Definition).Where(_ => _.Key.Kind == ArtifactKind.Command);

    protected string[] Identifiers(GeneratedScreenplayDefinition result) =>
        [.. Commands(result).Select(command => $"{command.Name}:{string.Join(',', command.Properties.Where(_ => _.IsIdentifier).Select(_ => _.Name))}")];

    protected bool Declares(GenerationFactCapability capability) =>
        Native.Facts.Any(fact => fact switch
        {
            ArtifactMemberDeclarationFact => capability == GenerationFactCapability.ArtifactMemberDeclaration,
            ArtifactMemberTypeUseFact => capability == GenerationFactCapability.ArtifactMemberTypeUse,
            ArtifactMemberRoleFact => capability == GenerationFactCapability.ArtifactMemberRole,
            TypeUseBindingPolicyFact => capability == GenerationFactCapability.TypeUseBindingPolicy,
            EntryPointFact => capability == GenerationFactCapability.EntryPoint,
            ArtifactDeclarationFact => capability == GenerationFactCapability.ArtifactDeclaration,
            ArtifactPlacementFact => capability == GenerationFactCapability.ArtifactPlacement,
            _ => false
        });

    /// <summary>Exposes the real Wolverine descriptor, identity and probe; analysis is the native production fragment only.</summary>
    sealed class real_wolverine_descriptor_bridge : IDescribedDotNetScreenplayAdapter, IDotNetScreenplayAdapter
    {
        readonly WolverineScreenplayAdapter _real = new();

        public AdapterIdentity Identity => _real.Identity;

        public AdapterDescriptor Descriptor => _real.Descriptor;

        public bool CanAnalyze(DotNetAnalysisContext context) => _real.CanAnalyze(context);

        public AdapterProbeResult Probe(DotNetAnalysisContext context) => _real.Probe(context);

        public AdapterContribution Analyze(DotNetAnalysisContext context, DotNetAdapterOptions options) =>
            WolverineFacts.EmitNativeHttpCommands(context, options, Identity);
    }
}
