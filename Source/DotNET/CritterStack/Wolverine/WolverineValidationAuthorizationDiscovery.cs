// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Cratis.Screenplay.Generation;
using Cratis.Screenplay.Generation.DotNet;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Cratis.CritterStack.Screenplay.Wolverine;

static class WolverineValidationAuthorizationDiscovery
{
    public static WolverineValidationAuthorizationDiscoveryResult Discover(DotNetProjectCompilation project)
    {
        var validationPolicies = new List<WolverineValidationPolicyActivation>();
        var authorizationPolicies = new List<WolverineAuthorizationPolicyActivation>();
        var diagnostics = new List<GenerationDiagnostic>();
        var sourceTypes = new DotNetArtifactCatalog(project.Compilation).Types.ToArray();

        foreach (var call in ConfigurationCalls(project))
        {
            var containingType = DotNetSubjectIds.MetadataName(call.Method.ContainingType.OriginalDefinition);
            if (IsValidationActivation(containingType, call.Method.Name, out var kind, out var scope))
            {
                DiscoverValidationActivation(project, call, kind, scope, validationPolicies, diagnostics);
                continue;
            }

            if (containingType == WellKnownTypes.WolverineHttpOptions)
            {
                switch (call.Method.Name)
                {
                    case "RequireAuthorizeOnAll":
                        DiscoverGlobalAuthorization(project, call, "RequireAuthorizeOnAll", authorizationPolicies, diagnostics);
                        break;
                    case "ConfigureEndpoints":
                        DiscoverConfiguredEndpointAuthorization(project, call, authorizationPolicies, diagnostics);
                        break;
                }
            }
        }

        diagnostics.AddRange(validationPolicies.Select(policy => new GenerationDiagnostic
        {
            Code = WolverineDiagnosticCodes.ValidationPolicyOmitted,
            Severity = GenerationDiagnosticSeverity.Information,
            Message = $"Wolverine {ValidationName(policy.Kind)} {ScopeName(policy.Scope)} policy is enabled by '{policy.MethodName}', but current generation contracts cannot represent policy activation",
            Source = policy.Source,
            Subject = policy.Subject
        }));
        diagnostics.AddRange(authorizationPolicies.Select(policy => new GenerationDiagnostic
        {
            Code = WolverineDiagnosticCodes.AuthorizationOmitted,
            Severity = GenerationDiagnosticSeverity.Warning,
            Message = $"Global Wolverine HTTP authorization is enabled by {policy.Description}, but current generation contracts cannot represent authorization without overloading an unrelated relationship",
            Source = policy.Source,
            Subject = policy.Subject
        }));

        return new(project, validationPolicies, sourceTypes, diagnostics);
    }

    static void DiscoverValidationActivation(
        DotNetProjectCompilation project,
        ConfigurationCall call,
        WolverineValidationPolicyKind kind,
        WolverineValidationPolicyScope scope,
        List<WolverineValidationPolicyActivation> policies,
        List<GenerationDiagnostic> diagnostics)
    {
        if (IsConditionallyExecuted(call.Invocation))
        {
            diagnostics.Add(UnresolvedValidation(
                project,
                call,
                "the policy call is conditionally executed at runtime"));
            return;
        }

        var canDiscoverValidators = true;
        var includeInternalValidators = false;
        var optionsResolved = true;
        if (kind == WolverineValidationPolicyKind.FluentValidation &&
            !TryResolveFluentValidationOptions(call, out canDiscoverValidators, out includeInternalValidators, out var reason))
        {
            diagnostics.Add(UnresolvedValidation(project, call, reason));
            canDiscoverValidators = false;
            includeInternalValidators = false;
            optionsResolved = false;
        }

        var source = CritterStackSource.RangeForProject(call.Invocation.GetLocation(), project);
        var subject = ConfigurationSubject(project, call);
        policies.Add(new(
            kind,
            scope,
            canDiscoverValidators,
            includeInternalValidators,
            source,
            subject,
            call.Method.Name));

        if (kind == WolverineValidationPolicyKind.FluentValidation && optionsResolved && !canDiscoverValidators)
        {
            diagnostics.Add(UnresolvedValidation(
                project,
                call,
                "validator applicability depends on explicit runtime container registrations"));
        }
    }

