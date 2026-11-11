// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Cratis.Screenplay.Generation.DotNet;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Cratis.CritterStack.Screenplay.Wolverine;

/// <summary>
/// The statically resolved options of a Wolverine <c>UseFluentValidation</c> call, or the reason they cannot be resolved.
/// </summary>
sealed class WolverineFluentValidationOptions
{
    const string RuntimeCallbackReason = "the FluentValidation configuration callback is executed at runtime";
    const string RuntimeBehaviorReason = "the FluentValidation configuration callback contains runtime or custom behavior";

    /// <summary>
    /// Gets or sets a value indicating whether validators are discovered by Wolverine.
    /// </summary>
    public bool CanDiscoverValidators { get; set; } = true;

    /// <summary>
    /// Gets or sets a value indicating whether internal validators are included.
    /// </summary>
    public bool IncludeInternalValidators { get; set; }

    /// <summary>
    /// Gets the reason the options could not be resolved, when they could not.
    /// </summary>
    public string? Failure { get; private set; }

    /// <summary>
    /// Gets a value indicating whether every option was resolved statically.
    /// </summary>
    public bool Resolved => Failure is null;

    /// <summary>
    /// Resolves the options passed to a <c>UseFluentValidation</c> call.
    /// </summary>
    /// <param name="invocation">The call to resolve.</param>
    /// <param name="semanticModel">The semantic model of the call.</param>
    /// <returns>The resolved options, or the failure reason.</returns>
    public static WolverineFluentValidationOptions Resolve(InvocationExpressionSyntax invocation, SemanticModel semanticModel)
    {
        var options = new WolverineFluentValidationOptions();
        foreach (var argument in invocation.ArgumentList.Arguments)
        {
            if (argument.Expression is LambdaExpressionSyntax lambda)
            {
                return ResolveCallback(semanticModel, lambda);
            }

            if (options.ApplyArgument(semanticModel, argument.Expression) is { } failure)
            {
                return options.Fail(failure);
            }
        }

        return options;
    }

    static WolverineFluentValidationOptions ResolveCallback(SemanticModel semanticModel, LambdaExpressionSyntax lambda)
    {
        var options = new WolverineFluentValidationOptions();
        if (WolverineLambdaSyntax.Parameter(lambda, semanticModel) is not { } callbackParameter)
        {
            return options.Fail("the FluentValidation configuration callback parameter cannot be resolved");
        }

        var assignments = CallbackAssignments(lambda);
        if (assignments.Length == 0)
        {
            return options.Fail(RuntimeBehaviorReason);
        }

        foreach (var assignment in assignments)
        {
            if (options.ApplyAssignment(semanticModel, assignment, callbackParameter) is { } failure)
            {
                return options.Fail(failure);
            }
        }

        return options;
    }

    static AssignmentExpressionSyntax[] CallbackAssignments(LambdaExpressionSyntax lambda) => lambda.Body switch
    {
        AssignmentExpressionSyntax assignment => [assignment],
        BlockSyntax block when block.Statements.All(_ => _ is ExpressionStatementSyntax { Expression: AssignmentExpressionSyntax }) =>
            block.Statements.Cast<ExpressionStatementSyntax>().Select(_ => (AssignmentExpressionSyntax)_.Expression).ToArray(),
        _ => []
    };

    static bool IsConfigurationCallback(ITypeSymbol? convertedType) =>
        convertedType is INamedTypeSymbol { TypeKind: TypeKind.Delegate } delegateType &&
        delegateType.TypeArguments.Any(_ => _ is INamedTypeSymbol named && DotNetSubjectIds.MetadataName(named) == WellKnownTypes.WolverineFluentValidationConfiguration);

    static bool IsRegistrationBehavior(ITypeSymbol? convertedType) =>
        convertedType is INamedTypeSymbol enumType &&
        DotNetSubjectIds.MetadataName(enumType.OriginalDefinition) == WellKnownTypes.WolverineFluentValidationRegistrationBehavior;

    static bool TryGetConfigurationProperty(
        SemanticModel semanticModel,
        AssignmentExpressionSyntax assignment,
        IParameterSymbol callbackParameter,
        out IPropertySymbol property)
    {
        property = null!;
        if (semanticModel.GetSymbolInfo(assignment.Left).Symbol is not IPropertySymbol found ||
            !IsConfigurationProperty(found))
        {
            return false;
        }

        if (!IsRootedMember(semanticModel, assignment.Left, callbackParameter))
        {
            return false;
        }

        property = found;
        return true;
    }

    static bool IsConfigurationProperty(IPropertySymbol property) =>
        DotNetSubjectIds.MetadataName(property.ContainingType) == WellKnownTypes.WolverineFluentValidationConfiguration;

    static bool IsRootedMember(SemanticModel semanticModel, ExpressionSyntax expression, IParameterSymbol callbackParameter) =>
        expression is MemberAccessExpressionSyntax memberAccess &&
        WolverineLambdaSyntax.IsRootedInParameter(memberAccess.Expression, callbackParameter, semanticModel);

    WolverineFluentValidationOptions Fail(string reason)
    {
        Failure = reason;
        return this;
    }

    string? ApplyArgument(SemanticModel semanticModel, ExpressionSyntax expression)
    {
        var convertedType = semanticModel.GetTypeInfo(expression).ConvertedType;
        if (expression is AnonymousMethodExpressionSyntax || IsConfigurationCallback(convertedType))
        {
            return RuntimeCallbackReason;
        }

        var constant = semanticModel.GetConstantValue(expression);
        if (!constant.HasValue)
        {
            return "a FluentValidation option is not a compile-time constant";
        }

        if (convertedType?.SpecialType == SpecialType.System_Boolean && constant.Value is bool includeInternal)
        {
            IncludeInternalValidators = includeInternal;
            return null;
        }

        if (IsRegistrationBehavior(convertedType) && constant.Value is int behavior)
        {
            CanDiscoverValidators = behavior == 0;
            return null;
        }

        return "a FluentValidation option uses an unsupported runtime configuration shape";
    }

    string? ApplyAssignment(SemanticModel semanticModel, AssignmentExpressionSyntax assignment, IParameterSymbol callbackParameter)
    {
        if (!TryGetConfigurationProperty(semanticModel, assignment, callbackParameter, out var property))
        {
            return RuntimeBehaviorReason;
        }

        var constant = semanticModel.GetConstantValue(assignment.Right);
        if (!constant.HasValue)
        {
            return $"FluentValidation option '{property.Name}' is not a compile-time constant";
        }

        switch (property.Name)
        {
            case "IncludeInternalTypes" when constant.Value is bool includeInternal:
                IncludeInternalValidators = includeInternal;
                return null;
            case "RegistrationBehavior" when constant.Value is int behavior:
                CanDiscoverValidators = behavior == 0;
                return null;
            default:
                return $"FluentValidation option '{property.Name}' is not statically supported";
        }
    }
}
