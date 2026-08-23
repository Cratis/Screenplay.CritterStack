// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Cratis.Screenplay.Generation;
using Cratis.Screenplay.Generation.DotNet;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Operations;

namespace Cratis.CritterStack.Screenplay.Wolverine;

sealed record WolverineDcbCondition(
    int Ordinal,
    INamedTypeSymbol TagType,
    INamedTypeSymbol? EventType,
    string? SourceMember,
    SourceRange? Source);

sealed record WolverineDcbDiscovery(
    IParameterSymbol Parameter,
    INamedTypeSymbol ModelType,
    bool IsBoundaryParameter,
    IMethodSymbol Companion,
    IReadOnlyList<WolverineDcbCondition> Conditions,
    bool QueryResolved,
    IReadOnlyList<INamedTypeSymbol> EventTypes,
    IReadOnlyList<INamedTypeSymbol> ImperativeEventTypes,
    IReadOnlyList<INamedTypeSymbol> QueryEventTypes,
    string? SourceMember,
    SourceRange? Source,
    SourceRange? QuerySource)
{
    public string Discriminator => $"wolverine:dcb:{Parameter.Ordinal}:{Parameter.Name}";
}

static class WolverineDcb
{
    static readonly string[] _companionNames = ["Before", "BeforeAsync", "Load", "LoadAsync"];

    static readonly HashSet<string> _persistenceWrappers =
    [
        WellKnownTypes.WolverineEvents,
        WellKnownTypes.WolverineEventsToAppend
    ];

    public static WolverineDcbDiscovery? Discover(
        IMethodSymbol method,
        IParameterSymbol? request,
        DotNetProjectCompilation project,
        bool isHttpEndpoint)
    {
        var attributed = method.Parameters
            .Select(parameter => (Parameter: parameter, Attribute: DcbAttribute(parameter, project)))
            .Where(_ => _.Attribute is not null)
            .ToArray();
        if (attributed.Length != 1)
        {
            return null;
        }

        var (parameter, attribute) = attributed[0];
        if (!TryGetModel(parameter.Type, project, out var modelType, out var isBoundaryParameter))
        {
            return null;
        }

        var companion = Companions(method, request, project).FirstOrDefault();
        if (companion is null)
        {
            return null;
        }

        var query = ParseQuery(companion, request, method, project);
        var (eventTypes, imperativeEventTypes) = DiscoverEvents(method, isBoundaryParameter, project, isHttpEndpoint);
        var queryEventTypes = query.Conditions
            .Select(_ => _.EventType)
            .Where(type => type is not null && IsAuthoredEventPayload(type, project))
            .Distinct(SymbolEqualityComparer.Default)
            .OfType<INamedTypeSymbol>()
            .ToArray();
        return new(
            parameter,
            modelType,
            isBoundaryParameter,
            companion,
            query.Conditions,
            query.Resolved,
            eventTypes,
            imperativeEventTypes,
            queryEventTypes,
            query.SourceMember,
            SourceOf(attribute, parameter, project),
            query.Source ?? SourceOf(companion, project));
    }

    public static bool HasAttributedParameter(IParameterSymbol parameter, DotNetProjectCompilation project) =>
        DcbAttribute(parameter, project) is not null;

    static AttributeData? DcbAttribute(IParameterSymbol parameter, DotNetProjectCompilation project) =>
        parameter.GetAttributes().FirstOrDefault(attribute =>
            attribute.AttributeClass is not null &&
            IsAdmittedAttribute(attribute.AttributeClass, project) &&
            attribute.ApplicationSyntaxReference?.SyntaxTree is { } tree &&
            project.AuthoredSyntaxTrees.Contains(tree) &&
            !DotNetGeneratedSource.IsGenerated(tree));

    static bool IsAdmittedAttribute(INamedTypeSymbol attributeType, DotNetProjectCompilation project)
    {
        if (project.Compilation.GetTypeByMetadataName(WellKnownTypes.WolverineDcbModelAttribute) is { } current &&
            IsAuthoredOrMetadataSymbol(current, project) &&
            DotNetSymbols.IsOrInheritsFrom(attributeType, WellKnownTypes.WolverineDcbModelAttribute) &&
            IsAuthoredOrMetadataInheritance(attributeType, WellKnownTypes.WolverineDcbModelAttribute, project))
        {
            return true;
        }

        return project.Compilation.GetTypeByMetadataName(WellKnownTypes.WolverineLegacyBoundaryModelAttribute) is { } legacy &&
               IsAuthoredOrMetadataSymbol(legacy, project) &&
               DotNetSubjectIds.MetadataName(attributeType.OriginalDefinition) == WellKnownTypes.WolverineLegacyBoundaryModelAttribute;
    }