    static void DiscoverGlobalAuthorization(
        DotNetProjectCompilation project,
        ConfigurationCall call,
        string description,
        List<WolverineAuthorizationPolicyActivation> policies,
        List<GenerationDiagnostic> diagnostics)
    {
        if (IsConditionallyExecuted(call.Invocation))
        {
            diagnostics.Add(UnresolvedAuthorization(
                project,
                call,
                "the global authorization call is conditionally executed at runtime"));
            return;
        }

        policies.Add(new(
            CritterStackSource.RangeForProject(call.Invocation.GetLocation(), project),
            ConfigurationSubject(project, call),
            $"'{description}'"));
    }

    static void DiscoverConfiguredEndpointAuthorization(
        DotNetProjectCompilation project,
        ConfigurationCall call,
        List<WolverineAuthorizationPolicyActivation> policies,
        List<GenerationDiagnostic> diagnostics)
    {
        if (call.Invocation.ArgumentList.Arguments.FirstOrDefault()?.Expression is not LambdaExpressionSyntax lambda ||
            LambdaParameter(lambda, call.SemanticModel) is not { } callbackParameter)
        {
            return;
        }

        var authorizationCalls = new List<InvocationExpressionSyntax>();
        foreach (var invocation in lambda.DescendantNodes().OfType<InvocationExpressionSyntax>())
        {
            if (call.SemanticModel.GetSymbolInfo(invocation).Symbol is IMethodSymbol invoked &&
                invoked.Name == "RequireAuthorization" &&
                DotNetSubjectIds.MetadataName((invoked.ReducedFrom ?? invoked).ContainingType.OriginalDefinition) == WellKnownTypes.AspNetAuthorizationEndpointExtensions &&
                invocation.Expression is MemberAccessExpressionSyntax memberAccess &&
                IsRootedInParameter(memberAccess.Expression, callbackParameter, call.SemanticModel))
            {
                authorizationCalls.Add(invocation);
            }
        }

        if (authorizationCalls.Count == 0)
        {
            return;
        }

        if (IsConditionallyExecuted(call.Invocation) ||
            authorizationCalls.Exists(_ => IsConditionallyExecuted(_) || !HasConstantAuthorizationArguments(call.SemanticModel, _)))
        {
            diagnostics.Add(UnresolvedAuthorization(
                project,
                call,
                "the endpoint authorization policy is conditional or has runtime arguments"));
            return;
        }

        policies.Add(new(
            CritterStackSource.RangeForProject(call.Invocation.GetLocation(), project),
            ConfigurationSubject(project, call),
            "'ConfigureEndpoints(... RequireAuthorization(...))'"));
    }

    static bool HasConstantAuthorizationArguments(SemanticModel semanticModel, InvocationExpressionSyntax invocation) =>
        invocation.ArgumentList.Arguments.All(argument =>
            semanticModel.GetTypeInfo(argument.Expression).ConvertedType?.SpecialType == SpecialType.System_String &&
            semanticModel.GetConstantValue(argument.Expression).HasValue);

    static bool TryResolveFluentValidationOptions(
        ConfigurationCall call,
        out bool canDiscoverValidators,
        out bool includeInternalValidators,
        out string reason)
    {
        canDiscoverValidators = true;
        includeInternalValidators = false;
        reason = string.Empty;
        foreach (var argument in call.Invocation.ArgumentList.Arguments)
        {
            var expression = argument.Expression;
            if (expression is LambdaExpressionSyntax lambda)
            {
                return TryResolveFluentValidationCallback(
                    call.SemanticModel,
                    lambda,
                    out canDiscoverValidators,
                    out includeInternalValidators,
                    out reason);
            }

            if (expression is AnonymousMethodExpressionSyntax)
            {
                reason = "the FluentValidation configuration callback is executed at runtime";
                return false;
            }

            var convertedType = call.SemanticModel.GetTypeInfo(expression).ConvertedType;
            if (convertedType is INamedTypeSymbol { TypeKind: TypeKind.Delegate } delegateType &&
                delegateType.TypeArguments.Any(_ => _ is INamedTypeSymbol named && DotNetSubjectIds.MetadataName(named) == WellKnownTypes.WolverineFluentValidationConfiguration))
            {
                reason = "the FluentValidation configuration callback is executed at runtime";
                return false;
            }

            var constant = call.SemanticModel.GetConstantValue(expression);
            if (!constant.HasValue)
            {
                reason = "a FluentValidation option is not a compile-time constant";
                return false;
            }

            if (convertedType?.SpecialType == SpecialType.System_Boolean && constant.Value is bool includeInternal)
            {
                includeInternalValidators = includeInternal;
                continue;
            }

            if (convertedType is INamedTypeSymbol enumType &&
                DotNetSubjectIds.MetadataName(enumType.OriginalDefinition) == WellKnownTypes.WolverineFluentValidationRegistrationBehavior &&
                constant.Value is int behavior)
            {
                canDiscoverValidators = behavior == 0;
                continue;
            }

            reason = "a FluentValidation option uses an unsupported runtime configuration shape";
            return false;
        }

        return true;
    }

