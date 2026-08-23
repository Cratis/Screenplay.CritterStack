// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Cratis.Screenplay.Generation;
using Cratis.Screenplay.Generation.DotNet;
using Microsoft.CodeAnalysis;

namespace Cratis.CritterStack.Screenplay.Wolverine;

enum WolverineValidationPolicyKind
{
    FluentValidation,
    DataAnnotations
}

enum WolverineValidationPolicyScope
{
    MessageHandlers,
    HttpEndpoints
}

sealed record WolverineValidationPolicyActivation(
    WolverineValidationPolicyKind Kind,
    WolverineValidationPolicyScope Scope,
    bool CanDiscoverValidators,
    bool IncludeInternalValidators,
    SourceRange? Source,
    SubjectId Subject,
    string MethodName);

sealed record WolverineAuthorizationPolicyActivation(
    SourceRange? Source,
    SubjectId Subject,
    string Description);

sealed class WolverineValidationAuthorizationDiscoveryResult(
    DotNetProjectCompilation project,
    IReadOnlyList<WolverineValidationPolicyActivation> validationPolicies,
    IReadOnlyList<INamedTypeSymbol> sourceTypes,
    IReadOnlyList<GenerationDiagnostic> diagnostics)
{
    public IReadOnlyList<GenerationDiagnostic> Diagnostics { get; } = diagnostics;

    public bool HasCompoundValidation(IMethodSymbol handler) => CompoundValidationMethods(handler).Any();

    public IEnumerable<GenerationDiagnostic> ValidationDiagnostics(
        IMethodSymbol handler,
        INamedTypeSymbol requestType,
        SubjectId subject,
        bool isHttpEndpoint)
    {
        foreach (var validation in CompoundValidationMethods(handler))
        {
            yield return OmittedValidation(
                subject,
                SourceOf(validation),
                $"Compound middleware method '{validation.Name}' for '{requestType.Name}' is applied to handler '{handler.Name}'");
        }

        var scope = isHttpEndpoint
            ? WolverineValidationPolicyScope.HttpEndpoints
            : WolverineValidationPolicyScope.MessageHandlers;
        foreach (var policy in validationPolicies.Where(_ => _.Scope == scope))
        {
            if (policy.Kind == WolverineValidationPolicyKind.FluentValidation)
            {
                var validator = ValidatorFor(requestType, policy);
                if (validator is null)
                {
                    continue;
                }

                yield return OmittedValidation(
                    subject,
                    SourceOf(validator),
                    $"FluentValidation {ScopeName(scope)} validation for '{requestType.Name}' is applied by validator '{validator.ToDisplayString()}' after '{policy.MethodName}' enabled the policy");
            }
            else if (HasDataAnnotationsValidation(requestType))
            {
                yield return OmittedValidation(
                    subject,
                    SourceOf(requestType),
                    $"DataAnnotations {ScopeName(scope)} validation for '{requestType.Name}' is applied after '{policy.MethodName}' enabled the policy");
            }
        }
    }

    public IEnumerable<GenerationDiagnostic> AuthorizationDiagnostics(
        IMethodSymbol endpoint,
        SubjectId subject)
    {
        var allowAnonymous = AttributeOn(endpoint, WellKnownTypes.AspNetAllowAnonymousAttribute);
        var authorize = AttributesOn(endpoint, WellKnownTypes.AspNetAuthorizeAttribute).ToArray();

        if (allowAnonymous is not null)
        {
            yield return OmittedAuthorization(
                subject,
                SourceOf(allowAnonymous, endpoint),
                $"Wolverine HTTP endpoint '{endpoint.Name}' explicitly allows anonymous access and overrides applicable authorization policies");
            yield break;
        }

        foreach (var attribute in authorize)
        {
            var details = AuthorizationDetails(attribute);
            yield return OmittedAuthorization(
                subject,
                SourceOf(attribute, endpoint),
                $"Wolverine HTTP endpoint '{endpoint.Name}' requires ASP.NET authorization{details}");
        }
    }

    IEnumerable<IMethodSymbol> CompoundValidationMethods(IMethodSymbol handler)
    {
        var handlerParameterTypes = handler.Parameters.Select(_ => _.Type).ToArray();
        foreach (var method in handler.ContainingType.GetMembers().OfType<IMethodSymbol>())
        {
            var isValidationMethod = string.Equals(method.Name, "Validate", StringComparison.Ordinal) ||
                                     string.Equals(method.Name, "ValidateAsync", StringComparison.Ordinal);
            if (!SymbolEqualityComparer.Default.Equals(method, handler) &&
                method.DeclaredAccessibility == Accessibility.Public &&
                isValidationMethod &&
                method.Locations.Any(IsAuthoredSourceLocation) &&
                (method.Parameters.Length == 0 || method.Parameters.Any(parameter =>
                    handlerParameterTypes.Any(handlerType => SymbolEqualityComparer.Default.Equals(handlerType, parameter.Type)))))
            {
                yield return method;
            }
        }
    }

    INamedTypeSymbol? ValidatorFor(
        INamedTypeSymbol requestType,
        WolverineValidationPolicyActivation policy)
    {
        if (!policy.CanDiscoverValidators)
        {
            return null;
        }

        foreach (var type in sourceTypes)
        {
            var supportedAccessibility = type.DeclaredAccessibility == Accessibility.Public ||
                                         (policy.IncludeInternalValidators && type.DeclaredAccessibility == Accessibility.Internal);
            if (supportedAccessibility &&
                type.Locations.Any(IsAuthoredSourceLocation) &&
                type.AllInterfaces.Any(@interface =>
                    DotNetSubjectIds.MetadataName(@interface.OriginalDefinition) == WellKnownTypes.FluentValidationValidator &&
                    @interface.TypeArguments.Length == 1 &&
                    SymbolEqualityComparer.Default.Equals(@interface.TypeArguments[0], requestType)))
            {
                return type;
            }
        }

        return null;
    }

    bool HasDataAnnotationsValidation(INamedTypeSymbol requestType) =>
        requestType.AllInterfaces.Any(_ =>
            DotNetSubjectIds.MetadataName(_.OriginalDefinition) == WellKnownTypes.DataAnnotationsValidatableObject) ||
        requestType.GetMembers().OfType<IPropertySymbol>().Any(property =>
            property.GetAttributes().Any(attribute => InheritsFrom(
                attribute.AttributeClass,
                WellKnownTypes.DataAnnotationsValidationAttribute)));

    bool InheritsFrom(INamedTypeSymbol? type, string metadataName)
    {
        for (var current = type; current is not null; current = current.BaseType)
        {
            if (DotNetSubjectIds.MetadataName(current.OriginalDefinition) == metadataName)
            {
                return true;
            }
        }

        return false;
    }

    AttributeData? AttributeOn(IMethodSymbol endpoint, string metadataName) =>
        AttributesOn(endpoint, metadataName).FirstOrDefault();

    IEnumerable<AttributeData> AttributesOn(IMethodSymbol endpoint, string metadataName) =>
        endpoint.GetAttributes()
            .Concat(endpoint.ContainingType.GetAttributes())
            .Where(_ => DotNetSubjectIds.MetadataName(_.AttributeClass!) == metadataName);

    string AuthorizationDetails(AttributeData attribute)
    {
        var values = new List<string>();
        if (attribute.ConstructorArguments.FirstOrDefault().Value is string policy && policy.Length > 0)
        {
            values.Add($"policy '{policy}'");
        }

        foreach (var argument in attribute.NamedArguments)
        {
            if (argument.Value.Value is string value && value.Length > 0)
            {
                values.Add($"{argument.Key.ToLowerInvariant()} '{value}'");
            }
        }

        return values.Count == 0 ? string.Empty : $" with {string.Join(", ", values)}";
    }

    GenerationDiagnostic OmittedValidation(SubjectId subject, SourceRange? source, string behavior) => new()
    {
        Code = WolverineDiagnosticCodes.ValidationOmitted,
        Severity = GenerationDiagnosticSeverity.Warning,
        Message = $"{behavior}, but current generation contracts cannot represent validation without overloading an unrelated relationship",
        Source = source,
        Subject = subject
    };

    GenerationDiagnostic OmittedAuthorization(SubjectId subject, SourceRange? source, string behavior) => new()
    {
        Code = WolverineDiagnosticCodes.AuthorizationOmitted,
        Severity = GenerationDiagnosticSeverity.Warning,
        Message = $"{behavior}, but current generation contracts cannot represent authorization without overloading an unrelated relationship",
        Source = source,
        Subject = subject
    };

    SourceRange? SourceOf(ISymbol symbol) => DotNetSource.Range(
        symbol.Locations.First(IsAuthoredSourceLocation),
        project.SourceRoot);

    SourceRange? SourceOf(AttributeData attribute, ISymbol fallback)
    {
        var syntax = attribute.ApplicationSyntaxReference?.GetSyntax();
        return syntax is null ||
               !project.AuthoredSyntaxTrees.Contains(syntax.SyntaxTree) ||
               DotNetGeneratedSource.IsGenerated(syntax.SyntaxTree)
            ? SourceOf(fallback)
            : DotNetSource.Range(syntax.GetLocation(), project.SourceRoot);
    }

    bool IsAuthoredSourceLocation(Location location) => location is
    {
        IsInSource: true,
        SourceTree: not null
    } && project.AuthoredSyntaxTrees.Contains(location.SourceTree) &&
         !DotNetGeneratedSource.IsGenerated(location.SourceTree);

    string ScopeName(WolverineValidationPolicyScope scope) => scope == WolverineValidationPolicyScope.HttpEndpoints
        ? "HTTP endpoint"
        : "message handler";
}
