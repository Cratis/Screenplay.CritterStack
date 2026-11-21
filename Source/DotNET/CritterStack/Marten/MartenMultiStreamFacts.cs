// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Cratis.Screenplay.Generation;

namespace Cratis.CritterStack.Screenplay.Marten;

static class MartenMultiStreamFacts
{
    public static void Add(
        MartenCollectedFacts collected,
        SubjectId modelSubject,
        SubjectId projectionSubject,
        MartenMultiStreamConfiguration configuration)
    {
        foreach (var identity in configuration.Identities)
        {
            AddIdentityFacts(collected, modelSubject, projectionSubject, identity);
        }

        foreach (var fanOut in configuration.FanOuts)
        {
            AddFanOutFacts(collected, modelSubject, projectionSubject, fanOut);
        }
    }

    static void AddIdentityFacts(
        MartenCollectedFacts collected,
        SubjectId modelSubject,
        SubjectId projectionSubject,
        MartenMultiStreamIdentity identity)
    {
        var eventSubject = collected.Subjects.SubjectForType(collected.Project, identity.EventType);
        var evidenceId = MartenAggregateProjectionFacts.EvidenceId(identity.Evidence);
        MartenAggregateProjectionFacts.AddEventFacts(
            collected,
            modelSubject,
            new(
                identity.EventType,
                identity.Evidence,
                identity.Evidence with
                {
                    Strength = EvidenceStrength.Heuristic,
                    Explanation = "The configured multi-stream event and project provide the default Screenplay placement"
                },
                evidenceId));
        var discriminator = identity.IsOneToMany
            ? $"marten:identities:{identity.TargetMember}"
            : $"marten:identity:{identity.TargetMember}";
        collected.Facts.Add(new MartenRelationshipFact
        {
            Id = $"marten:grouping:{projectionSubject.Value}:{eventSubject.Value}:{discriminator}:{evidenceId}",
            Source = projectionSubject,
            Kind = RelationshipKind.Consumes,
            Target = eventSubject,
            Evidence = identity.Evidence,
            TargetMember = identity.TargetMember,
            Discriminator = discriminator,
            IsCollection = identity.IsOneToMany
        }.ToFact());
    }

    static void AddFanOutFacts(
        MartenCollectedFacts collected,
        SubjectId modelSubject,
        SubjectId projectionSubject,
        MartenMultiStreamFanOut fanOut)
    {
        var parentSubject = collected.Subjects.SubjectForType(collected.Project, fanOut.ParentEventType);
        var childSubject = collected.Subjects.SubjectForType(collected.Project, fanOut.ChildEventType);
        var evidenceId = MartenAggregateProjectionFacts.EvidenceId(fanOut.Evidence);
        var placementEvidence = fanOut.Evidence with
        {
            Strength = EvidenceStrength.Heuristic,
            Explanation = "The configured fan-out event and project provide the default Screenplay placement"
        };
        MartenAggregateProjectionFacts.AddEventFacts(collected, modelSubject, new(fanOut.ParentEventType, fanOut.Evidence, placementEvidence, evidenceId));
        MartenAggregateProjectionFacts.AddEventFacts(collected, modelSubject, new(fanOut.ChildEventType, fanOut.Evidence, placementEvidence, evidenceId));
        collected.Facts.Add(new MartenRelationshipFact
        {
            Id = $"marten:fan-out-consumes:{projectionSubject.Value}:{parentSubject.Value}:{childSubject.Value}:{evidenceId}",
            Source = projectionSubject,
            Kind = RelationshipKind.Consumes,
            Target = parentSubject,
            Evidence = fanOut.Evidence,
            Discriminator = $"marten:fan-out-source:{childSubject.Value}"
        }.ToFact());
        collected.Facts.Add(new MartenRelationshipFact
        {
            Id = $"marten:fan-out-child:{projectionSubject.Value}:{parentSubject.Value}:{childSubject.Value}:{evidenceId}",
            Source = projectionSubject,
            Kind = RelationshipKind.Consumes,
            Target = childSubject,
            Evidence = fanOut.Evidence,
            SourceMember = fanOut.SourceMember,
            Discriminator = $"marten:fan-out-child:{parentSubject.Value}:{fanOut.Mode}:{fanOut.SourceMember}",
            IsCollection = true
        }.ToFact());
    }
}