    static bool TryResolveFluentValidationCallback(
        SemanticModel semanticModel,
        LambdaExpressionSyntax lambda,
        out bool canDiscoverValidators,
        out bool includeInternalValidators,
        out string reason)
    {
        canDiscoverValidators = true;
        includeInternalValidators = false;
        reason = string.Empty;
        if (LambdaParameter(lambda, semanticModel) is not { } callbackParameter)
        {
            reason = "the FluentValidation configuration callback parameter cannot be resolved";
            return false;
        }

        var assignments = lambda.Body switch
        {
            AssignmentExpressionSyntax assignment => [assignment],
            BlockSyntax block when block.Statements.All(_ => _ is ExpressionStatementSyntax { Expression: AssignmentExpressionSyntax }) =>
                block.Statements.Cast<ExpressionStatementSyntax>().Select(_ => (AssignmentExpressionSyntax)_.Expression).ToArray(),
            _ => []
        };
        if (assignments.Length == 0)
        {
            reason = "the FluentValidation configuration callback contains runtime or custom behavior";
            return false;
        }

        foreach (var assignment in assignments)
        {
            if (semanticModel.GetSymbolInfo(assignment.Left).Symbol is not IPropertySymbol property ||
                DotNetSubjectIds.MetadataName(property.ContainingType) != WellKnownTypes.WolverineFluentValidationConfiguration ||
                assignment.Left is not MemberAccessExpressionSyntax memberAccess ||
                !IsRootedInParameter(memberAccess.Expression, callbackParameter, semanticModel))
            {
                reason = "the FluentValidation configuration callback contains runtime or custom behavior";
                return false;
            }

            var constant = semanticModel.GetConstantValue(assignment.Right);
            if (!constant.HasValue)
            {
                reason = $"FluentValidation option '{property.Name}' is not a compile-time constant";
                return false;
            }

            switch (property.Name)
            {
                case "IncludeInternalTypes" when constant.Value is bool includeInternal:
                    includeInternalValidators = includeInternal;
                    break;
                case "RegistrationBehavior" when constant.Value is int behavior:
                    canDiscoverValidators = behavior == 0;
                    break;
                default:
                    reason = $"FluentValidation option '{property.Name}' is not statically supported";
                    return false;
            }
        }

        return true;
    }

    static IParameterSymbol? LambdaParameter(LambdaExpressionSyntax lambda, SemanticModel semanticModel)
    {
        var parameter = lambda switch
        {
            SimpleLambdaExpressionSyntax simple => simple.Parameter,
            ParenthesizedLambdaExpressionSyntax { ParameterList.Parameters.Count: 1 } parenthesized => parenthesized.ParameterList.Parameters[0],
            _ => null
        };
        return parameter is null ? null : semanticModel.GetDeclaredSymbol(parameter) as IParameterSymbol;
    }

    static bool IsRootedInParameter(
        ExpressionSyntax expression,
        IParameterSymbol parameter,
        SemanticModel semanticModel) => expression switch
        {
            IdentifierNameSyntax identifier => SymbolEqualityComparer.Default.Equals(
                semanticModel.GetSymbolInfo(identifier).Symbol,
                parameter),
            MemberAccessExpressionSyntax memberAccess => IsRootedInParameter(memberAccess.Expression, parameter, semanticModel),
            ParenthesizedExpressionSyntax parenthesized => IsRootedInParameter(parenthesized.Expression, parameter, semanticModel),
            CastExpressionSyntax cast => IsRootedInParameter(cast.Expression, parameter, semanticModel),
            _ => false
        };

