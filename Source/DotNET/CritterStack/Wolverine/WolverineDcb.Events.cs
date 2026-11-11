// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Cratis.Screenplay.Generation.DotNet;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Operations;

namespace Cratis.CritterStack.Screenplay.Wolverine;

internal static partial class WolverineDcb
{
    static (IReadOnlyList<INamedTypeSymbol> Events, IReadOnlyList<INamedTypeSymbol> ImperativeEvents) DiscoverEvents(
        IMethodSymbol method,
        bool isBoundaryParameter,
        DotNetProjectCompilation project,
        bool isHttpEndpoint)
    {
        var events = new List<INamedTypeSymbol>();
        var imperativeEvents = new List<INamedTypeSymbol>();
        var declarativeEventTypes = DeclarativeEventTypes(method, project, isHttpEndpoint);
        var hasSupportedCollectionReturn = IsSupportedCollectionReturn(method.ReturnType);
        foreach (var (declaration, semanticModel) in WolverineMethodSyntax.Declarations(method, project))
        {
            if (isBoundaryParameter)
            {
                var appended = BoundaryAppends(method, declaration, semanticModel, project).ToArray();
                events.AddRange(appended);
                imperativeEvents.AddRange(appended);
            }

            foreach (var operation in ReturnOperations(declaration, semanticModel))
            {
                events.AddRange(WrapperEvents(operation, project));
                if (!isBoundaryParameter)
                {
                    events.AddRange(OrdinaryEvents(operation, project, hasSupportedCollectionReturn, declarativeEventTypes));
                }
            }
        }

        return (
            [.. events.Distinct(SymbolEqualityComparer.Default).OfType<INamedTypeSymbol>()],
            [.. imperativeEvents.Distinct(SymbolEqualityComparer.Default).OfType<INamedTypeSymbol>()]);
    }

    static ITypeSymbol[] DeclarativeEventTypes(IMethodSymbol method, DotNetProjectCompilation project, bool isHttpEndpoint) =>
        [
            .. WolverineReturnConsequences.Classify(
                    method,
                    project,
                    isHttpEndpoint,
                    aggregateWorkflow: true,
                    hasEventStream: false)
                .Where(_ => _.Kind == WolverineReturnConsequenceKind.PersistedEvent)
                .Select(_ => _.Type)
        ];

    static IEnumerable<IOperation> ReturnOperations(MethodDeclarationSyntax declaration, SemanticModel semanticModel) =>
        DirectReturnExpressions(declaration)
            .Select(expression => semanticModel.GetOperation(expression))
            .OfType<IOperation>();

    static IEnumerable<INamedTypeSymbol> WrapperEvents(IOperation operation, DotNetProjectCompilation project) =>
        TryGetPersistenceWrapperPayloads(operation, project, out var payloads)
            ? payloads.Where(type => IsAuthoredEventPayload(type, project))
            : [];

    static IEnumerable<INamedTypeSymbol> OrdinaryEvents(
        IOperation operation,
        DotNetProjectCompilation project,
        bool hasSupportedCollectionReturn,
        ITypeSymbol[] declarativeEventTypes) =>
        TryGetOrdinaryReturnPayloads(operation, out var payloads)
            ? payloads.Where(type =>
                IsAuthoredEventPayload(type, project) &&
                (hasSupportedCollectionReturn ||
                 declarativeEventTypes.Any(candidate => SymbolEqualityComparer.Default.Equals(candidate, type))))
            : [];

    static IEnumerable<INamedTypeSymbol> BoundaryAppends(
        IMethodSymbol method,
        MethodDeclarationSyntax declaration,
        SemanticModel semanticModel,
        DotNetProjectCompilation project) =>
        declaration.DescendantNodes()
            .OfType<InvocationExpressionSyntax>()
            .Where(syntax => IsInDirectMethodBody(syntax, declaration))
            .Select(syntax => semanticModel.GetOperation(syntax))
            .OfType<IInvocationOperation>()
            .Where(invocation => IsExactBoundaryAppend(invocation, project) && IsOnBoundaryParameter(invocation, method))
            .SelectMany(invocation => AuthoredAppendPayloads(invocation, project));

    static IEnumerable<INamedTypeSymbol> AuthoredAppendPayloads(IInvocationOperation invocation, DotNetProjectCompilation project) =>
        WolverineEventStreams.TryGetDirectPayloads(invocation, out var payloads)
            ? payloads.Where(type => IsAuthoredEventPayload(type, project))
            : [];

