// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Cratis.Screenplay.Generation;
using Cratis.Screenplay.Generation.DotNet;
using Microsoft.CodeAnalysis;

namespace Cratis.CritterStack.Screenplay.Marten;

sealed record MartenDocumentCandidateResult(IReadOnlyList<GenerationFact> Facts, IReadOnlyList<GenerationDiagnostic> Diagnostics);

sealed class MartenDocumentCandidates(DotNetProjectCompilation project, DotNetAnalysisContext context, CritterStackSubjectResolver subjects)
{
    readonly List<GenerationFact> _facts = [];
    readonly List<GenerationDiagnostic> _diagnostics = [];

    public MartenDocumentCandidateResult Discover(IReadOnlyList<MartenDocumentObservation> observations)
    {
        foreach (var observation in observations.Where(observation => !observation.Ownership.CanEmit))
        {
            _diagnostics.Add(observation.Ownership.Diagnostic(
                observation.Method is { } method ? DotNetMethodIdentity.SubjectFor(project, method) : new() { Value = project.Name },
                observation.Evidence.Source,
                "Marten document operation ownership cannot be proved"));
        }

        var orderLimit = observations.Count == 0 ? 1 : observations.Max(observation => observation.Order) + 1;
        foreach (var occurrence in observations.Where(observation => observation.Ownership.CanEmit)
                     .GroupBy(observation => BackendOccurrenceKeys.Artifact(subjects.SubjectForType(project, observation.Type), ArtifactKind.Document, observation.Ownership)))
        {
            AddOccurrence(occurrence.Key, [.. occurrence.DistinctBy(observation => observation.Site)], orderLimit);
        }

        return new(_facts, _diagnostics);
    }

    static OccurrenceOmissionDiagnosticFact Omission(ArtifactKey artifact, MartenDocumentObservation observation, string prefix) => new()
    {
        Id = new() { Value = $"{prefix}:omission:{observation.Site}" },
        Subject = artifact.Subject,
        Evidence = observation.Evidence,
        Definition = new()
        {
            Family = new() { Value = MartenDiagnosticCodes.DocumentModelOmitted },
            Occurrence = artifact,
            Policy = OccurrenceOmissionDiagnosticPolicy.OnePerOccurrence,
            Preference = observation.Preference,
            Diagnostic = new()
            {
                Code = MartenDiagnosticCodes.DocumentModelOmitted,
                Severity = GenerationDiagnosticSeverity.Information,
                Outcome = GenerationDiagnosticOutcome.Unsupported,
                Subject = artifact.Subject,
                Source = observation.Evidence.Source,
                Facts = [new() { Value = prefix }],
                Message = $"Marten document '{observation.Type.Name}' is persisted, queried, or explicitly configured, but the current Screenplay language has no ordinary document-state declaration"
            }
        }
    };

    void AddOccurrence(ArtifactKey artifact, MartenDocumentObservation[] observations, long orderLimit)
    {
        var first = observations[0];
        var properties = MartenDocumentMembers.PropertiesOf(first.Type);
        var prefix = BackendOccurrenceKeys.FactId($"{first.Evidence.Adapter.Id}:document:{artifact.Subject.Value}", artifact.Scope);
        foreach (var fact in Declarations(artifact, properties, first, prefix))
        {
            AddSupported(fact, observations);
        }

        // Compatibility seeds documents from ordered projection operations before scanning ordinary uses.
        var projected = observations.Where(observation => observation.IsProjected).OrderBy(observation => observation.Order).FirstOrDefault();
        if (projected is not null)
        {
            AddSupported(Omission(artifact, projected, prefix), observations);
        }

        var identity = new MartenDocumentIdentityFacts(artifact, properties);
        foreach (var observation in observations)
        {
            AddSupported(identity.For(observation, orderLimit), observation);
            var unresolved = identity.UnresolvedDiagnostic(observation);
            if (projected is null || observation.Preference != OmissionDiagnosticEvidencePreference.UseSite)
            {
                AddSupported(Omission(artifact, observation, prefix), observation, unresolved is null ? [] : [unresolved]);
            }
            AddRelationship(artifact, observation, prefix);
        }
    }

