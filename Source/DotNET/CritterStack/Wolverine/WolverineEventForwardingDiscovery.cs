// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Cratis.Screenplay.Generation;
using Cratis.Screenplay.Generation.DotNet;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Cratis.CritterStack.Screenplay.Wolverine;

/// <summary>
/// Reports configured event forwarding without inventing an executable consumer relationship.
/// </summary>
static class WolverineEventForwardingDiscovery
{
    static readonly HashSet<string> _registrations =
    [
        "PublishEventsToWolverine", "ProcessEventsWithWolverineHandlersInStrictOrder", "SubscribeToEvents"
    ];

    public static IReadOnlyList<GenerationDiagnostic> Discover(DotNetProjectCompilation project)
    {
        List<GenerationDiagnostic> diagnostics = [];
        foreach (var tree in project.AuthoredSyntaxTrees.Where(_ => !DotNetGeneratedSource.IsGenerated(_)).OrderBy(_ => _.FilePath, StringComparer.Ordinal))
        {
            var model = project.Compilation.GetSemanticModel(tree);
            foreach (var node in tree.GetRoot().DescendantNodes().OrderBy(_ => _.SpanStart))
            {
                var description = Description(node, model);
                if (description is not null)
                {
                    diagnostics.Add(new()
                    {
                        Code = WolverineDiagnosticCodes.EventForwardingOmitted,
                        Severity = GenerationDiagnosticSeverity.Warning,
                        Outcome = GenerationDiagnosticOutcome.Unsupported,
                        Message = $"Wolverine event forwarding '{description}' is not represented as a consumer or reaction",
                        Source = CritterStackSource.RangeForProject(node.GetLocation(), project)
                    });
                }
            }
        }

        return diagnostics;
    }

    static string? Description(SyntaxNode node, SemanticModel model) => node switch
    {
        InvocationExpressionSyntax invocation => InvocationDescription(invocation, model),
        AssignmentExpressionSyntax assignment => FastForwardingDescription(assignment, model),
        _ => null
    };

    static string? InvocationDescription(InvocationExpressionSyntax invocation, SemanticModel model)
    {
        if (model.GetSymbolInfo(invocation).Symbol is not IMethodSymbol method)
        {
            return null;
        }

        var owner = MetadataName((method.ReducedFrom ?? method).ContainingType);
        if (method.Name == "TransformedTo" && owner == WellKnownTypes.WolverineEventForwardingTransform)
        {
            return $"TransformedTo: {Name(method.ContainingType.TypeArguments[0])}' to '{Name(method.TypeArguments[0])}";
        }

        if (_registrations.Contains(method.Name) && IsRegistrationOwner(owner))
        {
            return $"{method.Name}: {RegistrationTypes(invocation, model)}";
        }

        // A bare subscription opens a transform but does not itself turn forwarding on.
        // Reporting it makes the incomplete configuration visible without claiming it runs.
        return method.Name == "SubscribeToEvent" && IsForwardingOwner(owner) && !HasTransformParent(invocation, model)
            ? $"SubscribeToEvent (uncompleted transformation): {Name(method.TypeArguments[0])}"
            : null;
    }

    static bool HasTransformParent(InvocationExpressionSyntax invocation, SemanticModel model) =>
        invocation.Parent is MemberAccessExpressionSyntax { Parent: InvocationExpressionSyntax parent } &&
        model.GetSymbolInfo(parent).Symbol is IMethodSymbol method && method.Name == "TransformedTo" &&
        MetadataName(method.ContainingType) == WellKnownTypes.WolverineEventForwardingTransform;

    static string? FastForwardingDescription(AssignmentExpressionSyntax assignment, SemanticModel model)
    {
        if (model.GetSymbolInfo(assignment.Left).Symbol is not IPropertySymbol property || property.Name != "UseFastEventForwarding" ||
            MetadataName(property.ContainingType) != WellKnownTypes.WolverineMartenIntegration)
        {
            return null;
        }

        var value = model.GetConstantValue(assignment.Right);
        return value.HasValue && value.Value is false ? null : "UseFastEventForwarding: all forwarded event types";
    }

    static string RegistrationTypes(InvocationExpressionSyntax registration, SemanticModel model)
    {
        var calls = registration.DescendantNodes().OfType<InvocationExpressionSyntax>().ToArray();
        var events = calls.SelectMany(_ => RelayedEvents(_, model))
            .Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray();
        var messages = calls.Select(_ => model.GetSymbolInfo(_).Symbol).OfType<IMethodSymbol>()
            .Where(_ => (_.Name == "PublishAsync" || _.Name == "SendAsync") && MetadataName(_.ContainingType) == WellKnownTypes.WolverineMessageBus)
            .SelectMany(_ => _.TypeArguments).Select(Name).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray();
        if (events.Length > 0)
        {
            return string.Join(", ", events) + (messages.Length > 0 ? $"' to '{string.Join(", ", messages)}" : string.Empty);
        }

        var subscription = registration.ArgumentList.Arguments.LastOrDefault();
        return model.GetSymbolInfo(registration).Symbol is IMethodSymbol method && method.Name == "SubscribeToEvents" && subscription is not null
            ? model.GetTypeInfo(subscription.Expression).Type?.ToDisplayString() ?? "unresolved subscription"
            : "all subscribed event types";
    }

    static IEnumerable<string> RelayedEvents(InvocationExpressionSyntax invocation, SemanticModel model)
    {
        if (model.GetSymbolInfo(invocation).Symbol is not IMethodSymbol method || method.Name != "PublishEvent" ||
            MetadataName(method.ContainingType) != WellKnownTypes.WolverinePublishingRelay)
        {
            return [];
        }

        return method.TypeArguments.Select(Name).Concat(invocation.ArgumentList.Arguments
            .Select(_ => _.Expression).OfType<TypeOfExpressionSyntax>()
            .Select(_ => model.GetTypeInfo(_.Type).Type).OfType<ITypeSymbol>().Select(Name));
    }

    static bool IsRegistrationOwner(string owner) =>
        owner == WellKnownTypes.WolverineOptionsMartenExtensions || owner == WellKnownTypes.WolverineAncillaryMartenExtensions;

    static bool IsForwardingOwner(string owner) =>
        owner == WellKnownTypes.WolverineMartenIntegration || owner == WellKnownTypes.WolverineEventForwarding;

    static string Name(ITypeSymbol type) => type.ToDisplayString();

    static string MetadataName(INamedTypeSymbol type) => DotNetSubjectIds.MetadataName(type.OriginalDefinition);
}
