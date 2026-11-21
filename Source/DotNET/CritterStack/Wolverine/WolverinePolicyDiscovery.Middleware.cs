// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Cratis.Screenplay.Generation.DotNet;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Operations;

namespace Cratis.CritterStack.Screenplay.Wolverine;

internal static partial class WolverinePolicyDiscovery
{
    public static IReadOnlyList<WolverineMiddlewareApplication> MiddlewareFor(
        IMethodSymbol handler,
        DotNetProjectCompilation project,
        WolverineMiddlewareRegistrations registrations,
        bool isHttpEndpoint)
    {
        var applications = new List<WolverineMiddlewareApplication>();
        foreach (var attribute in MiddlewareAttributes(handler.ContainingType).Concat(MiddlewareAttributes(handler)))
        {
            if (AppliesTo(attribute, isHttpEndpoint) && HasResolvedMiddlewareTypes(attribute, project))
            {
                applications.AddRange(MiddlewareTypes(attribute).Where(_ => IsAuthored(_, project))
                    .Select(_ => new WolverineMiddlewareApplication(_, AttributeSource(attribute, project))));
            }
        }

        applications.AddRange(registrations.ApplicationsFor(isHttpEndpoint));
        return applications;
    }

    public static bool AppliesTo(AttributeData attribute, bool isHttpEndpoint)
    {
        var namedScoping = attribute.NamedArguments.FirstOrDefault(_ => _.Key == "Scoping");
        var scoping = namedScoping.Key is not null
            ? namedScoping.Value
            : attribute.ConstructorArguments.FirstOrDefault(_ => _.Type?.TypeKind == TypeKind.Enum);
        return scoping.Value switch
        {
            null or 0 => true,
            1 => !isHttpEndpoint,
            2 => isHttpEndpoint,
            _ => false
        };
    }

    public static WolverineMiddlewareRegistrations DiscoverRegistrations(DotNetProjectCompilation project) =>
        new(Registrations(project));

    static IEnumerable<WolverineMiddlewareRegistration> Registrations(DotNetProjectCompilation project)
    {
        foreach (var tree in project.AuthoredSyntaxTrees.Where(_ => !DotNetGeneratedSource.IsGenerated(_)).OrderBy(_ => _.FilePath, StringComparer.Ordinal))
        {
            var model = project.Compilation.GetSemanticModel(tree);
            foreach (var invocation in tree.GetRoot().DescendantNodes().OfType<InvocationExpressionSyntax>())
            {
                if (RegistrationFrom(invocation, model, project) is { } registration)
                {
                    yield return registration;
                }
            }
        }
    }

    static WolverineMiddlewareRegistration? RegistrationFrom(
        InvocationExpressionSyntax invocation,
        SemanticModel model,
        DotNetProjectCompilation project)
    {
        if (model.GetSymbolInfo(invocation).Symbol is not IMethodSymbol method)
        {
            return null;
        }

        if (!IsMiddlewareRegistration(method, project, out var isHttpEndpoint))
        {
            return null;
        }

        if (model.GetEnclosingSymbol(invocation.SpanStart)?.ContainingType is not { } owner)
        {
            return null;
        }

        var type = MiddlewareType(invocation, method, model);
        return new(
            type,
            owner,
            isHttpEndpoint,
            IsResolvedMiddleware(type, invocation, model, project),
            CritterStackSource.RangeForProject(invocation.GetLocation(), project));
    }

    static bool IsResolvedMiddleware(
        INamedTypeSymbol? type,
        InvocationExpressionSyntax invocation,
        SemanticModel model,
        DotNetProjectCompilation project) =>
        type is not null && IsMiddlewareType(type, project) &&
        !HasRuntimeFilter(invocation, model) && !IsConditional(invocation);

    static bool IsMiddlewareRegistration(IMethodSymbol method, DotNetProjectCompilation project, out bool isHttpEndpoint)
    {
        isHttpEndpoint = DotNetSubjectIds.MetadataName(method.ContainingType) == WellKnownTypes.WolverineHttpOptions;
        if (method.Name != "AddMiddleware")
        {
            return false;
        }

        var policies = project.Compilation.GetTypeByMetadataName(WellKnownTypes.WolverineOptions)?.GetMembers("Policies")
            .OfType<IPropertySymbol>().FirstOrDefault()?.Type;
        return isHttpEndpoint || SymbolEqualityComparer.Default.Equals(method.ContainingType, policies);
    }

    static INamedTypeSymbol? MiddlewareType(InvocationExpressionSyntax invocation, IMethodSymbol method, SemanticModel model)
    {
        if (method.TypeArguments is [INamedTypeSymbol genericType])
        {
            return genericType;
        }

        return model.GetOperation(invocation) is IInvocationOperation operation
            ? operation.Arguments.Where(_ => _.Parameter?.Name == "middlewareType")
                .Select(_ => _.Value).OfType<ITypeOfOperation>().Select(_ => _.TypeOperand).OfType<INamedTypeSymbol>().SingleOrDefault()
            : null;
    }

    static bool HasRuntimeFilter(InvocationExpressionSyntax invocation, SemanticModel model) =>
        model.GetOperation(invocation) is not IInvocationOperation operation || operation.Arguments.Any(_ =>
            _.Parameter?.Name == "filter" && (!_.Value.ConstantValue.HasValue || _.Value.ConstantValue.Value is not null));

    static bool IsConditional(InvocationExpressionSyntax invocation) => invocation.Ancestors().Any(_ => _ is
        IfStatementSyntax or ConditionalExpressionSyntax or ConditionalAccessExpressionSyntax or SwitchStatementSyntax or SwitchExpressionSyntax or
        ForStatementSyntax or ForEachStatementSyntax or ForEachVariableStatementSyntax or WhileStatementSyntax or DoStatementSyntax or CatchClauseSyntax);
}