    List<GenerationFact> Declarations(ArtifactKey artifact, IReadOnlyList<IPropertySymbol> properties, MartenDocumentObservation observation, string prefix)
    {
        List<GenerationFact> facts =
        [
            new ArtifactDeclarationFact
            {
                Id = new() { Value = prefix },
                Subject = artifact.Subject,
                Evidence = observation.Evidence,
                Definition = new() { Artifact = artifact, Name = observation.Type.Name, File = MartenSourceFiles.FileOf(observation.Type, project) }
            }
        ];
        for (var order = 0; order < properties.Count; order++)
        {
            var property = properties[order];
            var type = MartenDocumentMembers.TypeUseFor(property.Type, context);
            var member = new ArtifactMemberKey { Artifact = artifact, Name = MartenDocumentMembers.PropertyName(property.Name) };
            var evidence = CritterStackSource.EvidenceFor(property, observation.Evidence.Adapter, project, EvidenceStrength.Exact, "Marten document property");
            facts.Add(new ArtifactMemberDeclarationFact
            {
                Id = new() { Value = $"{prefix}:member:{property.Name}" },
                Subject = artifact.Subject,
                Evidence = evidence,
                Definition = new() { Member = member, DeclarationOrder = order }
            });
            var typeUse = new ArtifactMemberTypeUseFact
            {
                Id = new() { Value = $"{prefix}:type:{property.Name}" },
                Subject = artifact.Subject,
                Evidence = evidence,
                Definition = new() { Member = member, Type = type }
            };
            facts.Add(typeUse);
            if (typeUse.Definition.Type.ObservedTypeSubject is not null)
            {
                facts.Add(new TypeUseBindingPolicyFact
                {
                    Id = new() { Value = $"{typeUse.Id.Value}:binding" },
                    Subject = artifact.Subject,
                    Evidence = evidence,
                    Definition = new() { TypeUse = typeUse.Id, Policy = TypeUseBindingPolicyKind.ConceptOnlyWithUnboundFallback }
                });
            }
        }

        return facts;
    }

    void AddRelationship(ArtifactKey artifact, MartenDocumentObservation observation, string prefix)
    {
        if (observation.Method is not { } method || observation.Relationship is not { } kind) return;
        if (!observation.HandlerOwnership.CanEmit)
        {
            _diagnostics.Add(observation.HandlerOwnership.Diagnostic(DotNetMethodIdentity.SubjectFor(project, method), observation.Evidence.Source, "Marten document operation ownership cannot be proved"));
            return;
        }

        var methodSubject = DotNetMethodIdentity.SubjectFor(project, method);
        AddSupported(
            new ArtifactDeclarationFact
            {
                Id = new() { Value = $"{prefix}:handler:{observation.Site}" },
                Subject = methodSubject,
                Evidence = observation.Evidence,
                Definition = new()
                {
                    Artifact = BackendOccurrenceKeys.Artifact(methodSubject, ArtifactKind.Handler, observation.HandlerOwnership),
                    Name = DotNetMethodIdentity.DisplayName(method),
                    File = MartenSourceFiles.FileOf(method, project)
                }
            },
            observation);
        AddSupported(
            new RelationshipFact
            {
                Id = new() { Value = $"{prefix}:relationship:{observation.Site}" },
                Subject = methodSubject,
                Evidence = observation.Evidence,
                Definition = new()
                {
                    Key = new()
                    {
                        Source = methodSubject,
                        Kind = kind,
                        Target = artifact.Subject,
                        SourceScope = observation.HandlerOwnership.Scope,
                        TargetScope = artifact.Scope
                    }
                }
            },
            observation);
    }

    void AddSupported(GenerationFact fact, IReadOnlyList<MartenDocumentObservation> observations)
    {
        _facts.Add(fact);
        if (observations.All(observation => observation.Method is not null))
        {
            _facts.Add(CritterStackMethodFacts.Support(
                fact,
                [.. observations
                    .Select(observation => CritterStackMethodFacts.Observe(project, observation.Method!, observation.Ownership.Scope))
                    .DistinctBy(alternative => alternative.Method)]));
        }
    }

    void AddSupported(GenerationFact fact, MartenDocumentObservation observation, IReadOnlyList<GenerationDiagnostic>? diagnostics = null)
    {
        _facts.Add(fact);
        if (observation.Method is not { } method)
        {
            _diagnostics.AddRange(diagnostics ?? []);
            return;
        }

        _facts.Add(CritterStackMethodFacts.Support(fact, [CritterStackMethodFacts.Observe(project, method, observation.Ownership.Scope, diagnostics)]));
    }
}