    static bool IsAuthoredOrMetadataInheritance(
        INamedTypeSymbol attributeType,
        string admittedMetadataName,
        DotNetProjectCompilation project) => IsAuthoredOrMetadataInheritance(
            attributeType,
            admittedMetadataName,
            project,
            new HashSet<INamedTypeSymbol>(SymbolEqualityComparer.Default));

    static bool IsAuthoredOrMetadataInheritance(
        INamedTypeSymbol attributeType,
        string admittedMetadataName,
        DotNetProjectCompilation project,
        HashSet<INamedTypeSymbol> visited)
    {
        if (!visited.Add(attributeType))
        {
            return false;
        }

        if (DotNetSubjectIds.MetadataName(attributeType.OriginalDefinition) == admittedMetadataName)
        {
            return true;
        }

        if (attributeType.DeclaringSyntaxReferences.Length == 0)
        {
            return attributeType.BaseType is not null &&
                   IsAuthoredOrMetadataInheritance(attributeType.BaseType, admittedMetadataName, project, visited);
        }

        foreach (var syntaxReference in attributeType.DeclaringSyntaxReferences)
        {
            if (syntaxReference.GetSyntax() is not TypeDeclarationSyntax { BaseList: not null } declaration ||
                !project.AuthoredSyntaxTrees.Contains(declaration.SyntaxTree) ||
                DotNetGeneratedSource.IsGenerated(declaration.SyntaxTree))
            {
                continue;
            }

            var semanticModel = project.Compilation.GetSemanticModel(declaration.SyntaxTree);
            if (declaration.BaseList.Types.Any(baseType =>
                    semanticModel.GetTypeInfo(baseType.Type).Type is INamedTypeSymbol candidate &&
                    IsAuthoredOrMetadataInheritance(candidate, admittedMetadataName, project, visited)))
            {
                return true;
            }
        }

        return false;
    }

    static bool TryGetModel(
        ITypeSymbol parameterType,
        DotNetProjectCompilation project,
        out INamedTypeSymbol modelType,
        out bool isBoundaryParameter)
    {
        isBoundaryParameter = false;
        if (parameterType is INamedTypeSymbol boundary &&
            boundary.IsGenericType &&
            DotNetSubjectIds.MetadataName(boundary.OriginalDefinition) == WellKnownTypes.JasperFxEventBoundary)
        {
            if (project.Compilation.GetTypeByMetadataName(WellKnownTypes.JasperFxEventBoundary) is not { } canonicalBoundary ||
                !IsAuthoredOrMetadataSymbol(canonicalBoundary, project) ||
                !SymbolEqualityComparer.Default.Equals(boundary.OriginalDefinition, canonicalBoundary) ||
                boundary.TypeArguments[0] is not INamedTypeSymbol boundaryModel ||
                !IsAuthoredSourceType(boundaryModel, project))
            {
                modelType = null!;
                return false;
            }

            modelType = boundaryModel;
            isBoundaryParameter = true;
            return true;
        }

        if (parameterType is INamedTypeSymbol assignableBoundary &&
            assignableBoundary.AllInterfaces.Any(@interface =>
                DotNetSubjectIds.MetadataName(@interface.OriginalDefinition) == WellKnownTypes.JasperFxEventBoundary))
        {
            modelType = null!;
            return false;
        }

        var candidate = parameterType as INamedTypeSymbol;
        if (candidate?.OriginalDefinition.SpecialType == SpecialType.System_Nullable_T)
        {
            candidate = candidate.TypeArguments[0] as INamedTypeSymbol;
        }

        if (candidate is null ||
            candidate.SpecialType != SpecialType.None ||
            DotNetSubjectIds.MetadataName(candidate.OriginalDefinition).StartsWith("System.", StringComparison.Ordinal) ||
            !IsAuthoredSourceType(candidate, project))
        {
            modelType = null!;
            return false;
        }

        modelType = candidate;
        return true;
    }

    static IEnumerable<IMethodSymbol> Companions(
        IMethodSymbol handler,
        IParameterSymbol? request,
        DotNetProjectCompilation project) =>
        handler.ContainingType.GetMembers()
            .OfType<IMethodSymbol>()
            .Where(method =>
                method.DeclaredAccessibility == Accessibility.Public &&
                method.MethodKind == MethodKind.Ordinary &&
                _companionNames.Contains(method.Name, StringComparer.Ordinal) &&
                IsExactQueryReturn(method.ReturnType) &&
                ParametersCorrelate(method, handler, request) &&
                WolverineMethodSyntax.Declarations(method, project).Any())
            .OrderBy(method => SourcePathOf(method, project), StringComparer.Ordinal)
            .ThenBy(method => SourcePositionOf(method, project));

