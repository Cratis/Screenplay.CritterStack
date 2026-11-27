// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Text.Json;

namespace Cratis.CritterStack.Screenplay.for_FactParity.given;

/// <summary>Asserts exact handler parity through the real Marten and Wolverine descriptors in both orders.</summary>
public abstract class a_document_delete_handler_comparison : a_native_http_command_comparison
{
    // The real probe requires an authored Wolverine API use, independently of conventional entry-point discovery.
    const string WolverineProbeSource = "namespace FactParity { [Wolverine.WolverineIgnore] public sealed class Ignored; }";

    protected IEnumerable<GeneratedScreenplayDefinition> Composites => [Composed, ComposedReverse, PublicComposed, PublicComposedReverse];

    protected IEnumerable<RelationshipFact> NativeDeletes => Native.Facts.OfType<RelationshipFact>().Where(fact => fact.Definition.Key.Kind == RelationshipKind.Deletes);

    protected IEnumerable<EntryPointFact> EntryPoints => Native.Facts.OfType<EntryPointFact>();

    protected void CompareHandlers(string application) => CompareWithMarten($"{WolverineProbeSource}\n{application}");

    protected void CompareHandlerFiles(params (string Source, string Path)[] files) =>
        CompareHandlerFiles(DotNetSourceDisplayRoot.Workspace, files);

    protected void CompareHandlerFiles(DotNetSourceDisplayRoot displayRoot, params (string Source, string Path)[] files) =>
        CompareWithMartenFiles(displayRoot, [(WolverineProbeSource, "/workspace/FactParity/WolverineProbe.cs"), .. files]);

    protected bool AllAdmitted() => Composites.All(result =>
        Admitted(result) && result.AdapterRun is { Adapters.Length: 2 } run &&
        run.Adapters.Count(record => record.Descriptor.Identity == MartenIdentity) == 1 &&
        run.Adapters.All(record => record is { Executed: true, Probe: AdapterProbeApplicable }));

    protected SubjectId DocumentSubject(string name) => ComparedProject.SubjectForType(fact_vectors.Type(ComparedProject, $"FactParity.{name}"));

    protected SubjectId MethodSubject(string type, string name = "Handle") => DotNetMethodIdentity.SubjectFor(
        ComparedProject, fact_vectors.Type(ComparedProject, $"FactParity.{type}").GetMembers(name).OfType<IMethodSymbol>().Single());

    protected static IEnumerable<RelationshipKey> Outgoing(AdapterContribution contribution, SubjectId source) => contribution.Facts
        .OfType<RelationshipFact>().Select(fact => fact.Definition.Key).Where(key => key.Source == source);

    protected static IEnumerable<ArtifactKey> Declared(AdapterContribution contribution, ArtifactKind kind) => contribution.Facts
        .OfType<ArtifactDeclarationFact>().Select(fact => fact.Definition.Artifact).Where(key => key.Kind == kind);

    protected static RelationshipKey Operation(SubjectId source, RelationshipKind kind, SubjectId target, BackendScopeKey? scope = null) => new()
    {
        Source = source,
        Target = target,
        Kind = kind,
        SourceScope = scope,
        TargetScope = scope
    };

    protected bool AllArtifactsMatch() => Composites.All(actual => ExactArtifacts(actual) == ExactArtifacts(Expected));

    protected bool AllRelationshipsMatch() => Composites.All(actual => Relationships(actual) == Relationships(Expected));

    protected void AssertDiagnosticsMatch()
    {
        foreach (var actual in Composites)
        {
            Diagnostics(actual).ShouldEqual(Diagnostics(Expected));
        }
    }

    protected bool AllSourcesMatch() => Composites.All(actual => actual.Source == Expected.Source);

    protected bool AllStatusesMatch() => Composites.All(actual => actual.IsSuccess == Expected.IsSuccess);

    protected static string ExactArtifacts(GeneratedScreenplayDefinition result) => JsonSerializer.Serialize(
        result.Graph.Artifacts.SelectMany(artifact => artifact.Variants).Select(variant => variant.Definition));
}
