// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Cratis.Screenplay.Generation;
using Cratis.Screenplay.Generation.DotNet;
using Microsoft.CodeAnalysis;

namespace Cratis.CritterStack.Screenplay.Wolverine;

sealed record WolverineSlicePattern(GenerationSliceKind? Kind, Evidence? Evidence, bool HasDeclaration);

static class WolverineSlicePatterns
{
    public static WolverineSlicePattern Resolve(
        WolverineFactScope scope,
        IMethodSymbol method,
        INamedTypeSymbol? request,
        HttpEndpoint? endpoint)
    {
        var project = scope.Project;
        var attribute = Declaration(method, project);
        var source = attribute?.ApplicationSyntaxReference?.GetSyntax().GetLocation();
        var range = source is null ? null : CritterStackSource.RangeForProject(source, project);
        var declared = attribute is null ? null : Declared(attribute, scope, method, range);
        var trigger = Trigger(request, endpoint, project);
        if (IsConflict(declared, trigger))
        {
            scope.Diagnostics.Add(ConflictDiagnostic(scope, method, endpoint, (declared, trigger), range));
        }

        var kind = trigger ?? declared;
        var evidence = kind is null ? null : SliceEvidence(scope, method, endpoint, (trigger, declared), range);
        return new(kind, evidence, declared is not null);
    }

    public static void Apply(
        WolverineSlicePattern decision,
        WolverineFactScope scope,
        IMethodSymbol method,
        int firstPlacement)
    {
        var project = scope.Project;
        var diagnostics = scope.Diagnostics;
        var placements = scope.Placements;
        if (decision.HasDeclaration && placements.Count == firstPlacement)
        {
            diagnostics.Add(new()
            {
                Code = WolverineDiagnosticCodes.SlicePatternOmitted,
                Severity = GenerationDiagnosticSeverity.Warning,
                Outcome = GenerationDiagnosticOutcome.Unsupported,
                Message = $"The SlicePattern declaration on '{DotNetMethodIdentity.DisplayName(method)}' cannot obtain a supported artifact placement; classification does not invent handler behavior",
                Source = decision.Evidence?.Source,
                Subject = DotNetMethodIdentity.SubjectFor(project, method)
            });
        }

        if (decision.Kind is not { } kind)
        {
            return;
        }

        for (var index = firstPlacement; index < placements.Count; index++)
        {
            var intent = placements[index];
            placements[index] = intent with
            {
                CompatibilityPlacement = intent.CompatibilityPlacement with { SliceKind = kind },
                SliceKindEvidence = decision.Evidence
            };
        }
    }

    static GenerationSliceKind? Declared(
        AttributeData attribute,
        WolverineFactScope scope,
        IMethodSymbol method,
        SourceRange? range)
    {
        var (resolved, name) = DeclaredName(attribute, scope.Project);
        var declared = KindFor(name);
        if (declared is null)
        {
            scope.Diagnostics.Add(UnresolvedDiagnostic(scope, method, resolved, range));
        }

        return declared;
    }

    static GenerationSliceKind? KindFor(string? name) => name switch
    {
        "Command" => GenerationSliceKind.StateChange,
        "View" => GenerationSliceKind.StateView,
        "Automation" => GenerationSliceKind.Automation,
        "Translation" => GenerationSliceKind.Translate,
        _ => null
    };

    static GenerationDiagnostic UnresolvedDiagnostic(WolverineFactScope scope, IMethodSymbol method, bool resolved, SourceRange? range) => new()
    {
        Code = WolverineDiagnosticCodes.SlicePatternUnresolved,
        Severity = GenerationDiagnosticSeverity.Warning,
        Outcome = resolved ? GenerationDiagnosticOutcome.Unsupported : GenerationDiagnosticOutcome.Unknown,
        Message = $"The SlicePattern declaration on '{DotNetMethodIdentity.DisplayName(method)}' has an {(resolved ? "undefined" : "unresolved")} value; independently inferred classification is retained",
        Source = range,
        Subject = DotNetMethodIdentity.SubjectFor(scope.Project, method)
    };

    static bool IsConflict(GenerationSliceKind? declared, GenerationSliceKind? trigger) =>
        declared is not null && trigger is not null && declared != trigger;

    static GenerationDiagnostic ConflictDiagnostic(
        WolverineFactScope scope,
        IMethodSymbol method,
        HttpEndpoint? endpoint,
        (GenerationSliceKind? Declared, GenerationSliceKind? Trigger) kinds,
        SourceRange? range) => new()
        {
            Code = WolverineDiagnosticCodes.SlicePatternConflict,
            Severity = GenerationDiagnosticSeverity.Warning,
            Outcome = GenerationDiagnosticOutcome.Conflict,
            Message = $"The SlicePattern declaration '{kinds.Declared}' conflicts with the proven {(endpoint is null ? "TimeoutMessage" : "HTTP")} trigger '{kinds.Trigger}' on '{DotNetMethodIdentity.DisplayName(method)}'; the trigger takes precedence",
            Source = range,
            Subject = DotNetMethodIdentity.SubjectFor(scope.Project, method)
        };

