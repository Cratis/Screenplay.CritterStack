// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Cratis.Screenplay.Generation;
using Cratis.Screenplay.Generation.DotNet;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Operations;

namespace Cratis.CritterStack.Screenplay.Wolverine;

internal static partial class WolverineDcb
{
    static readonly string[] _taskFactoryNames =
    [
        "Task",
        "ValueTask",
        "System.Threading.Tasks.Task",
        "System.Threading.Tasks.ValueTask",
        "global::System.Threading.Tasks.Task",
        "global::System.Threading.Tasks.ValueTask"
    ];

    static readonly string[] _valueTaskTypePrefixes =
    [
        "ValueTask<",
        "System.Threading.Tasks.ValueTask<",
        "global::System.Threading.Tasks.ValueTask<"
    ];

    static ParsedQuery ParseQuery(
        IMethodSymbol companion,
        IParameterSymbol? request,
        IMethodSymbol handler,
        DotNetProjectCompilation project) =>
        WolverineMethodSyntax.Declarations(companion, project)
            .Select(_ => ParseDeclaration(_.Declaration, _.SemanticModel, request, handler, project))
            .FirstOrDefault() ?? ParsedQuery.Unresolved(SourceOf(companion, project));

    static ParsedQuery ParseDeclaration(
        MethodDeclarationSyntax declaration,
        SemanticModel semanticModel,
        IParameterSymbol? request,
        IMethodSymbol handler,
        DotNetProjectCompilation project)
    {
        if (QueryExpression(declaration) is not { } expression)
        {
            return ParsedQuery.Unresolved(CritterStackSource.RangeForProject(declaration.GetLocation(), project));
        }

        var source = CritterStackSource.RangeForProject(expression.GetLocation(), project);
        var operation = UnwrapQueryResult(semanticModel.GetOperation(expression));
        var parsed = operation is null
            ? null
            : new QueryChainParser(request, handler, project, semanticModel).Parse(operation);
        return parsed is null ? ParsedQuery.Unresolved(source) : parsed with { Source = source };
    }

    static ExpressionSyntax? QueryExpression(MethodDeclarationSyntax declaration)
    {
        if (declaration.ExpressionBody is { } body)
        {
            return body.Expression;
        }

        return declaration.Body is { } block ? BodyQueryExpression(block) : null;
    }

    static ExpressionSyntax? BodyQueryExpression(BlockSyntax body) => body.Statements switch
    {
        [ReturnStatementSyntax { Expression: { } direct }] => direct,
        [
            LocalDeclarationStatementSyntax { Declaration.Variables: [{ Initializer.Value: var initializer } local] },
            ReturnStatementSyntax { Expression: { } returned }
        ] when IsUnmodifiedLocalReturn(returned, local.Identifier.ValueText) => initializer,
        _ => null
    };

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

    static bool IsTaskFactoryExpression(ExpressionSyntax expression) =>
        _taskFactoryNames.Contains(expression.ToString(), StringComparer.Ordinal);

    static bool IsValueTaskType(TypeSyntax type)
    {
        var name = type.ToString();
        return _valueTaskTypePrefixes.Any(prefix => name.StartsWith(prefix, StringComparison.Ordinal));
    }

    static IOperation? UnwrapQueryResult(IOperation? operation)
    {
        operation = Unwrap(operation);
        return TaskResultArgument(operation) is { } inner ? Unwrap(inner) : operation;
    }

    static IOperation? TaskResultArgument(IOperation? operation) => operation switch
    {
        IInvocationOperation invocation => TaskFromResultArgument(invocation),
        IObjectCreationOperation { Type: INamedTypeSymbol valueTask, Arguments.Length: 1 } creation
            when DotNetSubjectIds.MetadataName(valueTask.OriginalDefinition) == "System.Threading.Tasks.ValueTask`1" =>
            creation.Arguments[0].Value,
        _ => null
    };

    static IOperation? TaskFromResultArgument(IInvocationOperation invocation) =>
        invocation is { TargetMethod: { IsStatic: true } target, Arguments.Length: 1 } &&
        string.Equals(target.Name, "FromResult", StringComparison.Ordinal) &&
        IsTaskFactoryType(target.ContainingType)
            ? invocation.Arguments[0].Value
            : null;