    static bool ParametersCorrelate(
        IMethodSymbol companion,
        IMethodSymbol handler,
        IParameterSymbol? request)
    {
        if (request is null)
        {
            return false;
        }

        var companionRequests = companion.Parameters
            .Where(parameter => SymbolEqualityComparer.Default.Equals(parameter.Type, request.Type))
            .ToArray();
        if (companionRequests.Length != 1)
        {
            return false;
        }

        var handlerRequests = handler.Parameters
            .Where(parameter => SymbolEqualityComparer.Default.Equals(parameter.Type, request.Type))
            .ToArray();
        return handlerRequests.Length == 1 ||
               string.Equals(companionRequests[0].Name, request.Name, StringComparison.Ordinal);
    }

    static bool IsExactQueryReturn(ITypeSymbol returnType)
    {
        if (returnType is not INamedTypeSymbol named)
        {
            return false;
        }

        var metadataName = DotNetSubjectIds.MetadataName(named.OriginalDefinition);
        if (metadataName == WellKnownTypes.JasperFxEventTagQuery)
        {
            return true;
        }

        return (string.Equals(metadataName, "System.Threading.Tasks.Task`1", StringComparison.Ordinal) ||
                string.Equals(metadataName, "System.Threading.Tasks.ValueTask`1", StringComparison.Ordinal)) &&
               named.TypeArguments[0] is INamedTypeSymbol result &&
               DotNetSubjectIds.MetadataName(result.OriginalDefinition) == WellKnownTypes.JasperFxEventTagQuery;
    }

    static (IReadOnlyList<WolverineDcbCondition> Conditions, bool Resolved, string? SourceMember, SourceRange? Source) ParseQuery(
        IMethodSymbol companion,
        IParameterSymbol? request,
        IMethodSymbol handler,
        DotNetProjectCompilation project)
    {
        foreach (var (declaration, semanticModel) in WolverineMethodSyntax.Declarations(companion, project))
        {
            var expression = QueryExpression(declaration);
            if (expression is null)
            {
                return ([], false, null, CritterStackSource.RangeForProject(declaration.GetLocation(), project));
            }

            var operation = semanticModel.GetOperation(expression);
            operation = UnwrapQueryResult(operation);
            var source = CritterStackSource.RangeForProject(expression.GetLocation(), project);
            if (operation is null || !TryParseChain(operation, semanticModel, request, handler, project, out var conditions, out var sourceMember))
            {
                return ([], false, null, source);
            }

            return (conditions, true, sourceMember, source);
        }

        return ([], false, null, SourceOf(companion, project));
    }

    static ExpressionSyntax? QueryExpression(MethodDeclarationSyntax declaration)
    {
        if (declaration.ExpressionBody is not null)
        {
            return declaration.ExpressionBody.Expression;
        }

        if (declaration.Body is null)
        {
            return null;
        }

        if (declaration.Body.Statements is [ReturnStatementSyntax { Expression: not null } direct])
        {
            return direct.Expression;
        }

        if (declaration.Body.Statements.Count == 2 &&
            declaration.Body.Statements[0] is LocalDeclarationStatementSyntax localDeclaration &&
            localDeclaration.Declaration.Variables.Count == 1 &&
            localDeclaration.Declaration.Variables[0] is { Initializer.Value: var initializer } local &&
            declaration.Body.Statements[1] is ReturnStatementSyntax { Expression: not null } returned &&
            IsUnmodifiedLocalReturn(returned.Expression, local.Identifier.ValueText))
        {
            return initializer;
        }

        return null;
    }

    static bool IsUnmodifiedLocalReturn(ExpressionSyntax expression, string localName) => expression switch
    {
        IdentifierNameSyntax identifier => identifier.Identifier.ValueText == localName,
        InvocationExpressionSyntax
        {
            Expression: MemberAccessExpressionSyntax
            {
                Expression: var factory,
                Name.Identifier.ValueText: "FromResult"
            },
            ArgumentList.Arguments: [{ Expression: IdentifierNameSyntax returned }]
        } => IsTaskFactoryExpression(factory) && returned.Identifier.ValueText == localName,
        ObjectCreationExpressionSyntax
        {
            Type: var type,
            ArgumentList.Arguments: [{ Expression: IdentifierNameSyntax returned }]
        } => IsValueTaskType(type) && returned.Identifier.ValueText == localName,
        _ => false
    };

