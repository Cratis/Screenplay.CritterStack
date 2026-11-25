// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Cratis.Screenplay.Generation;
using Cratis.Screenplay.Generation.DotNet;
using Microsoft.CodeAnalysis;

namespace Cratis.CritterStack.Screenplay.Marten;

sealed class MartenSessionReadFacts(
    DotNetProjectCompilation project,
    AdapterIdentity adapter,
    CritterStackSubjectResolver subjects,
    MartenBackendOwnershipResolver ownership)
{
    public IReadOnlyList<GenerationFact> Emit(IReadOnlyList<MartenDocumentObservation> documents)
    {
        var discovery = new MartenSessionReadDiscovery(project, adapter, ownership);
        var declarations = documents.Where(document => document.Relationship == RelationshipKind.Reads && document.Ownership.CanEmit).ToArray();
        return
        [
            .. new DotNetArtifactCatalog(project.Compilation).Types
                .SelectMany(type => type.GetMembers().OfType<IMethodSymbol>())
                .SelectMany(method => discovery.Discover(method, ownership.ForMethod(project, method))
                    .Where(read => IsDeclaredRead(read, declarations))
                    .SelectMany(read => FactsFor(method, read)))
        ];
    }

    static bool IsDeclaredRead(MartenSessionRead read, IReadOnlyList<MartenDocumentObservation> declarations) =>
        !read.IsCompiledQuery && read.DocumentType is not null && read.Ownership.CanEmit &&
        declarations.Any(document => document.Evidence.Source == read.Evidence.Source &&
            document.Ownership == read.Ownership &&
            SymbolEqualityComparer.Default.Equals(document.Type, read.DocumentType));

    IReadOnlyList<GenerationFact> FactsFor(IMethodSymbol method, MartenSessionRead read)
    {
        var methodSubject = CritterStackMethodFacts.SubjectFor(project, method);
        var declaration = subjects.SubjectForType(project, read.DocumentType!);
        var artifact = BackendOccurrenceKeys.Artifact(declaration, ArtifactKind.Document, read.Ownership);
        var site = read.Evidence.Source!;
        var prefix = BackendOccurrenceKeys.FactId($"{adapter.Id}:session-read:{methodSubject.Value}:{site.Path}:{site.StartLine}:{site.StartColumn}", artifact.Scope);
        GenerationFact[] facts =
        [
            new ArtifactUsageDeclarationFact
            {
                Id = new() { Value = $"{prefix}:declaration" },
                Subject = declaration,
                Evidence = read.Evidence,
                Definition = new() { Declaration = declaration, Artifact = artifact }
            },
            new MethodArtifactUsageFact
            {
                Id = new() { Value = $"{prefix}:execution" },
                Subject = methodSubject,
                Evidence = read.Evidence,
                Definition = new()
                {
                    Method = methodSubject,
                    Declaration = declaration,
                    Kind = RelationshipKind.Reads,
                    TargetScope = artifact.Scope
                }
            }
        ];
        return [.. facts, .. facts.Select(fact => CritterStackMethodFacts.Support(fact, [CritterStackMethodFacts.Observe(project, method)]))];
    }
}