    static bool IsValidationActivation(
        string containingType,
        string methodName,
        out WolverineValidationPolicyKind kind,
        out WolverineValidationPolicyScope scope)
    {
        if (containingType == WellKnownTypes.WolverineFluentValidationExtensions && methodName == "UseFluentValidation")
        {
            kind = WolverineValidationPolicyKind.FluentValidation;
            scope = WolverineValidationPolicyScope.MessageHandlers;
            return true;
        }

        if (containingType == WellKnownTypes.WolverineDataAnnotationsValidationExtensions && methodName == "UseDataAnnotationsValidation")
        {
            kind = WolverineValidationPolicyKind.DataAnnotations;
            scope = WolverineValidationPolicyScope.MessageHandlers;
            return true;
        }

        if (containingType == WellKnownTypes.WolverineHttpFluentValidationExtensions && methodName == "UseFluentValidationProblemDetailMiddleware")
        {
            kind = WolverineValidationPolicyKind.FluentValidation;
            scope = WolverineValidationPolicyScope.HttpEndpoints;
            return true;
        }

        if (containingType == WellKnownTypes.WolverineHttpOptions && methodName == "UseDataAnnotationsValidationProblemDetailMiddleware")
        {
            kind = WolverineValidationPolicyKind.DataAnnotations;
            scope = WolverineValidationPolicyScope.HttpEndpoints;
            return true;
        }

        kind = default;
        scope = default;
        return false;
    }

    static IEnumerable<ConfigurationCall> ConfigurationCalls(DotNetProjectCompilation project)
    {
        foreach (var tree in project.Compilation.SyntaxTrees
                     .Where(_ => project.AuthoredSyntaxTrees.Contains(_) && !DotNetGeneratedSource.IsGenerated(_))
                     .OrderBy(_ => _.FilePath, StringComparer.Ordinal))
        {
            var semanticModel = project.Compilation.GetSemanticModel(tree);
            foreach (var invocation in tree.GetRoot()
                         .DescendantNodes()
                         .OfType<InvocationExpressionSyntax>()
                         .OrderBy(_ => _.SpanStart))
            {
                if (semanticModel.GetSymbolInfo(invocation).Symbol is not IMethodSymbol invoked)
                {
                    continue;
                }

                yield return new(invocation, invoked.ReducedFrom ?? invoked, semanticModel);
            }
        }
    }

    static bool IsConditionallyExecuted(InvocationExpressionSyntax invocation)
    {
        foreach (var ancestor in invocation.Ancestors())
        {
            if (ancestor is BaseMethodDeclarationSyntax or LocalFunctionStatementSyntax)
            {
                return false;
            }

            if (ancestor is IfStatementSyntax or ConditionalExpressionSyntax or SwitchStatementSyntax or SwitchExpressionSyntax or
                ForStatementSyntax or ForEachStatementSyntax or WhileStatementSyntax or DoStatementSyntax or CatchClauseSyntax or
                ConditionalAccessExpressionSyntax)
            {
                return true;
            }
        }

        return false;
    }

    static GenerationDiagnostic UnresolvedValidation(
        DotNetProjectCompilation project,
        ConfigurationCall call,
        string reason) => new()
        {
            Code = WolverineDiagnosticCodes.ValidationConfigurationUnresolved,
            Severity = GenerationDiagnosticSeverity.Warning,
            Message = $"Wolverine validation call '{call.Method.Name}' was not applied because {reason}",
            Source = CritterStackSource.RangeForProject(call.Invocation.GetLocation(), project),
            Subject = ConfigurationSubject(project, call)
        };

    static GenerationDiagnostic UnresolvedAuthorization(
        DotNetProjectCompilation project,
        ConfigurationCall call,
        string reason) => new()
        {
            Code = WolverineDiagnosticCodes.AuthorizationConfigurationUnresolved,
            Severity = GenerationDiagnosticSeverity.Warning,
            Message = $"Wolverine authorization configuration call '{call.Method.Name}' was not applied because {reason}",
            Source = CritterStackSource.RangeForProject(call.Invocation.GetLocation(), project),
            Subject = ConfigurationSubject(project, call)
        };

    static SubjectId ConfigurationSubject(DotNetProjectCompilation project, ConfigurationCall call)
    {
        var containingType = call.SemanticModel.GetEnclosingSymbol(call.Invocation.SpanStart)?.ContainingType;
        return containingType is null
            ? new SubjectId { Value = $"dotnet://{project.Name}/#wolverine-configuration" }
            : project.SubjectForType(containingType);
    }

    static string ValidationName(WolverineValidationPolicyKind kind) => kind == WolverineValidationPolicyKind.FluentValidation
        ? "FluentValidation"
        : "DataAnnotations";

    static string ScopeName(WolverineValidationPolicyScope scope) => scope == WolverineValidationPolicyScope.HttpEndpoints
        ? "HTTP endpoint"
        : "message handler";

    sealed record ConfigurationCall(
        InvocationExpressionSyntax Invocation,
        IMethodSymbol Method,
        SemanticModel SemanticModel);
}