    static bool IsTaskFactoryExpression(ExpressionSyntax expression)
    {
        var name = expression.ToString();
        return string.Equals(name, "Task", StringComparison.Ordinal) ||
               string.Equals(name, "ValueTask", StringComparison.Ordinal) ||
               string.Equals(name, "System.Threading.Tasks.Task", StringComparison.Ordinal) ||
               string.Equals(name, "System.Threading.Tasks.ValueTask", StringComparison.Ordinal) ||
               string.Equals(name, "global::System.Threading.Tasks.Task", StringComparison.Ordinal) ||
               string.Equals(name, "global::System.Threading.Tasks.ValueTask", StringComparison.Ordinal);
    }

    static bool IsValueTaskType(TypeSyntax type)
    {
        var name = type.ToString();
        return name.StartsWith("ValueTask<", StringComparison.Ordinal) ||
               name.StartsWith("System.Threading.Tasks.ValueTask<", StringComparison.Ordinal) ||
               name.StartsWith("global::System.Threading.Tasks.ValueTask<", StringComparison.Ordinal);
    }

    static IOperation? UnwrapQueryResult(IOperation? operation)
    {
        operation = Unwrap(operation);
        if (operation is IInvocationOperation invocation &&
            invocation.TargetMethod.IsStatic &&
            invocation.TargetMethod.Name == "FromResult" &&
            IsTaskFactoryType(invocation.TargetMethod.ContainingType) &&
            invocation.Arguments.Length == 1)
        {
            return Unwrap(invocation.Arguments[0].Value);
        }

        if (operation is IObjectCreationOperation creation &&
            creation.Type is INamedTypeSymbol valueTask &&
            DotNetSubjectIds.MetadataName(valueTask.OriginalDefinition) == "System.Threading.Tasks.ValueTask`1" &&
            creation.Arguments.Length == 1)
        {
            return Unwrap(creation.Arguments[0].Value);
        }

        return operation;
    }

    static bool TryParseChain(
        IOperation operation,
        SemanticModel semanticModel,
        IParameterSymbol? request,
        IMethodSymbol handler,
        DotNetProjectCompilation project,
        out IReadOnlyList<WolverineDcbCondition> conditions,
        out string? sourceMember)
    {
        sourceMember = null;
        var calls = new List<IInvocationOperation>();
        var current = Unwrap(operation);
        while (current is IInvocationOperation invocation)
        {
            calls.Add(invocation);
            current = Unwrap(invocation.Instance);
            if (current is null)
            {
                break;
            }
        }
        calls.Reverse();

        if (calls.Count == 0 && current is not IObjectCreationOperation)
        {
            conditions = [];
            return false;
        }

        if (current is IObjectCreationOperation creation && !IsExactQueryType(creation.Type, project))
        {
            conditions = [];
            return false;
        }

        var parsed = new List<MutableCondition>();
        var sourceMembers = new List<string?>();
        TagContext? context = null;
        var started = current is IObjectCreationOperation;
        foreach (var call in calls)
        {
            if (!IsExactQueryMethod(call.TargetMethod, project))
            {
                conditions = [];
                return false;
            }

            switch (call.TargetMethod.Name)
            {
                case "For" when !started && call.TargetMethod.IsStatic && call.TargetMethod.TypeArguments.Length == 1 && call.Arguments.Length == 1:
                    if (call.TargetMethod.TypeArguments[0] is not INamedTypeSymbol forTag)
                    {
                        conditions = [];
                        return false;
                    }
                    var forSourceMember = SourceMember(call.Arguments[0].Value, request, handler, project);
                    sourceMembers.Add(forSourceMember);
                    context = new(forTag, forSourceMember, null);
                    started = true;
                    break;
                case "Or" when started && !call.TargetMethod.IsStatic && call.Arguments.Length == 1 && call.TargetMethod.TypeArguments.Length == 1:
                    if (call.TargetMethod.TypeArguments[0] is not INamedTypeSymbol tagType)
                    {
                        conditions = [];
                        return false;
                    }
                    var tagOnly = new MutableCondition(
                        tagType,
                        null,
                        SourceMember(call.Arguments[0].Value, request, handler, project),
                        SourceOf(call, semanticModel, project));
                    parsed.Add(tagOnly);
                    sourceMembers.Add(tagOnly.SourceMember);
                    context = new(tagType, tagOnly.SourceMember, tagOnly);
                    break;
                case "Or" when started && !call.TargetMethod.IsStatic && call.Arguments.Length == 1 && call.TargetMethod.TypeArguments.Length == 2:
                    if (call.TargetMethod.TypeArguments[0] is not INamedTypeSymbol eventType ||
                        call.TargetMethod.TypeArguments[1] is not INamedTypeSymbol eventTagType)
                    {
                        conditions = [];
                        return false;
                    }
                    var eventCondition = new MutableCondition(
                        eventTagType,
                        eventType,
                        SourceMember(call.Arguments[0].Value, request, handler, project),
                        SourceOf(call, semanticModel, project));
                    parsed.Add(eventCondition);
                    sourceMembers.Add(eventCondition.SourceMember);
                    context = new(eventTagType, eventCondition.SourceMember, null);
                    break;
                case "AndEventsOfType" when started && !call.TargetMethod.IsStatic && call.Arguments.Length == 0 && call.TargetMethod.TypeArguments.Length is >= 1 and <= 6 && context is not null:
                    if (context.TagOnlyCondition is not null)
                    {
                        parsed.Remove(context.TagOnlyCondition);
                        context = context with { TagOnlyCondition = null };
                    }
                    foreach (var typeArgument in call.TargetMethod.TypeArguments)
                    {
                        if (typeArgument is not INamedTypeSymbol andEventType)
                        {
                            conditions = [];
                            return false;
                        }
                        parsed.Add(new(
                            context.TagType,
                            andEventType,
                            context.SourceMember,
                            SourceOf(call, semanticModel, project)));
                    }
                    break;
                default:
                    conditions = [];
                    return false;
            }
        }

        if (!started)
        {
            conditions = [];
            return false;
        }

        sourceMember = sourceMembers.Count > 0 &&
                       sourceMembers[0] is not null &&
                       sourceMembers.TrueForAll(candidate => string.Equals(candidate, sourceMembers[0], StringComparison.Ordinal))
            ? sourceMembers[0]
            : null;
        conditions =
        [
            .. parsed.Select((condition, ordinal) => new WolverineDcbCondition(
                ordinal,
                condition.TagType,
                condition.EventType,
                condition.SourceMember,
                condition.Source))
        ];
        return true;
    }

