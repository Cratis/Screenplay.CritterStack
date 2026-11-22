// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Cratis.CritterStack.Screenplay.Marten;
using Cratis.Screenplay.Generation;

namespace Cratis.CritterStack.Screenplay.Wolverine;

internal static partial class WolverineFacts
{
    static BackendOwnership QueryResultOwnership(WolverineFactScope scope, IReadOnlyList<MartenSessionRead> reads)
    {
        var ownerships = reads.Select(read => read.Ownership).Distinct().ToArray();
        return ownerships.Length switch
        {
            0 => scope.Ownership,
            1 => ownerships[0],
            _ => BackendOwnership.Conflict
        };
    }

    static void AddSessionQueryReads(WolverineFactScope scope, SubjectId querySubject, IReadOnlyList<MartenSessionRead> reads)
    {
        var documents = reads.Where(read => read.DocumentType is not null)
            .GroupBy(read => (
                Document: BackendOccurrenceKeys.Artifact(scope.Subjects.SubjectForType(scope.Project, read.DocumentType!), ArtifactKind.Document, read.Ownership),
                read.IsCompiledQuery))
            .Select(group => group.First());
        foreach (var read in documents)
        {
            var documentSubject = scope.Subjects.SubjectForType(scope.Project, read.DocumentType!);
            var kind = read.IsCompiledQuery ? "compiled" : "session";
            scope.Facts.Add(Relationship(
                scope,
                $"wolverine:reads:{kind}:{querySubject.Value}:{documentSubject.Value}",
                querySubject,
                RelationshipKind.Reads,
                documentSubject,
                read.Evidence,
                unscopedSource: true,
                targetOwnership: read.Ownership));
        }
    }
}
