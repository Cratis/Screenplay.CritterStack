// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Cratis.Screenplay.Generation;
using Microsoft.CodeAnalysis;

namespace Cratis.CritterStack.Screenplay.Marten;

sealed class MartenDocumentIdentityFacts(ArtifactKey artifact, IReadOnlyList<IPropertySymbol> properties)
{
    public ArtifactMemberRoleSelectionFact For(MartenDocumentObservation observation, long observationCount)
    {
        var member = observation.HasExplicitIdentity
            ? observation.IdentityMember
            : MartenDocumentMembers.ConventionalIdentityMember(observation.Type);
        var retained = properties.FirstOrDefault(property => SymbolEqualityComparer.Default.Equals(property, member));
        var id = new FactId { Value = BackendOccurrenceKeys.FactId($"{observation.Evidence.Adapter.Id}:document-identity:{artifact.Subject.Value}:{observation.Site}", artifact.Scope) };
        return new()
        {
            Id = id,
            Subject = artifact.Subject,
            Evidence = observation.Evidence,
            Definition = new()
            {
                Artifact = artifact,
                Role = ArtifactMemberRoleKind.EventSourceIdentifier,
                Member = retained is null ? null : new() { Artifact = artifact, Name = MartenDocumentMembers.PropertyName(retained.Name) },
                Priority = observation.HasExplicitIdentity ? 1 : 0,
                ObservationOrder = observation.HasExplicitIdentity ? observation.Order : FallbackOrder(observation, observationCount),
                Diagnostics = Diagnostics(observation, member, retained)
            }
        };
    }

    public GenerationDiagnostic? UnresolvedDiagnostic(MartenDocumentObservation observation) =>
        observation.HasExplicitIdentity && observation.IdentityMember is null
            ? new()
            {
                Code = MartenDiagnosticCodes.DocumentIdentityUnresolved,
                Severity = GenerationDiagnosticSeverity.Warning,
                Outcome = GenerationDiagnosticOutcome.Unknown,
                Subject = artifact.Subject,
                Source = observation.Evidence.Source,
                Message = $"Marten identity configuration for '{observation.Type.Name}' is not a direct member expression and cannot be resolved safely"
            }
            : null;

    static long FallbackOrder(MartenDocumentObservation observation, long observationCount) =>
        ((observation.Preference == OmissionDiagnosticEvidencePreference.ExplicitRegistration ? 2 : 1) * observationCount) - observation.Order;

    IReadOnlyList<GenerationDiagnostic> Diagnostics(MartenDocumentObservation observation, ISymbol? member, IPropertySymbol? retained)
    {
        if (retained is not null || member is null) return [];
        var source = observation.Evidence.Source;
        return
        [
            new()
            {
                Code = MartenDiagnosticCodes.DocumentIdentityUnresolved,
                Severity = GenerationDiagnosticSeverity.Warning,
                Outcome = GenerationDiagnosticOutcome.Unsupported,
                Subject = artifact.Subject,
                Source = source,
                Message = $"Marten identity member '{member.ContainingType?.Name}.{member.Name}' cannot be represented uniquely in the emitted '{observation.Type.Name}' document property shape"
            }
        ];
    }
}