    static bool IsExactQueryMethod(IMethodSymbol method, DotNetProjectCompilation project) =>
        IsExactQueryType(method.ContainingType, project) &&
        method.ContainingType.GetMembers(method.Name).OfType<IMethodSymbol>().Any(candidate =>
            SymbolEqualityComparer.Default.Equals(candidate.OriginalDefinition, method.OriginalDefinition));

    static bool IsExactQueryType(ITypeSymbol? type, DotNetProjectCompilation project) =>
        type is INamedTypeSymbol named &&
        project.Compilation.GetTypeByMetadataName(WellKnownTypes.JasperFxEventTagQuery) is { } canonical &&
        IsAuthoredOrMetadataSymbol(canonical, project) &&
        SymbolEqualityComparer.Default.Equals(named.OriginalDefinition, canonical);

    static string? SourceMember(
        IOperation operation,
        IParameterSymbol? request,
        IMethodSymbol handler,
        DotNetProjectCompilation project)
    {
        operation = Unwrap(operation)!;
        if (request is not null &&
            operation is IPropertyReferenceOperation property &&
            IsAuthoredOrMetadataSymbol(property.Property, project) &&
            Unwrap(property.Instance) is IParameterReferenceOperation requestReference &&
            SymbolEqualityComparer.Default.Equals(requestReference.Parameter.Type, request.Type))
        {
            return LowerFirst(property.Property.Name);
        }

        if (operation is IParameterReferenceOperation parameterReference &&
            handler.Parameters.Any(parameter =>
                !HasAttributedParameter(parameter, project) &&
                parameter.Name == parameterReference.Parameter.Name &&
                SymbolEqualityComparer.Default.Equals(parameter.Type, parameterReference.Parameter.Type)))
        {
            return LowerFirst(parameterReference.Parameter.Name);
        }

        return null;
    }