    static bool IsExactQueryMethod(IMethodSymbol method, DotNetProjectCompilation project) =>
        IsExactQueryType(method.ContainingType, project) && IsDeclaredOn(method, method.ContainingType);

    static bool IsDeclaredOn(IMethodSymbol method, INamedTypeSymbol type) =>
        type.GetMembers(method.Name).OfType<IMethodSymbol>().Any(candidate =>
            SymbolEqualityComparer.Default.Equals(candidate.OriginalDefinition, method.OriginalDefinition));

    static bool IsExactQueryType(ITypeSymbol? type, DotNetProjectCompilation project) =>
        type is INamedTypeSymbol named &&
        project.Compilation.GetTypeByMetadataName(WellKnownTypes.JasperFxEventTagQuery) is { } canonical &&
        WolverineSymbolAuthority.IsAuthoredOrMetadataSymbol(canonical, project) &&
        SymbolEqualityComparer.Default.Equals(named.OriginalDefinition, canonical);

    static string? SourceMember(
        IOperation operation,
        IParameterSymbol? request,
        IMethodSymbol handler,
        DotNetProjectCompilation project)
    {
        operation = Unwrap(operation)!;
        return RequestPropertyMember(operation, request, project) ?? HandlerParameterMember(operation, handler, project);
    }

    static string? RequestPropertyMember(IOperation operation, IParameterSymbol? request, DotNetProjectCompilation project) =>
        request is not null &&
        operation is IPropertyReferenceOperation property &&
        WolverineSymbolAuthority.IsAuthoredOrMetadataSymbol(property.Property, project) &&
        Unwrap(property.Instance) is IParameterReferenceOperation requestReference &&
        SymbolEqualityComparer.Default.Equals(requestReference.Parameter.Type, request.Type)
            ? LowerFirst(property.Property.Name)
            : null;

    static string? HandlerParameterMember(IOperation operation, IMethodSymbol handler, DotNetProjectCompilation project) =>
        operation is IParameterReferenceOperation reference &&
        handler.Parameters.Any(parameter => IsPlainHandlerParameter(parameter, reference.Parameter, project))
            ? LowerFirst(reference.Parameter.Name)
            : null;

    static bool IsPlainHandlerParameter(IParameterSymbol parameter, IParameterSymbol referenced, DotNetProjectCompilation project) =>
        !HasAttributedParameter(parameter, project) &&
        parameter.Name == referenced.Name &&
        SymbolEqualityComparer.Default.Equals(parameter.Type, referenced.Type);

    sealed record ParsedQuery(
        IReadOnlyList<WolverineDcbCondition> Conditions,
        bool Resolved,
        string? SourceMember,
        SourceRange? Source)
    {
        public static ParsedQuery Unresolved(SourceRange? source) => new([], false, null, source);
    }

    sealed record MutableCondition(
        INamedTypeSymbol TagType,
        INamedTypeSymbol? EventType,
        string? SourceMember,
        SourceRange? Source);

    sealed record TagContext(
        INamedTypeSymbol TagType,
        string? SourceMember,
        MutableCondition? TagOnlyCondition);

