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
    enum DocumentCallSupport
    {
        Unsupported,
        WithoutEntryPoint,
        WithEntryPoint
    }

    static void EmitNativeDeleteHandler(
        WolverineFactScope scope,
        DotNetAnalysisContext context,
        IMethodSymbol method,
        WolverineValidationAuthorizationDiscoveryResult validation)
    {
        var request = RequestParameter(method, scope.Project);
        if (request?.Type is not INamedTypeSymbol requestType || !IsStandaloneHandler(scope, method, requestType))
        {
            return;
        }

        var support = DeleteHandlerSupport(scope, method);
        if (support == DocumentCallSupport.Unsupported ||
            !HasOnlyDocumentEffects(scope.Project, DiscoverStateChangeShape(method, request, requestType, scope, isHttpEndpoint: false)))
        {
            return;
        }

        var handler = new NativeDeleteHandler(method, requestType, support == DocumentCallSupport.WithEntryPoint);
        AnalyzeWithSlicePattern(scope, method, null, owned => AddNativeDeleteHandlerFacts(owned, context, handler, validation));
    }

    static bool IsStandaloneHandler(WolverineFactScope scope, IMethodSymbol method, INamedTypeSymbol requestType) =>
        !WolverineSagaTypes.IsSagaState(requestType, scope.Project) &&
        WolverineCompoundStages.StagesFor(method, requestType, scope).Count == 0;

    static bool HasOnlyDocumentEffects(DotNetProjectCompilation project, StateChangeShape shape) =>
        !HasEventSourcingShape(shape) && HasNoProducedEvents(shape) && !shape.AppendDiscovery.HasDirectWrite &&
        shape.Aggregate is null && shape.BusConsequences.Count == 0 && shape.OutgoingMessages.Count == 0 &&
        shape.ReturnConsequences.Count == 0 && !shape.Method.Parameters.Any(parameter => IsStateInputParameter(parameter, project));

    static DocumentCallSupport DeleteHandlerSupport(WolverineFactScope scope, IMethodSymbol method)
    {
        var deletes = DocumentDeletes(method, scope.Project).Select(delete => delete.Invocation).ToHashSet();
        if (deletes.Count == 0) return DocumentCallSupport.Unsupported;

        var calls = WolverineMethodSyntax.Declarations(method, scope.Project).SelectMany(declaration =>
            declaration.Declaration.DescendantNodes().OfType<InvocationExpressionSyntax>()
                .Select(invocation => ClassifyDocumentCall(invocation, declaration.SemanticModel, deletes.Contains(invocation))));
        var support = DocumentCallSupport.WithEntryPoint;
        foreach (var call in calls)
        {
            if (call == DocumentCallSupport.Unsupported) return call;
            if (call == DocumentCallSupport.WithoutEntryPoint) support = call;
        }

        return support;
    }

    static DocumentCallSupport ClassifyDocumentCall(InvocationExpressionSyntax invocation, SemanticModel model, bool hasCommandEdge)
    {
        if (model.GetSymbolInfo(invocation).Symbol is not IMethodSymbol called || !IsPersistenceNamespace(called)) return DocumentCallSupport.WithEntryPoint;

        // An entry point would lift the method-level read onto the command, which only has a delete edge on the compatibility path.
        if (IsDocumentSessionRead(called) || IsMartenQueryTerminal(called, invocation, model)) return DocumentCallSupport.WithoutEntryPoint;

        return IsEventByIdLoad(called) || IsSupportedMartenCall(called, invocation, model, hasCommandEdge)
            ? DocumentCallSupport.WithEntryPoint
            : DocumentCallSupport.Unsupported;
    }

    static bool IsDocumentSessionRead(IMethodSymbol called) =>
        MartenSessionReadDiscovery.IsDocumentReadMethod(called.Name) &&
        (string.Equals(DotNetSubjectIds.MetadataName(called.ContainingType), "Marten.IQuerySession", StringComparison.Ordinal) ||
         IsDocumentOperationsContract(called.ContainingType));

    // Loading an event by ID is not a document read or a stream effect in the compatibility producer.
    static bool IsEventByIdLoad(IMethodSymbol called) =>
        called.Name == "LoadAsync" && DotNetSubjectIds.MetadataName(called.ContainingType) == "JasperFx.Events.IQueryEventStore";

    static bool IsMartenQueryTerminal(IMethodSymbol called, InvocationExpressionSyntax invocation, SemanticModel model) =>
        called.ReducedFrom is { } extension && IsQueryTerminal(extension) && IsSessionQueryChain(invocation, model);

    static bool IsQueryTerminal(IMethodSymbol extension) => DotNetSubjectIds.MetadataName(extension.ContainingType) switch
    {
        "Marten.QueryableExtensions" => extension.Name == "ToListAsync",
        "JasperFx.Events.Documents.DocumentQueryableExtensions" => string.Equals(extension.Name, "FirstOrDefaultAsync", StringComparison.Ordinal) ||
            string.Equals(extension.Name, "CountAsync", StringComparison.Ordinal),
        _ => false
    };

    static bool IsSessionQueryChain(InvocationExpressionSyntax invocation, SemanticModel model)
    {
        while (invocation.Expression is MemberAccessExpressionSyntax { Expression: InvocationExpressionSyntax receiver } &&
               model.GetSymbolInfo(receiver).Symbol is IMethodSymbol method)
        {
            if (IsDocumentSessionRead(method)) return method.Name == "Query";
            if (method.ReducedFrom is not { } extension || DotNetSubjectIds.MetadataName(extension.ContainingType) != "System.Linq.Queryable") return false;
            invocation = receiver;
        }

        return false;
    }

    static bool IsSupportedMartenCall(IMethodSymbol called, InvocationExpressionSyntax invocation, SemanticModel model, bool hasCommandEdge) =>
        MartenDocumentSource.IsMarten(called, includeIntegrationApis: false) &&
        (IsSupportedDeleteCall(called, hasCommandEdge) || IsDocumentSessionCall(called, invocation, model));

    static bool IsSupportedDeleteCall(IMethodSymbol called, bool hasCommandEdge) =>
        called.Name == "DeleteWhere" || (called.Name == "Delete" && (hasCommandEdge || !called.IsGenericMethod));

    // Marten owns ordinary writes (including lambdas and generated targets); a session save adds no command edge.
    static bool IsDocumentSessionCall(IMethodSymbol called, InvocationExpressionSyntax invocation, SemanticModel model) =>
        !called.IsStatic && IsDocumentOperationsType(called.ContainingType) &&
        (IsSessionSave(called.Name) || IsDocumentWrite(called, invocation, model));

    static bool IsSessionSave(string name) =>
        string.Equals(name, "SaveChanges", StringComparison.Ordinal) ||
        string.Equals(name, "SaveChangesAsync", StringComparison.Ordinal);

    static bool IsDocumentWrite(IMethodSymbol called, InvocationExpressionSyntax invocation, SemanticModel model) =>
        MartenDocumentSource.RelationshipKindFor(called.Name) is RelationshipKind.Stores or RelationshipKind.Updates &&
        MartenDocumentSource.DocumentTypeFrom(called, invocation, model) is { DeclaringSyntaxReferences.Length: > 0 };

    static bool IsDocumentOperationsType(INamedTypeSymbol type) =>
        IsDocumentOperationsContract(type) || type.AllInterfaces.Any(IsDocumentOperationsContract);

    static bool IsDocumentOperationsContract(INamedTypeSymbol type)
    {
        var name = DotNetSubjectIds.MetadataName(type);
        return string.Equals(name, "Marten.IDocumentOperations", StringComparison.Ordinal) ||
            string.Equals(name, "Marten.IDocumentSession", StringComparison.Ordinal);
    }

    static void AddNativeDeleteHandlerFacts(
        WolverineFactScope scope,
        DotNetAnalysisContext context,
        NativeDeleteHandler handler,
        WolverineValidationAuthorizationDiscoveryResult validation)
    {
        var (method, requestType, emitsEntryPoint) = handler;
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
        if (emitsEntryPoint)
        {
            scope.Facts.Add(NativeCommandEntryPoint(scope, key, method, evidence));
        }

        scope.Diagnostics.AddRange(validation.ValidationDiagnostics(method, requestType, subject, isHttpEndpoint: false));
        AddStandaloneBoundaryDiagnostics(scope, method);
        AddNativeDocumentDeletes(scope, EmissionFor(scope, subject, evidence), method);
        AddHandlerChainConfigurationDiagnostics(scope, subject, method);
    }

    sealed record NativeDeleteHandler(IMethodSymbol Method, INamedTypeSymbol RequestType, bool EmitsEntryPoint);
}
