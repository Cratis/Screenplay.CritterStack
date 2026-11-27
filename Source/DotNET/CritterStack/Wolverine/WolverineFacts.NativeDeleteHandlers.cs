// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Cratis.CritterStack.Screenplay.Marten;
using Cratis.Screenplay.Generation;
using Cratis.Screenplay.Generation.DotNet;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Cratis.CritterStack.Screenplay.Wolverine;

internal static partial class WolverineFacts
{
    static void EmitNativeDeleteHandler(
        WolverineFactScope scope,
        DotNetAnalysisContext context,
        IMethodSymbol method,
        WolverineValidationAuthorizationDiscoveryResult validation)
    {
        var request = RequestParameter(method, scope.Project);
        if (request?.Type is not INamedTypeSymbol requestType || !IsDirectDeleteEntryPoint(scope, method, requestType))
        {
            return;
        }

        var shape = DiscoverStateChangeShape(method, request, requestType, scope, isHttpEndpoint: false);
        if (!IsDeleteOnlyHandler(scope.Project, shape))
        {
            return;
        }

        AnalyzeWithSlicePattern(scope, method, null, owned => AddNativeDeleteHandlerFacts(owned, context, new(method, requestType), validation));
    }

    static bool IsDirectDeleteEntryPoint(WolverineFactScope scope, IMethodSymbol method, INamedTypeSymbol requestType) =>
        !WolverineSagaTypes.IsSagaState(requestType, scope.Project) &&
        HasDirectMartenDeletes(scope, method) &&
        WolverineCompoundStages.StagesFor(method, requestType, scope).Count == 0;

    static bool IsDeleteOnlyHandler(DotNetProjectCompilation project, StateChangeShape shape) =>
        !HasEventSourcingShape(shape) && HasNoProducedEvents(shape) && !shape.AppendDiscovery.HasDirectWrite &&
        shape.Aggregate is null && shape.BusConsequences.Count == 0 && shape.OutgoingMessages.Count == 0 &&
        shape.ReturnConsequences.Count == 0 && !shape.Method.Parameters.Any(parameter => IsStateInputParameter(parameter, project));

    static bool HasDirectMartenDeletes(WolverineFactScope scope, IMethodSymbol method)
    {
        var deletes = DocumentDeletes(method, scope.Project).Select(delete => delete.Invocation).ToHashSet();
        return deletes.Count > 0 && WolverineMethodSyntax.Declarations(method, scope.Project).All(declaration =>
            declaration.Declaration.DescendantNodes().OfType<InvocationExpressionSyntax>().All(invocation =>
                declaration.SemanticModel.GetSymbolInfo(invocation).Symbol is not IMethodSymbol called ||
                ClassifyDeleteHandlerCall(called, deletes.Contains(invocation))));
    }

    // Entity and predicate deletes are observed by Marten. Only typed Delete calls produce a Wolverine command edge.
    static bool ClassifyDeleteHandlerCall(IMethodSymbol called, bool hasCommandEdge) =>
        !IsPersistenceNamespace(called) ||
        (MartenDocumentSource.IsMarten(called, includeIntegrationApis: false) &&
         (hasCommandEdge || (called.Name == "Delete" && !called.IsGenericMethod) || called.Name == "DeleteWhere"));

    static void AddNativeDeleteHandlerFacts(
        WolverineFactScope scope,
        DotNetAnalysisContext context,
        NativeDeleteHandler handler,
        WolverineValidationAuthorizationDiscoveryResult validation)
    {
        var (method, requestType) = handler;
        if (!scope.RequireOwnership(method))
        {
            return;
        }

        var project = scope.Project;
        var subject = scope.Subjects.SubjectForType(project, requestType);
        var key = scope.Key(subject, ArtifactKind.Command);
        var evidence = MethodEvidence(method, project, scope.Adapter, EvidenceStrength.Exact, "Wolverine message handler with persistence effects");
        var declaration = NativeCommandDeclaration(scope, key, requestType.Name, evidence);
        if (!scope.Facts.OfType<ArtifactDeclarationFact>().Any(fact => fact.Id == declaration.Id))
        {
            scope.Facts.Add(declaration);
            scope.Facts.AddRange(NativeTypedCommandMembers(context, key, requestType, evidence));
        }

        var placement = CritterStackSourcePlacement.CompatibilityPlacement(project, scope.Options, requestType.Name, requestType.Name, GenerationSliceKind.StateChange);
        scope.Placements.Add(new(scope.PlacementId($"wolverine:placement:command:{subject.Value}"), key, null, placement, evidence));
        scope.Facts.Add(NativeCommandEntryPoint(scope, key, method, evidence));
        scope.Diagnostics.AddRange(validation.ValidationDiagnostics(method, requestType, subject, isHttpEndpoint: false));
        AddStandaloneBoundaryDiagnostics(scope, method);
        AddNativeDocumentDeletes(scope, EmissionFor(scope, subject, evidence), method);
        AddHandlerChainConfigurationDiagnostics(scope, subject, method);
    }

    sealed record NativeDeleteHandler(IMethodSymbol Method, INamedTypeSymbol RequestType);
}