    static bool IsOnBoundaryParameter(IInvocationOperation invocation, IMethodSymbol method) =>
        ReceiverParameter(invocation.Instance) is { } receiver &&
        method.Parameters.Any(parameter =>
            SymbolEqualityComparer.Default.Equals(parameter, receiver) &&
            parameter.Type is INamedTypeSymbol boundary &&
            IsEventBoundaryDefinition(boundary));

    static bool IsExactBoundaryAppend(IInvocationOperation invocation, DotNetProjectCompilation project)
    {
        var method = invocation.TargetMethod;
        return (string.Equals(method.Name, "AppendOne", StringComparison.Ordinal) ||
                string.Equals(method.Name, "AppendMany", StringComparison.Ordinal)) &&
               CanonicalEventBoundary(project) is { } boundary &&
               SymbolEqualityComparer.Default.Equals(method.ContainingType.OriginalDefinition, boundary) &&
               IsDeclaredOn(method, boundary);
    }

    static bool TryGetPersistenceWrapperPayloads(
        IOperation operation,
        DotNetProjectCompilation project,
        out IReadOnlyList<INamedTypeSymbol> eventTypes)
    {
        operation = UnwrapReturn(operation)!;
        if (TaskResultArgument(operation) is { } inner)
        {
            return TryGetPersistenceWrapperPayloads(inner, project, out eventTypes);
        }

        switch (operation)
        {
            case ITupleOperation tuple:
                var payloads = tuple.Elements.SelectMany(element => WrapperPayloads(element, project)).ToList();
                eventTypes = payloads;
                return payloads.Count > 0;
            case IObjectCreationOperation creation when IsPersistenceWrapper(creation.Type, project):
                return TryGetWrapperCreationPayloads(creation, out eventTypes);
            case ICollectionExpressionOperation collection when IsPersistenceWrapper(collection.Type, project):
                return WolverineEventStreams.TryGetDirectPayloads(collection, out eventTypes);
            default:
                eventTypes = [];
                return false;
        }
    }

    static IReadOnlyList<INamedTypeSymbol> WrapperPayloads(IOperation operation, DotNetProjectCompilation project) =>
        TryGetPersistenceWrapperPayloads(operation, project, out var payloads) ? payloads : [];

    static bool TryGetWrapperCreationPayloads(IObjectCreationOperation creation, out IReadOnlyList<INamedTypeSymbol> eventTypes)
    {
        var payloads = new List<INamedTypeSymbol>();
        var collected = TryCollectArgumentPayloads(creation.Arguments, payloads, allowEmpty: true) &&
                        TryCollectInitializerPayloads(creation.Initializer, payloads);
        eventTypes = collected ? payloads : [];
        return collected && payloads.Count > 0;
    }

    static bool TryCollectArgumentPayloads(
        IEnumerable<IArgumentOperation> arguments,
        List<INamedTypeSymbol> payloads,
        bool allowEmpty)
    {
        foreach (var argument in arguments)
        {
            if (!WolverineEventStreams.TryGetDirectPayloads(argument.Value, out var argumentPayloads, allowEmpty))
            {
                return false;
            }

            payloads.AddRange(argumentPayloads);
        }

        return true;
    }

    static bool TryCollectInitializerPayloads(IObjectOrCollectionInitializerOperation? initializer, List<INamedTypeSymbol> payloads)
    {
        if (initializer is null)
        {
            return true;
        }

        foreach (var element in initializer.Initializers)
        {
            if (element is not IInvocationOperation { TargetMethod.Name: "Add" } add ||
                add.Arguments.Length == 0 ||
                !TryCollectArgumentPayloads(add.Arguments, payloads, allowEmpty: false))
            {
                return false;
            }
        }

        return true;
    }

    static bool IsPersistenceWrapper(ITypeSymbol? type, DotNetProjectCompilation project)
    {
        if (type is not INamedTypeSymbol named ||
            !_persistenceWrappers.Contains(DotNetSubjectIds.MetadataName(named.OriginalDefinition)))
        {
            return false;
        }

        var canonical = project.Compilation.GetTypeByMetadataName(DotNetSubjectIds.MetadataName(named.OriginalDefinition));
        return canonical is not null &&
               WolverineSymbolAuthority.IsAuthoredOrMetadataSymbol(canonical, project) &&
               SymbolEqualityComparer.Default.Equals(named.OriginalDefinition, canonical);
    }