    static (IReadOnlyList<INamedTypeSymbol> Events, IReadOnlyList<INamedTypeSymbol> ImperativeEvents) DiscoverEvents(
        IMethodSymbol method,
        bool isBoundaryParameter,
        DotNetProjectCompilation project,
        bool isHttpEndpoint)
    {
        var events = new List<INamedTypeSymbol>();
        var imperativeEvents = new List<INamedTypeSymbol>();
        var declarativeEventTypes = WolverineReturnConsequences.Classify(
                method,
                project,
                isHttpEndpoint,
                aggregateWorkflow: true,
                hasEventStream: false)
            .Where(_ => _.Kind == WolverineReturnConsequenceKind.PersistedEvent)
            .Select(_ => _.Type)
            .ToArray();
        var hasSupportedCollectionReturn = IsSupportedCollectionReturn(method.ReturnType);
        foreach (var (declaration, semanticModel) in WolverineMethodSyntax.Declarations(method, project))
        {
            if (isBoundaryParameter)
            {
                DiscoverBoundaryAppends(method, declaration, semanticModel, project, events, imperativeEvents);
            }

            foreach (var expression in DirectReturnExpressions(declaration))
            {
                if (semanticModel.GetOperation(expression) is not { } operation)
                {
                    continue;
                }

                if (TryGetPersistenceWrapperPayloads(operation, project, out var wrapperEvents))
                {
                    events.AddRange(wrapperEvents.Where(type => IsAuthoredEventPayload(type, project)));
                }

                if (!isBoundaryParameter && TryGetOrdinaryReturnPayloads(operation, out var ordinaryEvents))
                {
                    events.AddRange(ordinaryEvents.Where(type =>
                        IsAuthoredEventPayload(type, project) &&
                        (hasSupportedCollectionReturn ||
                         declarativeEventTypes.Any(candidate => SymbolEqualityComparer.Default.Equals(candidate, type)))));
                }
            }
        }

        return (
            [.. events.Distinct(SymbolEqualityComparer.Default).OfType<INamedTypeSymbol>()],
            [.. imperativeEvents.Distinct(SymbolEqualityComparer.Default).OfType<INamedTypeSymbol>()]);
    }

    static void DiscoverBoundaryAppends(
        IMethodSymbol method,
        MethodDeclarationSyntax declaration,
        SemanticModel semanticModel,
        DotNetProjectCompilation project,
        List<INamedTypeSymbol> events,
        List<INamedTypeSymbol> imperativeEvents)
    {
        foreach (var invocationSyntax in declaration.DescendantNodes().OfType<InvocationExpressionSyntax>())
        {
            if (!IsInDirectMethodBody(invocationSyntax, declaration) ||
                semanticModel.GetOperation(invocationSyntax) is not IInvocationOperation invocation ||
                !IsExactBoundaryAppend(invocation, project) ||
                ReceiverParameter(invocation.Instance) is not { } receiver ||
                !method.Parameters.Any(parameter =>
                    SymbolEqualityComparer.Default.Equals(parameter, receiver) &&
                    parameter.Type is INamedTypeSymbol boundary &&
                    DotNetSubjectIds.MetadataName(boundary.OriginalDefinition) == WellKnownTypes.JasperFxEventBoundary) ||
                !WolverineEventStreams.TryGetDirectPayloads(invocation, out var payloads))
            {
                continue;
            }

            var authoredPayloads = payloads.Where(type => IsAuthoredEventPayload(type, project)).ToArray();
            events.AddRange(authoredPayloads);
            imperativeEvents.AddRange(authoredPayloads);
        }
    }

    static bool IsExactBoundaryAppend(IInvocationOperation invocation, DotNetProjectCompilation project)
    {
        var method = invocation.TargetMethod;
        if (method.Name is not ("AppendOne" or "AppendMany") ||
            project.Compilation.GetTypeByMetadataName(WellKnownTypes.JasperFxEventBoundary) is not { } boundary ||
            !IsAuthoredOrMetadataSymbol(boundary, project) ||
            !SymbolEqualityComparer.Default.Equals(method.ContainingType.OriginalDefinition, boundary))
        {
            return false;
        }

        return boundary.GetMembers(method.Name).OfType<IMethodSymbol>().Any(candidate =>
            SymbolEqualityComparer.Default.Equals(candidate.OriginalDefinition, method.OriginalDefinition));
    }

