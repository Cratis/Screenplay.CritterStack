// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Cratis.Screenplay.Generation;

namespace Cratis.CritterStack.Screenplay;

static class BackendOccurrenceKeys
{
    public static ArtifactKey Artifact(SubjectId subject, ArtifactKind kind, BackendOwnership ownership)
    {
        RequireResolved(ownership);
        return new()
        {
            Subject = subject,
            Kind = kind,
            Scope = kind is ArtifactKind.Concept or ArtifactKind.Query ? null : ownership.Scope
        };
    }

    public static string ScopeId(BackendScopeKey? scope) => scope is null
        ? string.Empty
        : $":backend/{Uri.EscapeDataString(scope.Store.Value)}/{(scope.EventModelName is null ? "null" : $"value/{Uri.EscapeDataString(scope.EventModelName)}")}";

    public static string FactId(string legacy, BackendScopeKey? scope) => legacy + ScopeId(scope);

    public static string RelationshipId(string legacy, BackendScopeKey? source, BackendScopeKey? target) =>
        source is null && target is null ? legacy : $"{legacy}:source{ScopeId(source)}:target{ScopeId(target)}";

    static void RequireResolved(BackendOwnership ownership)
    {
        if (!ownership.CanEmit)
        {
            throw new InvalidOperationException("Unresolved backend ownership cannot identify an artifact occurrence");
        }
    }
}
