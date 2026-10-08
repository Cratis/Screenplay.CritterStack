// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Cratis.Screenplay.Generation;
using Cratis.Screenplay.Generation.DotNet;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Operations;

namespace Cratis.CritterStack.Screenplay.Wolverine;

/// <summary>
/// One event a handler produces, with the stream identity read from source or the reason it could not be linked.
/// </summary>
/// <param name="EventType">The produced event type.</param>
/// <param name="StreamIdentity">The command member that identifies the target stream, when read from source.</param>
/// <param name="LossReason">Why the production cannot be represented as a <c>produces</c> link, or <see langword="null"/>.</param>
/// <param name="Source">The source range of the producing expression.</param>
/// <param name="LossOutcome">Whether a loss is unknown input or a recognized but unrepresentable behavior.</param>
sealed record WolverineProduction(
    INamedTypeSymbol EventType,
    ISymbol? StreamIdentity,
    string? LossReason,
    SourceRange? Source,
    GenerationDiagnosticOutcome LossOutcome = GenerationDiagnosticOutcome.Unknown);

/// <summary>
/// Reads the events a Wolverine handler produces through direct Marten session appends and aggregate-handler yields.
/// </summary>
static class WolverineProductionLinks
{
    static readonly HashSet<string> _sessionAppendMethods = ["Append", "StartStream"];

    /// <summary>
    /// Discovers <c>session.Events.Append(id, ...)</c> and <c>session.Events.StartStream(id, ...)</c> productions.
    /// </summary>
    /// <param name="method">The handler method.</param>
    /// <param name="request">The command parameter.</param>
    /// <param name="project">The project compilation.</param>
    /// <returns>The discovered productions.</returns>
    public static IReadOnlyList<WolverineProduction> SessionAppends(
        IMethodSymbol method,
        IParameterSymbol? request,
        DotNetProjectCompilation project)
    {
        var productions = new List<WolverineProduction>();
        foreach (var (declaration, semanticModel) in WolverineMethodSyntax.Declarations(method, project))
        {
            foreach (var invocationSyntax in declaration.DescendantNodes().OfType<InvocationExpressionSyntax>())
            {
                if (semanticModel.GetOperation(invocationSyntax) is not IInvocationOperation invocation ||
                    !IsSessionAppend(invocation, project))
                {
                    continue;
                }

                var source = CritterStackSource.RangeForProject(invocationSyntax.GetLocation(), project);
                var payloadArguments = invocation.Arguments
                    .Where(_ => _.Parameter is not null && !IsStreamIdentityType(_.Parameter.Type) && !IsMetadataParameter(_.Parameter))
                    .ToArray();
                var eventTypes = new List<INamedTypeSymbol>();
                var directPayloads = payloadArguments.Length > 0;
                foreach (var argument in payloadArguments)
                {
                    if (!WolverineEventStreams.TryGetDirectPayloads(argument.Value, out var argumentEvents))
                    {
                        directPayloads = false;
                        break;
                    }

                    eventTypes.AddRange(argumentEvents);
                }

                if (!directPayloads)
                {
                    productions.AddRange(UndirectedPayloadTypes(payloadArguments, project)
                        .Select(_ => new WolverineProduction(_, null, "the appended payload is not a direct event creation", source)));
                    continue;
                }

                var (identity, identityLoss) = StreamIdentity(invocation, request);
                var conditional = IsConditional(invocationSyntax, declaration);
                var lossReason = identityLoss ?? (conditional ? "the append only happens on some paths through the handler" : null);
                var lossOutcome = identityLoss is null && conditional
                    ? GenerationDiagnosticOutcome.Unsupported
                    : GenerationDiagnosticOutcome.Unknown;
                productions.AddRange(eventTypes
                    .Where(_ => !WolverineSagaTypes.IsSagaState(_, project))
                    .Select(_ => new WolverineProduction(_, lossReason is null ? identity : null, lossReason, source, lossOutcome)));
            }
        }

        return productions;
    }

    /// <summary>
    /// Discovers events an aggregate handler yields through <c>yield return</c>.
    /// </summary>
    /// <param name="method">The aggregate handler method.</param>
    /// <param name="identity">The command member identifying the aggregate stream, if known.</param>
    /// <param name="project">The project compilation.</param>
    /// <returns>The discovered productions.</returns>
    public static IReadOnlyList<WolverineProduction> AggregateYields(
        IMethodSymbol method,
        ISymbol? identity,
        DotNetProjectCompilation project)
    {
        var productions = new List<WolverineProduction>();
        foreach (var (declaration, semanticModel) in WolverineMethodSyntax.Declarations(method, project))
        {
            foreach (var yield in declaration.DescendantNodes()
                         .OfType<YieldStatementSyntax>()
                         .Where(_ => _.Expression is not null && IsInHandlerBody(_, declaration)))
            {
                var source = CritterStackSource.RangeForProject(yield.GetLocation(), project);
                if (semanticModel.GetOperation(yield.Expression!) is not { } operation ||
                    !WolverineEventStreams.TryGetDirectPayloads(operation, out var eventTypes))
                {
                    if (semanticModel.GetTypeInfo(yield.Expression!).Type is INamedTypeSymbol yieldedType &&
                        IsEventType(yieldedType, project))
                    {
                        productions.Add(new(yieldedType, null, "the yielded value is not a direct event creation", source));
                    }

                    continue;
                }

                var conditional = IsConditional(yield, declaration);
                var lossReason = YieldLossReason(conditional, identity);
                var lossOutcome = conditional ? GenerationDiagnosticOutcome.Unsupported : GenerationDiagnosticOutcome.Unknown;
                productions.AddRange(eventTypes
                    .Where(_ => IsEventType(_, project))
                    .Select(_ => new WolverineProduction(_, lossReason is null ? identity : null, lossReason, source, lossOutcome)));
            }
        }

        return productions;
    }