    static Evidence SliceEvidence(
        WolverineFactScope scope,
        IMethodSymbol method,
        HttpEndpoint? endpoint,
        (GenerationSliceKind? Trigger, GenerationSliceKind? Declared) kinds,
        SourceRange? range) => new()
        {
            Adapter = scope.Adapter,
            Strength = EvidenceStrength.Exact,
            Source = range ?? CritterStackSource.EvidenceFor(method, scope.Adapter, scope.Project, EvidenceStrength.Exact).Source,
            Explanation = Explanation(kinds.Trigger, kinds.Declared, endpoint)
        };

    static (bool Resolved, string? Name) DeclaredName(AttributeData attribute, DotNetProjectCompilation project)
    {
        var enumType = project.Compilation.GetTypeByMetadataName(WellKnownTypes.JasperFxSlicePattern);
        var argument = attribute.ConstructorArguments.FirstOrDefault();
        if (!IsSlicePatternArgument(attribute, argument, enumType, project))
        {
            return (false, null);
        }

        var name = enumType!.GetMembers().OfType<IFieldSymbol>()
            .FirstOrDefault(field => field.HasConstantValue && Equals(field.ConstantValue, argument.Value))?.Name;
        return (true, name);
    }

    static bool IsSlicePatternArgument(AttributeData attribute, TypedConstant argument, INamedTypeSymbol? enumType, DotNetProjectCompilation project) =>
        enumType is not null && WolverineSymbolAuthority.IsAuthoredOrMetadataSymbol(enumType, project) &&
        attribute.AttributeConstructor?.Parameters is [{ Type: var parameterType }] &&
        SymbolEqualityComparer.Default.Equals(parameterType, enumType) &&
        argument.Kind == TypedConstantKind.Enum && SymbolEqualityComparer.Default.Equals(argument.Type, enumType);

    static GenerationSliceKind? Trigger(INamedTypeSymbol? request, HttpEndpoint? endpoint, DotNetProjectCompilation project) => endpoint switch
    {
        // HEAD and OPTIONS are safe verbs that are still analyzed for effects, so the verb
        // proves neither a view nor a state change; a declaration may classify them.
        null => IsAuthoredTimeout(request, project) ? GenerationSliceKind.Automation : null,
        { IsQuery: true } => GenerationSliceKind.StateView,
        { IsUnclassifiedSafeVerb: true } => null,
        _ => GenerationSliceKind.StateChange
    };

    static string Explanation(GenerationSliceKind? trigger, GenerationSliceKind? declared, HttpEndpoint? endpoint)
    {
        if (trigger is null)
        {
            return $"Exact Wolverine SlicePattern declaration classifies the slice as {declared}";
        }

        var proven = $"Proven {(endpoint is null ? "TimeoutMessage scheduler" : $"HTTP {endpoint.Verb}")} trigger classifies the slice as {trigger}";
        return declared is null ? proven : $"{proven}; declared SlicePattern={declared}";
    }

    static AttributeData? Declaration(IMethodSymbol method, DotNetProjectCompilation project) =>
        DeclarationSites(method).Select(site => AttributeOn(site, project)).FirstOrDefault(attribute => attribute is not null);

    static IEnumerable<ISymbol> DeclarationSites(IMethodSymbol method)
    {
        for (var current = method; current is not null; current = current.OverriddenMethod)
        {
            yield return current;
        }

        for (var current = method.ContainingType; current is not null; current = current.BaseType)
        {
            yield return current;
        }
    }

    static AttributeData? AttributeOn(ISymbol symbol, DotNetProjectCompilation project)
    {
        var definition = project.Compilation.GetTypeByMetadataName(WellKnownTypes.WolverineSlicePatternAttribute);
        return definition is null || !WolverineSymbolAuthority.IsAuthoredOrMetadataSymbol(definition, project)
            ? null
            : symbol.GetAttributes().FirstOrDefault(attribute =>
                SymbolEqualityComparer.Default.Equals(attribute.AttributeClass, definition) &&
                (attribute.ApplicationSyntaxReference is null ||
                 (project.AuthoredSyntaxTrees.Contains(attribute.ApplicationSyntaxReference.SyntaxTree) &&
                  !DotNetGeneratedSource.IsGenerated(attribute.ApplicationSyntaxReference.SyntaxTree))));
    }

    static bool IsAuthoredTimeout(INamedTypeSymbol? request, DotNetProjectCompilation project)
    {
        for (var current = request; current is not null; current = current.BaseType)
        {
            if (!WolverineSymbolAuthority.IsAuthoredOrMetadataSymbol(current, project))
            {
                return false;
            }

            if (DotNetSubjectIds.MetadataName(current) == WellKnownTypes.WolverineTimeoutMessage)
            {
                return true;
            }
        }

        return false;
    }
}