    static bool TryGetPersistenceWrapperPayloads(
        IOperation operation,
        DotNetProjectCompilation project,
        out IReadOnlyList<INamedTypeSymbol> eventTypes)
    {
        operation = UnwrapReturn(operation)!;
        if (operation is IInvocationOperation taskFactory &&
            taskFactory.TargetMethod.IsStatic &&
            string.Equals(taskFactory.TargetMethod.Name, "FromResult", StringComparison.Ordinal) &&
            IsTaskFactoryType(taskFactory.TargetMethod.ContainingType) &&
            taskFactory.Arguments.Length == 1)
        {
            return TryGetPersistenceWrapperPayloads(taskFactory.Arguments[0].Value, project, out eventTypes);
        }

        if (operation is IObjectCreationOperation
            {
                Type: INamedTypeSymbol valueTask,
                Arguments.Length: 1
            } valueTaskCreation &&
            DotNetSubjectIds.MetadataName(valueTask.OriginalDefinition) == "System.Threading.Tasks.ValueTask`1")
        {
            return TryGetPersistenceWrapperPayloads(valueTaskCreation.Arguments[0].Value, project, out eventTypes);
        }

        if (operation is ITupleOperation tuple)
        {
            var payloads = new List<INamedTypeSymbol>();
            foreach (var element in tuple.Elements)
            {
                if (TryGetPersistenceWrapperPayloads(element, project, out var elementPayloads))
                {
                    payloads.AddRange(elementPayloads);
                }
            }

            eventTypes = payloads;
            return payloads.Count > 0;
        }

        if (operation is IObjectCreationOperation creation && IsPersistenceWrapper(creation.Type, project))
        {
            var payloads = new List<INamedTypeSymbol>();
            foreach (var argument in creation.Arguments)
            {
                if (!WolverineEventStreams.TryGetDirectPayloads(argument.Value, out var argumentPayloads, allowEmpty: true))
                {
                    eventTypes = [];
                    return false;
                }
                payloads.AddRange(argumentPayloads);
            }

            if (creation.Initializer is not null)
            {
                foreach (var initializer in creation.Initializer.Initializers)
                {
                    if (initializer is not IInvocationOperation { TargetMethod.Name: "Add" } add || add.Arguments.Length == 0)
                    {
                        eventTypes = [];
                        return false;
                    }

                    foreach (var argument in add.Arguments)
                    {
                        if (!WolverineEventStreams.TryGetDirectPayloads(argument.Value, out var initializedPayloads))
                        {
                            eventTypes = [];
                            return false;
                        }
                        payloads.AddRange(initializedPayloads);
                    }
                }
            }

            eventTypes = payloads;
            return payloads.Count > 0;
        }

        if (operation is ICollectionExpressionOperation collection &&
            IsPersistenceWrapper(collection.Type, project))
        {
            return WolverineEventStreams.TryGetDirectPayloads(collection, out eventTypes);
        }

        eventTypes = [];
        return false;
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
               IsAuthoredOrMetadataSymbol(canonical, project) &&
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

        if (operation is IObjectCreationOperation creation &&
            creation.Type is INamedTypeSymbol payload &&
            IsOrdinaryPayload(payload))
        {
            eventTypes.Add(payload);
            return true;
        }

        if (operation is ITupleOperation tuple)
        {
            var found = false;
            foreach (var element in tuple.Elements)
            {
                var elementEvents = new List<INamedTypeSymbol>();
                if (TryGetOrdinaryReturnPayloads(element, elementEvents))
                {
                    eventTypes.AddRange(elementEvents);
                    found = true;
                }
            }
            return found;
        }

        if (operation is IInvocationOperation invocation &&
            invocation.TargetMethod.IsStatic &&
            invocation.TargetMethod.Name == "FromResult" &&
            IsTaskFactoryType(invocation.TargetMethod.ContainingType) &&
            invocation.Arguments.Length == 1)
        {
            return TryGetOrdinaryReturnPayloads(invocation.Arguments[0].Value, eventTypes);
        }

        return false;
    }

    static bool IsSupportedCollectionReturn(ITypeSymbol returnType)
    {
        var unwrapped = WolverineReturnTypes.UnwrapTask(returnType);
        if (unwrapped is IArrayTypeSymbol { ElementType.SpecialType: SpecialType.System_Object })
        {
            return true;
        }

        return unwrapped is INamedTypeSymbol named && named.AllInterfaces.Concat([named]).Any(candidate =>
            candidate.IsGenericType &&
            DotNetSubjectIds.MetadataName(candidate.OriginalDefinition) == "System.Collections.Generic.IEnumerable`1" &&
            candidate.TypeArguments[0].SpecialType == SpecialType.System_Object);
    }

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
        IsOrdinaryPayload(type) && IsAuthoredSourceType(type, project);

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