    /// <summary>
    /// Determines whether an invocation is a direct Marten session event append.
    /// </summary>
    /// <param name="invocation">The invocation.</param>
    /// <param name="project">The project compilation.</param>
    /// <returns><see langword="true"/> when the invocation appends to a session event store.</returns>
    public static bool IsSessionAppend(IInvocationOperation invocation, DotNetProjectCompilation project)
    {
        var target = invocation.TargetMethod.ReducedFrom ?? invocation.TargetMethod;
        if (!_sessionAppendMethods.Contains(target.Name) ||
            WolverineEventStreams.IsExactAppend(invocation, project) ||
            WolverineEventStreams.IsEventStream(target.ContainingType))
        {
            return false;
        }

        var @namespace = target.ContainingNamespace.ToDisplayString();
        return @namespace.StartsWith("Marten", StringComparison.Ordinal) ||
               @namespace.StartsWith("JasperFx.Events", StringComparison.Ordinal);
    }

    static string? YieldLossReason(bool conditional, ISymbol? identity)
    {
        if (conditional)
        {
            return "the handler yields it only on some paths, and the condition is not represented";
        }

        return identity is null ? "the aggregate stream identity is not a command property" : null;
    }

    static (ISymbol? Identity, string? LossReason) StreamIdentity(IInvocationOperation invocation, IParameterSymbol? request)
    {
        var identityArgument = invocation.Arguments.FirstOrDefault(_ => _.Parameter is not null && IsStreamIdentityType(_.Parameter.Type));
        if (identityArgument is null)
        {
            return (null, "the stream identity is assigned by Marten rather than read from the command");
        }

        var value = identityArgument.Value;
        while (value is IConversionOperation conversion)
        {
            value = conversion.Operand;
        }

        if (value is IPropertyReferenceOperation { Instance: IParameterReferenceOperation parameter } property &&
            request is not null &&
            SymbolEqualityComparer.Default.Equals(parameter.Parameter, request))
        {
            return (property.Property, null);
        }

        return (null, "the stream identity is not read directly from a property of the command");
    }

    static IEnumerable<INamedTypeSymbol> UndirectedPayloadTypes(
        IEnumerable<IArgumentOperation> arguments,
        DotNetProjectCompilation project) =>
        arguments
            .SelectMany(argument => argument.Value is IArrayCreationOperation { Initializer: not null } array
                ? array.Initializer.ElementValues
                : [argument.Value])
            .Select(_ => _ is IConversionOperation conversion ? conversion.Operand.Type : _.Type)
            .OfType<INamedTypeSymbol>()
            .Where(_ => IsEventType(_, project));

    static bool IsStreamIdentityType(ITypeSymbol type) =>
        type.SpecialType == SpecialType.System_String ||
        string.Equals(MetadataName(type), "System.Guid", StringComparison.Ordinal);

    static bool IsMetadataParameter(IParameterSymbol parameter) =>
        parameter.Type.SpecialType is SpecialType.System_Int64 or SpecialType.System_Int32 ||
        string.Equals(MetadataName(parameter.Type), "System.Type", StringComparison.Ordinal) ||
        string.Equals(MetadataName(parameter.Type), "System.Threading.CancellationToken", StringComparison.Ordinal);

    static string MetadataName(ITypeSymbol type) =>
        type is INamedTypeSymbol named ? DotNetSubjectIds.MetadataName(named.OriginalDefinition) : string.Empty;

    static bool IsEventType(INamedTypeSymbol type, DotNetProjectCompilation project) =>
        type.SpecialType == SpecialType.None &&
        !WolverineReturnTypes.IsSpecialReturn(type) &&
        !WolverineSagaTypes.IsSagaState(type, project) &&
        !DotNetSubjectIds.MetadataName(type.OriginalDefinition).StartsWith("System.", StringComparison.Ordinal);

    static bool IsInHandlerBody(SyntaxNode node, MethodDeclarationSyntax declaration) =>
        !node.Ancestors()
            .TakeWhile(_ => _ != declaration)
            .Any(_ => _ is LocalFunctionStatementSyntax or AnonymousFunctionExpressionSyntax);

    static bool IsConditional(SyntaxNode node, MethodDeclarationSyntax declaration) =>
        node.Ancestors()
            .TakeWhile(_ => _ != declaration)
            .Any(_ => _ is IfStatementSyntax
                or ElseClauseSyntax
                or SwitchStatementSyntax
                or SwitchExpressionSyntax
                or ConditionalExpressionSyntax
                or ConditionalAccessExpressionSyntax
                or CommonForEachStatementSyntax
                or ForStatementSyntax
                or WhileStatementSyntax
                or DoStatementSyntax
                or CatchClauseSyntax
                or LocalFunctionStatementSyntax
                or AnonymousFunctionExpressionSyntax) ||
        HasEarlierExit(node, declaration);

    static bool HasEarlierExit(SyntaxNode node, MethodDeclarationSyntax declaration) =>
        declaration.Body is { } body &&
        body.DescendantNodes().Any(exit =>
            exit is ReturnStatementSyntax or YieldStatementSyntax { RawKind: (int)Microsoft.CodeAnalysis.CSharp.SyntaxKind.YieldBreakStatement } &&
            exit.SpanStart < node.SpanStart &&
            IsInHandlerBody(exit, declaration) &&
            IsConditionalExit(exit, declaration));

    static bool IsConditionalExit(SyntaxNode exit, MethodDeclarationSyntax declaration) =>
        exit.Ancestors()
            .TakeWhile(_ => _ != declaration)
            .Any(_ => _ is IfStatementSyntax or ElseClauseSyntax or SwitchStatementSyntax or CatchClauseSyntax);
}
