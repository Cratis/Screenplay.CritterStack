// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Cratis.Screenplay.Generation;
using Cratis.Screenplay.Generation.DotNet;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Cratis.CritterStack.Screenplay.Wolverine;

static class WolverineValidationAuthorizationDiscovery
{
    static readonly (string ContainingType, string MethodName, WolverineValidationPolicyKind Kind, WolverineValidationPolicyScope Scope)[] _validationActivations =
    [
        (WellKnownTypes.WolverineFluentValidationExtensions, "UseFluentValidation", WolverineValidationPolicyKind.FluentValidation, WolverineValidationPolicyScope.MessageHandlers),
        (WellKnownTypes.WolverineDataAnnotationsValidationExtensions, "UseDataAnnotationsValidation", WolverineValidationPolicyKind.DataAnnotations, WolverineValidationPolicyScope.MessageHandlers),
        (WellKnownTypes.WolverineHttpFluentValidationExtensions, "UseFluentValidationProblemDetailMiddleware", WolverineValidationPolicyKind.FluentValidation, WolverineValidationPolicyScope.HttpEndpoints),
        (WellKnownTypes.WolverineHttpOptions, "UseDataAnnotationsValidationProblemDetailMiddleware", WolverineValidationPolicyKind.DataAnnotations, WolverineValidationPolicyScope.HttpEndpoints)
    ];

    public static WolverineValidationAuthorizationDiscoveryResult Discover(
        DotNetProjectCompilation project,
        CritterStackSubjectResolver subjects)
    {
        var context = new DiscoveryContext(project, subjects);
        var sourceTypes = new DotNetArtifactCatalog(project.Compilation).Types.ToArray();

        foreach (var call in ConfigurationCalls(project))
        {
            DiscoverCall(context, call);
        }

        context.AddActivationDiagnostics();
        return new(project, context.ValidationPolicies, sourceTypes, context.Diagnostics);
    }

    static void DiscoverCall(DiscoveryContext context, ConfigurationCall call)
    {
        var containingType = DotNetSubjectIds.MetadataName(call.Method.ContainingType.OriginalDefinition);
        if (IsValidationActivation(containingType, call.Method.Name, out var kind, out var scope))
        {
            DiscoverValidationActivation(context, call, kind, scope);
            return;
        }

        if (containingType != WellKnownTypes.WolverineHttpOptions)
        {
            return;
        }

        switch (call.Method.Name)
        {
            case "RequireAuthorizeOnAll":
                DiscoverGlobalAuthorization(context, call, "RequireAuthorizeOnAll");
                break;
            case "ConfigureEndpoints":
                DiscoverConfiguredEndpointAuthorization(context, call);
                break;
        }
    }

    static void DiscoverValidationActivation(
        DiscoveryContext context,
        ConfigurationCall call,
        WolverineValidationPolicyKind kind,
        WolverineValidationPolicyScope scope)
    {
        if (IsConditionallyExecuted(call.Invocation))
        {
            context.ReportUnresolvedValidation(call, "the policy call is conditionally executed at runtime");
            return;
        }

        var isFluent = kind == WolverineValidationPolicyKind.FluentValidation;
        var options = isFluent
            ? WolverineFluentValidationOptions.Resolve(call.Invocation, call.SemanticModel)
            : new WolverineFluentValidationOptions();
        var optionsResolved = options.Resolved;
        if (!optionsResolved)
        {
            context.ReportUnresolvedValidation(call, options.Failure!);
            options.CanDiscoverValidators = false;
            options.IncludeInternalValidators = false;
        }

        context.ValidationPolicies.Add(new(
            kind,
            scope,
            options.CanDiscoverValidators,
            options.IncludeInternalValidators,
            context.SourceOf(call),
            context.SubjectOf(call),
            call.Method.Name));

        if (isFluent && optionsResolved && !options.CanDiscoverValidators)
        {
            context.ReportUnresolvedValidation(call, "validator applicability depends on explicit runtime container registrations");
        }
    }

    static void DiscoverGlobalAuthorization(DiscoveryContext context, ConfigurationCall call, string description)
    {
        if (IsConditionallyExecuted(call.Invocation))
        {
            context.ReportUnresolvedAuthorization(call, "the global authorization call is conditionally executed at runtime");
            return;
        }

        context.AuthorizationPolicies.Add(new(context.SourceOf(call), context.SubjectOf(call), $"'{description}'"));
    }

    static void DiscoverConfiguredEndpointAuthorization(DiscoveryContext context, ConfigurationCall call)
    {
        if (call.Invocation.ArgumentList.Arguments.FirstOrDefault()?.Expression is not LambdaExpressionSyntax lambda ||
            WolverineLambdaSyntax.Parameter(lambda, call.SemanticModel) is not { } callbackParameter)
        {
            return;
        }

        var authorizationCalls = EndpointAuthorizationCalls(lambda, callbackParameter, call.SemanticModel);
        if (authorizationCalls.Count == 0)
        {
            return;
        }

        if (IsConditionallyExecuted(call.Invocation) || authorizationCalls.Exists(_ => IsRuntimeAuthorization(call.SemanticModel, _)))
        {
            context.ReportUnresolvedAuthorization(call, "the endpoint authorization policy is conditional or has runtime arguments");
            return;
        }

        context.AuthorizationPolicies.Add(new(
            context.SourceOf(call),
            context.SubjectOf(call),
            "'ConfigureEndpoints(... RequireAuthorization(...))'"));
    }

