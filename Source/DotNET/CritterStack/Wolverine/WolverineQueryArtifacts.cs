// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Cratis.Screenplay.Generation;
using Cratis.Screenplay.Generation.DotNet;

namespace Cratis.CritterStack.Screenplay.Wolverine;

sealed class WolverineQueryArtifacts(
    DotNetProjectCompilation project,
    DotNetAnalysisContext context,
    DotNetAdapterOptions options,
    AdapterIdentity adapter)
{
    public WolverineDiscoveryResult Emit(WolverineQueryObservation query, BackendOwnership resultOwnership)
    {
        var evidence = CritterStackSource.EvidenceFor(query.Endpoint.Method, adapter, project, EvidenceStrength.Exact, $"Wolverine HTTP {query.Endpoint.Verb} endpoint");
        var key = new ArtifactKey { Subject = query.Subject, Kind = ArtifactKind.Query };
        var prefix = $"{adapter.Id}:query:{query.Subject.Value}";
        List<GenerationFact> facts =
        [
            Declaration(key, query.Name, evidence.Source?.Path, evidence),
            .. WolverineFacts.QueryMemberFacts(query.Endpoint.Method, key, context, evidence)
        ];
        List<CritterStackPlacementIntent> placements =
        [
            new(
                $"{prefix}:placement",
                key,
                project.SubjectForType(query.Endpoint.Method.ContainingType),
                Placement(query.Result.Model.Name, query.Name),
                evidence)
        ];
        List<GenerationDiagnostic> diagnostics = [HttpMetadata(query, evidence)];
        if (!resultOwnership.CanEmit)
        {
            diagnostics.Add(resultOwnership.Diagnostic(DotNetMethodIdentity.SubjectFor(project, query.Endpoint.Method), evidence.Source, "Marten-backed handler ownership cannot be proved"));
            return new(facts, diagnostics, placements);
        }

        var result = EmitResult(query, resultOwnership, evidence);
        return new([.. facts, .. result.Facts], diagnostics, [.. placements, .. result.Placements!]);
    }

    static ArtifactDeclarationFact Declaration(ArtifactKey key, string name, string? file, Evidence evidence) => new()
    {
        Id = new() { Value = BackendOccurrenceKeys.FactId($"{evidence.Adapter.Id}:query-artifact:{key.Subject.Value}:{key.Kind}", key.Scope) },
        Subject = key.Subject,
        Evidence = evidence,
        Definition = new() { Artifact = key, Name = name, File = file }
    };

    static GenerationDiagnostic HttpMetadata(WolverineQueryObservation query, Evidence evidence) => new()
    {
        Code = WolverineDiagnosticCodes.HttpMetadataOmitted,
        Severity = GenerationDiagnosticSeverity.Information,
        Outcome = GenerationDiagnosticOutcome.Unsupported,
        Message = $"HTTP {query.Endpoint.Verb} route '{query.Endpoint.Route}' for query '{query.Name}' is not represented by the current Screenplay language",
        Source = evidence.Source,
        Subject = query.Subject
    };

    WolverineDiscoveryResult EmitResult(WolverineQueryObservation query, BackendOwnership ownership, Evidence evidence)
    {
        var model = query.Result.Model;
        var key = BackendOccurrenceKeys.Artifact(context.SubjectForType(model)!, ArtifactKind.ReadModel, ownership);
        var members = DotNetTypeUseFacts.Emit(model, key, context, evidence);
        List<GenerationFact> facts =
        [
            Declaration(key, model.Name, CritterStackSource.EvidenceFor(model, adapter, project, EvidenceStrength.Exact).Source?.Path, evidence),
            .. members
        ];
        facts.AddRange(members.OfType<ArtifactMemberTypeUseFact>()
            .Where(fact => fact.Definition.Type.ObservedTypeSubject is not null)
            .Select(fact => new TypeUseBindingPolicyFact
            {
                Id = new() { Value = $"{fact.Id.Value}:binding" },
                Subject = key.Subject,
                Evidence = evidence,
                Definition = new() { TypeUse = fact.Id, Policy = TypeUseBindingPolicyKind.ConceptOnlyWithUnboundFallback }
            }));
        CritterStackPlacementIntent placement = new(
            BackendOccurrenceKeys.FactId($"{adapter.Id}:query-result:{key.Subject.Value}:placement", key.Scope),
            key,
            null,
            Placement(model.Name, model.Name),
            evidence with
            {
                Strength = EvidenceStrength.Heuristic,
                Explanation = "The returned model name provides the default Screenplay placement"
            });
        facts.Add(new RelationshipFact
        {
            Id = new() { Value = BackendOccurrenceKeys.RelationshipId($"{adapter.Id}:returns:{query.Subject.Value}:{key.Subject.Value}", null, key.Scope) },
            Subject = query.Subject,
            Evidence = evidence,
            Definition = new()
            {
                Key = new() { Source = query.Subject, Kind = RelationshipKind.Returns, Target = key.Subject, TargetScope = key.Scope },
                IsCollection = query.Result.IsCollection,
                IsOptional = query.Result.IsOptional
            }
        });
        return new(facts, [], [placement]);
    }

    ArtifactPlacement Placement(string feature, string slice) =>
        CritterStackSourcePlacement.CompatibilityPlacement(project, options, feature, slice, GenerationSliceKind.StateView);
}