    static bool TryGetOrdinaryReturnPayloads(
        IOperation operation,
        out IReadOnlyList<INamedTypeSymbol> eventTypes)
    {
        var payloads = new List<INamedTypeSymbol>();
        if (!TryGetOrdinaryReturnPayloads(UnwrapReturn(operation)!, payloads))
        {
            eventTypes = [];
            return false;
        }

        eventTypes = payloads;
        return payloads.Count > 0;
    }

    static bool TryGetOrdinaryReturnPayloads(IOperation operation, List<INamedTypeSymbol> eventTypes)
    {
        operation = UnwrapReturn(operation)!;
        if (WolverineEventStreams.TryGetDirectPayloads(operation, out var directPayloads))
        {
            eventTypes.AddRange(directPayloads);
            return directPayloads.Count > 0;
        }

        return operation switch
        {
            IObjectCreationOperation { Type: INamedTypeSymbol payload } when IsOrdinaryPayload(payload) => AddPayload(eventTypes, payload),
            ITupleOperation tuple => TryGetTupleOrdinaryPayloads(tuple, eventTypes),
            IInvocationOperation invocation when TaskFromResultArgument(invocation) is { } inner => TryGetOrdinaryReturnPayloads(inner, eventTypes),
            _ => false
        };
    }

    static bool AddPayload(List<INamedTypeSymbol> eventTypes, INamedTypeSymbol payload)
    {
        eventTypes.Add(payload);
        return true;
    }

    static bool TryGetTupleOrdinaryPayloads(ITupleOperation tuple, List<INamedTypeSymbol> eventTypes)
    {
        var found = false;
        foreach (var element in tuple.Elements)
        {
            found |= TryGetOrdinaryReturnPayloads(element, eventTypes);
        }

        return found;
    }

    static bool IsSupportedCollectionReturn(ITypeSymbol returnType)
    {
        var unwrapped = WolverineReturnTypes.UnwrapTask(returnType);
        if (unwrapped is IArrayTypeSymbol { ElementType.SpecialType: SpecialType.System_Object })
        {
            return true;
        }

        return unwrapped is INamedTypeSymbol named && named.AllInterfaces.Concat([named]).Any(IsObjectEnumerable);
    }

    static bool IsObjectEnumerable(INamedTypeSymbol candidate) =>
        candidate.IsGenericType &&
        DotNetSubjectIds.MetadataName(candidate.OriginalDefinition) == "System.Collections.Generic.IEnumerable`1" &&
        candidate.TypeArguments[0].SpecialType == SpecialType.System_Object;

    static bool IsTaskFactoryType(INamedTypeSymbol type)
    {
        var metadataName = DotNetSubjectIds.MetadataName(type);
        return string.Equals(metadataName, "System.Threading.Tasks.Task", StringComparison.Ordinal) ||
               string.Equals(metadataName, "System.Threading.Tasks.ValueTask", StringComparison.Ordinal);
    }

    static bool IsOrdinaryPayload(INamedTypeSymbol type) =>
        type.SpecialType == SpecialType.None &&
        !WolverineReturnTypes.IsSpecialReturn(type) &&
        !DotNetSubjectIds.MetadataName(type.OriginalDefinition).StartsWith("System.", StringComparison.Ordinal);

    static bool IsAuthoredEventPayload(INamedTypeSymbol type, DotNetProjectCompilation project) =>
        IsOrdinaryPayload(type) &&
        IsAuthoredSourceType(type, project) &&
        !WolverineSagaTypes.IsSagaState(type, project);

    static IEnumerable<ExpressionSyntax> DirectReturnExpressions(MethodDeclarationSyntax declaration)
    {
        if (declaration.ExpressionBody is not null)
        {
            yield return declaration.ExpressionBody.Expression;
        }

        if (declaration.Body is null)
        {
            yield break;
        }

        var returned = declaration.Body.DescendantNodes()
            .OfType<ReturnStatementSyntax>()
            .Where(statement => IsInDirectMethodBody(statement, declaration))
            .Select(statement => statement.Expression)
            .OfType<ExpressionSyntax>();
        foreach (var expression in returned)
        {
            yield return expression;
        }
    }
}