    static List<InvocationExpressionSyntax> EndpointAuthorizationCalls(
        LambdaExpressionSyntax lambda,
        IParameterSymbol callbackParameter,
        SemanticModel semanticModel) =>
        [.. lambda.DescendantNodes()
            .OfType<InvocationExpressionSyntax>()
            .Where(_ => IsAuthorizationCallOnParameter(semanticModel, _, callbackParameter))];

    static bool IsAuthorizationCallOnParameter(
        SemanticModel semanticModel,
        InvocationExpressionSyntax invocation,
        IParameterSymbol callbackParameter) =>
        semanticModel.GetSymbolInfo(invocation).Symbol is IMethodSymbol invoked &&
        string.Equals(invoked.Name, "RequireAuthorization", StringComparison.Ordinal) &&
        DotNetSubjectIds.MetadataName((invoked.ReducedFrom ?? invoked).ContainingType.OriginalDefinition) == WellKnownTypes.AspNetAuthorizationEndpointExtensions &&
        invocation.Expression is MemberAccessExpressionSyntax memberAccess &&
        WolverineLambdaSyntax.IsRootedInParameter(memberAccess.Expression, callbackParameter, semanticModel);

    static bool IsRuntimeAuthorization(SemanticModel semanticModel, InvocationExpressionSyntax invocation) =>
        IsConditionallyExecuted(invocation) || !HasConstantAuthorizationArguments(semanticModel, invocation);

    static bool HasConstantAuthorizationArguments(SemanticModel semanticModel, InvocationExpressionSyntax invocation) =>
        invocation.ArgumentList.Arguments.All(argument =>
            semanticModel.GetTypeInfo(argument.Expression).ConvertedType?.SpecialType == SpecialType.System_String &&
            semanticModel.GetConstantValue(argument.Expression).HasValue);

    static bool IsValidationActivation(
        string containingType,
        string methodName,
        out WolverineValidationPolicyKind kind,
        out WolverineValidationPolicyScope scope)
    {
        foreach (var activation in _validationActivations)
        {
            if (containingType == activation.ContainingType && methodName == activation.MethodName)
            {
                kind = activation.Kind;
                scope = activation.Scope;
                return true;
            }
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

    sealed class DiscoveryContext(DotNetProjectCompilation project, CritterStackSubjectResolver subjects)
    {
        public List<WolverineValidationPolicyActivation> ValidationPolicies { get; } = [];

        public List<WolverineAuthorizationPolicyActivation> AuthorizationPolicies { get; } = [];

        public List<GenerationDiagnostic> Diagnostics { get; } = [];

        public SourceRange? SourceOf(ConfigurationCall call) =>
            CritterStackSource.RangeForProject(call.Invocation.GetLocation(), project);

        public SubjectId SubjectOf(ConfigurationCall call)
        {
            var containingType = call.SemanticModel.GetEnclosingSymbol(call.Invocation.SpanStart)?.ContainingType;
            return containingType is null
                ? new SubjectId { Value = $"dotnet://{project.Name}/#wolverine-configuration" }
                : subjects.SubjectForType(project, containingType);
        }

        public void ReportUnresolvedValidation(ConfigurationCall call, string reason) =>
            ReportUnresolved(
                call,
                WolverineDiagnosticCodes.ValidationConfigurationUnresolved,
                $"Wolverine validation call '{call.Method.Name}' was not applied because {reason}");

        public void ReportUnresolvedAuthorization(ConfigurationCall call, string reason) =>
            ReportUnresolved(
                call,
                WolverineDiagnosticCodes.AuthorizationConfigurationUnresolved,
                $"Wolverine authorization configuration call '{call.Method.Name}' was not applied because {reason}");

        public void AddActivationDiagnostics()
        {
            Diagnostics.AddRange(ValidationPolicies.Select(policy => new GenerationDiagnostic
            {
                Code = WolverineDiagnosticCodes.ValidationPolicyOmitted,
                Severity = GenerationDiagnosticSeverity.Information,
                Outcome = GenerationDiagnosticOutcome.Unsupported,
                Message = $"Wolverine {ValidationName(policy.Kind)} {ScopeName(policy.Scope)} policy is enabled by '{policy.MethodName}', but current generation contracts cannot represent policy activation",
                Source = policy.Source,
                Subject = policy.Subject
            }));
            Diagnostics.AddRange(AuthorizationPolicies.Select(policy => new GenerationDiagnostic
            {
                Code = WolverineDiagnosticCodes.AuthorizationOmitted,
                Severity = GenerationDiagnosticSeverity.Warning,
                Outcome = GenerationDiagnosticOutcome.Unsupported,
                Message = $"Global Wolverine HTTP authorization is enabled by {policy.Description}, but current generation contracts cannot represent authorization without overloading an unrelated relationship",
                Source = policy.Source,
                Subject = policy.Subject
            }));
        }

        void ReportUnresolved(ConfigurationCall call, string code, string message) =>
            Diagnostics.Add(new()
            {
                Code = code,
                Severity = GenerationDiagnosticSeverity.Warning,
                Outcome = GenerationDiagnosticOutcome.Unknown,
                Message = message,
                Source = SourceOf(call),
                Subject = SubjectOf(call)
            });
    }
}
