// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Cratis.Screenplay.Generation;
using Cratis.Screenplay.Generation.DotNet;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Cratis.CritterStack.Screenplay.Wolverine;

/// <summary>
/// The one persisting effect family the native HTTP command path emits: direct, source-resolved typed document deletions.
/// </summary>
internal static partial class WolverineFacts
{
    // An endpoint is a document-delete command only when every persistence call in its source is an observed typed delete and nothing else
    // (events, loads, state inputs, bus, returned messages or storage actions, aggregates, streams) contributes a consequence.
    static bool IsDocumentDeleteEndpointCommand(WolverineFactScope scope, EndpointCommand command)
    {
        var shape = command.Shape;
        var method = command.Endpoint.Method;
        var deletes = DocumentDeletes(method, scope.Project).Select(_ => _.Invocation).ToHashSet();
        return deletes.Count > 0 &&
               HasNoProducedEvents(shape) &&
               !shape.AppendDiscovery.HasDirectWrite &&
               shape.Dcb is null &&
               shape.Aggregate is null &&
               shape.StreamBindings.Count == 0 &&
               shape.BusConsequences.Count == 0 &&
               shape.OutgoingMessages.Count == 0 &&
               shape.ReturnConsequences.All(_ => _.Kind == WolverineReturnConsequenceKind.HttpResponse) &&
               !method.Parameters.Any(_ => IsStateInputParameter(_, scope.Project)) &&
               HasOnlyObservedPersistenceCalls(scope, method, deletes);
    }

    // One deletion fact per invocation site: repeated deletes of the same document keep distinct FactIds (the admission validator rejects duplicates and
    // would drop the whole contribution) while the relationship key, and so the graph, stays the same.
    static void AddNativeDocumentDeletes(WolverineFactScope scope, MessageEmission emission, IMethodSymbol method)
    {
        foreach (var (documentType, invocation) in DocumentDeletes(method, emission.Project))
        {
            var ownership = scope.Resolver?.ForInvocation(scope.Project, invocation, method) ?? scope.Ownership;
            if (!ownership.CanEmit)
            {
                scope.Diagnostics.Add(ownership.Diagnostic(emission.SourceSubject, CritterStackSource.RangeForProject(invocation.GetLocation(), scope.Project), "The backend owning this document deletion could not be proven"));
                continue;
            }

            var documentSubject = emission.Subjects.SubjectForType(emission.Project, documentType);
            var site = invocation.GetLocation().GetLineSpan().Span.Start;
            emission.Facts.Add(Relationship(
                $"wolverine:deletes:{emission.SourceSubject.Value}:{documentSubject.Value}:{invocation.SpanStart}:{site.Line}:{site.Character}",
                emission.SourceSubject,
                RelationshipKind.Deletes,
                documentSubject,
                emission.Evidence,
                sourceScope: emission.Ownership.Scope,
                targetScope: ownership.Scope));
        }
    }

    static bool HasOnlyObservedPersistenceCalls(WolverineFactScope scope, IMethodSymbol method, HashSet<InvocationExpressionSyntax> deletes) =>
        WolverineMethodSyntax.Declarations(method, scope.Project).All(declaration =>
            declaration.Declaration.DescendantNodes()
                .OfType<InvocationExpressionSyntax>()
                .Where(invocation => !deletes.Contains(invocation))
                .All(invocation => declaration.SemanticModel.GetSymbolInfo(invocation).Symbol is not IMethodSymbol invoked || !IsPersistenceNamespace(invoked)));
}
