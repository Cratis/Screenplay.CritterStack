// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Cratis.Screenplay.Generation.DotNet;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Cratis.CritterStack.Screenplay.Wolverine;

sealed record WolverineBusConsequence(ITypeSymbol MessageType, string Discriminator, bool IsScheduled);

static class WolverineBusConsequences
{
    static readonly HashSet<string> _busContracts =
    [
        WellKnownTypes.WolverineMessageBus,
        WellKnownTypes.WolverineCommandBus,
        WellKnownTypes.WolverineDestinationEndpoint,
        WellKnownTypes.WolverineMessageBusExtensions
    ];

    public static IReadOnlyList<WolverineBusConsequence> Discover(
        IMethodSymbol method,
        DotNetProjectCompilation project)
    {
        var consequences = new List<WolverineBusConsequence>();
        foreach (var (declaration, semanticModel) in WolverineMethodSyntax.Declarations(method, project))
        {
            foreach (var invocation in declaration.DescendantNodes().OfType<InvocationExpressionSyntax>())
            {
                if (semanticModel.GetSymbolInfo(invocation).Symbol is not IMethodSymbol invoked ||
                    ConsequenceOf(invoked, invocation, semanticModel) is not { } consequence)
                {
                    continue;
                }

                consequences.Add(consequence);
            }
        }

        return consequences;
    }

    static WolverineBusConsequence? ConsequenceOf(
        IMethodSymbol invoked,
        InvocationExpressionSyntax invocation,
        SemanticModel semanticModel)
    {
        var candidate = invoked.ReducedFrom ?? invoked;
        if (!_busContracts.Contains(DotNetSubjectIds.MetadataName(candidate.ContainingType.OriginalDefinition)) ||
            OperationOf(invoked.Name) is not { } operation)
        {
            return null;
        }

        var (discriminator, messageIndex, scheduled) = operation;
        if (invocation.ArgumentList.Arguments.Count <= messageIndex)
        {
            return null;
        }

        var message = invocation.ArgumentList.Arguments[messageIndex].Expression;
        if (semanticModel.GetTypeInfo(message).Type is not { } messageType)
        {
            return null;
        }

        scheduled |= HasScheduledDeliveryOptions(invocation, messageIndex, semanticModel);
        return new WolverineBusConsequence(
            messageType,
            scheduled && (string.Equals(discriminator, "send", StringComparison.Ordinal) || string.Equals(discriminator, "publish", StringComparison.Ordinal))
                ? $"scheduled-{discriminator}"
                : discriminator,
            scheduled);
    }

    static (string Discriminator, int MessageIndex, bool Scheduled)? OperationOf(string methodName) => methodName switch
    {
        "SendAsync" => ("send", 0, false),
        "PublishAsync" => ("publish", 0, false),
        "InvokeAsync" => ("request-reply", 0, false),
        "ScheduleAsync" => ("scheduled", 0, true),
        "BroadcastToTopicAsync" => ("broadcast-topic", 1, false),
        _ => null
    };

    static bool HasScheduledDeliveryOptions(
        InvocationExpressionSyntax invocation,
        int messageIndex,
        SemanticModel semanticModel) => invocation.ArgumentList.Arguments
        .Skip(messageIndex + 1)
        .Any(argument =>
            semanticModel.GetTypeInfo(argument.Expression).Type is INamedTypeSymbol optionsType &&
            DotNetSubjectIds.MetadataName(optionsType.OriginalDefinition) == WellKnownTypes.WolverineDeliveryOptions &&
            argument.Expression.DescendantNodesAndSelf()
                .OfType<AssignmentExpressionSyntax>()
                .Any(assignment =>
                    string.Equals(assignment.Left.ToString(), "ScheduleDelay", StringComparison.Ordinal) ||
                    string.Equals(assignment.Left.ToString(), "ScheduledTime", StringComparison.Ordinal)));
}