    sealed class QueryChainParser(
        IParameterSymbol? request,
        IMethodSymbol handler,
        DotNetProjectCompilation project,
        SemanticModel semanticModel)
    {
        readonly List<MutableCondition> _conditions = [];
        readonly List<string?> _sourceMembers = [];
        TagContext? _context;
        bool _started;

        public ParsedQuery? Parse(IOperation operation)
        {
            var (calls, root) = Spine(operation);
            if (!IsQueryRoot(calls, root))
            {
                return null;
            }

            _started = root is IObjectCreationOperation;
            foreach (var call in calls)
            {
                if (!Apply(call))
                {
                    return null;
                }
            }

            return _started ? new(Conditions(), true, CommonSourceMember(), null) : null;
        }

        static (List<IInvocationOperation> Calls, IOperation? Root) Spine(IOperation operation)
        {
            var calls = new List<IInvocationOperation>();
            var current = Unwrap(operation);
            while (current is IInvocationOperation invocation)
            {
                calls.Add(invocation);
                current = Unwrap(invocation.Instance);
            }

            calls.Reverse();
            return (calls, current);
        }

        bool IsQueryRoot(List<IInvocationOperation> calls, IOperation? root) => root switch
        {
            IObjectCreationOperation creation => IsExactQueryType(creation.Type, project),
            _ => calls.Count > 0
        };

        bool Apply(IInvocationOperation call)
        {
            var method = call.TargetMethod;
            if (!IsExactQueryMethod(method, project))
            {
                return false;
            }

            return method.Name switch
            {
                "For" when IsStartCall(call) => ApplyFor(call),
                "Or" when IsContinuationCall(call, typeArguments: 1) => ApplyOrTag(call),
                "Or" when IsContinuationCall(call, typeArguments: 2) => ApplyOrEvent(call),
                "AndEventsOfType" when IsAndEventsCall(call) && _context is { } context => ApplyAndEvents(call, context),
                _ => false
            };
        }

        bool IsStartCall(IInvocationOperation call) =>
            !_started && call.TargetMethod.IsStatic && call.TargetMethod.TypeArguments.Length == 1 && call.Arguments.Length == 1;

        bool IsContinuationCall(IInvocationOperation call, int typeArguments) =>
            _started && !call.TargetMethod.IsStatic && call.Arguments.Length == 1 && call.TargetMethod.TypeArguments.Length == typeArguments;

        bool IsAndEventsCall(IInvocationOperation call) =>
            _started && !call.TargetMethod.IsStatic && call.Arguments.Length == 0 && call.TargetMethod.TypeArguments.Length is >= 1 and <= 6;

        bool ApplyFor(IInvocationOperation call)
        {
            if (call.TargetMethod.TypeArguments[0] is not INamedTypeSymbol tagType)
            {
                return false;
            }

            var sourceMember = SourceMember(call.Arguments[0].Value, request, handler, project);
            _sourceMembers.Add(sourceMember);
            _context = new(tagType, sourceMember, null);
            _started = true;
            return true;
        }

        bool ApplyOrTag(IInvocationOperation call)
        {
            if (call.TargetMethod.TypeArguments[0] is not INamedTypeSymbol tagType)
            {
                return false;
            }

            var condition = AddCondition(call, tagType, null);
            _context = new(tagType, condition.SourceMember, condition);
            return true;
        }

        bool ApplyOrEvent(IInvocationOperation call)
        {
            if (call.TargetMethod.TypeArguments[0] is not INamedTypeSymbol eventType ||
                call.TargetMethod.TypeArguments[1] is not INamedTypeSymbol tagType)
            {
                return false;
            }

            var condition = AddCondition(call, tagType, eventType);
            _context = new(tagType, condition.SourceMember, null);
            return true;
        }

        bool ApplyAndEvents(IInvocationOperation call, TagContext context)
        {
            var eventTypes = call.TargetMethod.TypeArguments.OfType<INamedTypeSymbol>().ToArray();
            if (eventTypes.Length != call.TargetMethod.TypeArguments.Length)
            {
                return false;
            }

            if (context.TagOnlyCondition is not null)
            {
                _conditions.Remove(context.TagOnlyCondition);
                _context = context with { TagOnlyCondition = null };
            }

            foreach (var eventType in eventTypes)
            {
                _conditions.Add(new(context.TagType, eventType, context.SourceMember, SourceOf(call, semanticModel, project)));
            }

            return true;
        }

        MutableCondition AddCondition(IInvocationOperation call, INamedTypeSymbol tagType, INamedTypeSymbol? eventType)
        {
            var condition = new MutableCondition(
                tagType,
                eventType,
                SourceMember(call.Arguments[0].Value, request, handler, project),
                SourceOf(call, semanticModel, project));
            _conditions.Add(condition);
            _sourceMembers.Add(condition.SourceMember);
            return condition;
        }

        string? CommonSourceMember() =>
            _sourceMembers is [{ } first, ..] &&
            _sourceMembers.TrueForAll(candidate => string.Equals(candidate, first, StringComparison.Ordinal))
                ? first
                : null;

        List<WolverineDcbCondition> Conditions() =>
        [
            .. _conditions.Select((condition, ordinal) => new WolverineDcbCondition(
                ordinal,
                condition.TagType,
                condition.EventType,
                condition.SourceMember,
                condition.Source))
        ];
    }
}