        foreach (var statement in declaration.Body.DescendantNodes().OfType<ReturnStatementSyntax>())
        {
            if (statement.Expression is not null &&
                !statement.Ancestors().TakeWhile(_ => _ != declaration).Any(_ => _ is LocalFunctionStatementSyntax or AnonymousFunctionExpressionSyntax))
            {
                yield return statement.Expression;
            }
        }
    }

    static IParameterSymbol? ReceiverParameter(IOperation? receiver)
    {
        var current = receiver;
        while (current is not null)
        {
            switch (current)
            {
                case IConversionOperation conversion when
                    !conversion.Conversion.IsUserDefined &&
                    (conversion.Conversion.IsIdentity || conversion.Conversion.IsReference) &&
                    conversion.Type?.SpecialType != SpecialType.System_Object &&
                    conversion.Operand.Type?.SpecialType != SpecialType.System_Object &&
                    conversion.Operand.Type?.TypeKind != TypeKind.Dynamic:
                    current = conversion.Operand;
                    continue;
                case IParenthesizedOperation parenthesized:
                    current = parenthesized.Operand;
                    continue;
                case IParameterReferenceOperation parameter:
                    return parameter.Parameter;
                default:
                    return null;
            }
        }

        return null;
    }

    static IOperation? Unwrap(IOperation? operation)
    {
        var current = operation;
        while (current is not null)
        {
            switch (current)
            {
                case IConversionOperation conversion when !conversion.Conversion.IsUserDefined:
                    current = conversion.Operand;
                    continue;
                case IParenthesizedOperation parenthesized:
                    current = parenthesized.Operand;
                    continue;
                default:
                    return current;
            }
        }

        return null;
    }

    static IOperation? UnwrapReturn(IOperation? operation)
    {
        var current = Unwrap(operation);
        while (current is IAwaitOperation awaitOperation)
        {
            current = Unwrap(awaitOperation.Operation);
        }
        return current;
    }

    static bool IsInDirectMethodBody(InvocationExpressionSyntax invocation, MethodDeclarationSyntax declaration) =>
        !invocation.Ancestors()
            .TakeWhile(_ => _ != declaration)
            .Any(_ => _ is LocalFunctionStatementSyntax or AnonymousFunctionExpressionSyntax);

    static bool IsAuthoredSourceType(INamedTypeSymbol type, DotNetProjectCompilation project) => type.Locations.Any(location =>
        location.IsInSource &&
        location.SourceTree is not null &&
        project.AuthoredSyntaxTrees.Contains(location.SourceTree) &&
        !DotNetGeneratedSource.IsGenerated(location.SourceTree));

    static bool IsAuthoredOrMetadataSymbol(ISymbol symbol, DotNetProjectCompilation project) => symbol.Locations.All(location =>
        !location.IsInSource ||
        (location.SourceTree is not null &&
         project.AuthoredSyntaxTrees.Contains(location.SourceTree) &&
         !DotNetGeneratedSource.IsGenerated(location.SourceTree)));

    static SourceRange? SourceOf(AttributeData? attribute, IParameterSymbol parameter, DotNetProjectCompilation project)
    {
        if (attribute?.ApplicationSyntaxReference?.GetSyntax() is { } syntax)
        {
            return CritterStackSource.RangeForProject(syntax.GetLocation(), project);
        }

        return SourceOf(parameter, project);
    }

    static SourceRange? SourceOf(ISymbol symbol, DotNetProjectCompilation project)
    {
        var location = symbol.Locations.FirstOrDefault(candidate =>
            candidate.IsInSource &&
            candidate.SourceTree is not null &&
            project.AuthoredSyntaxTrees.Contains(candidate.SourceTree) &&
            !DotNetGeneratedSource.IsGenerated(candidate.SourceTree));
        return location is null ? null : CritterStackSource.RangeForProject(location, project);
    }

    static SourceRange? SourceOf(IInvocationOperation invocation, SemanticModel semanticModel, DotNetProjectCompilation project)
    {
        var syntax = invocation.Syntax;
        return semanticModel.SyntaxTree == syntax.SyntaxTree
            ? CritterStackSource.RangeForProject(syntax.GetLocation(), project)
            : null;
    }

    static string SourcePathOf(ISymbol symbol, DotNetProjectCompilation project) =>
        SourceOf(symbol, project)?.Path ?? string.Empty;

    static int SourcePositionOf(ISymbol symbol, DotNetProjectCompilation project) => symbol.Locations
        .Where(location => location.IsInSource && location.SourceTree is not null && project.AuthoredSyntaxTrees.Contains(location.SourceTree))
        .Select(location => location.SourceSpan.Start)
        .DefaultIfEmpty(int.MaxValue)
        .Min();

    static string LowerFirst(string value) => value.Length == 0
        ? value
        : $"{char.ToLowerInvariant(value[0])}{value[1..]}";

    sealed record MutableCondition(
        INamedTypeSymbol TagType,
        INamedTypeSymbol? EventType,
        string? SourceMember,
        SourceRange? Source);

    sealed record TagContext(
        INamedTypeSymbol TagType,
        string? SourceMember,
        MutableCondition? TagOnlyCondition);
}
