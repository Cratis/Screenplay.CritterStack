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
        IMethodSymbol method,
        INamedTypeSymbol? request,
        HttpEndpoint? endpoint,
        DotNetProjectCompilation project,
        AdapterIdentity adapter,
        List<GenerationDiagnostic> diagnostics)
    {
        var attribute = Declaration(method, project);
        var source = attribute?.ApplicationSyntaxReference?.GetSyntax().GetLocation();
        var range = source is null ? null : CritterStackSource.RangeForProject(source, project);
        GenerationSliceKind? declared = null;
        if (attribute is not null)
        {
            var enumType = project.Compilation.GetTypeByMetadataName(WellKnownTypes.JasperFxSlicePattern);
            var argument = attribute.ConstructorArguments.FirstOrDefault();
            var resolved = enumType is not null && WolverineSymbolAuthority.IsAuthoredOrMetadataSymbol(enumType, project) &&
                attribute.AttributeConstructor?.Parameters is [{ Type: var parameterType }] &&
                SymbolEqualityComparer.Default.Equals(parameterType, enumType) &&
                argument.Kind == TypedConstantKind.Enum && SymbolEqualityComparer.Default.Equals(argument.Type, enumType);
            var name = resolved
                ? enumType!.GetMembers().OfType<IFieldSymbol>().FirstOrDefault(field => field.HasConstantValue && Equals(field.ConstantValue, argument.Value))?.Name
                : null;
            declared = name switch
            {
                "Command" => GenerationSliceKind.StateChange,
                "View" => GenerationSliceKind.StateView,
                "Automation" => GenerationSliceKind.Automation,
                "Translation" => GenerationSliceKind.Translate,
                _ => null
            };
            if (declared is null)
            {
                diagnostics.Add(new()
                {
                    Code = WolverineDiagnosticCodes.SlicePatternUnresolved,
                    Severity = GenerationDiagnosticSeverity.Warning,
                    Outcome = resolved ? GenerationDiagnosticOutcome.Unsupported : GenerationDiagnosticOutcome.Unknown,
                    Message = $"The SlicePattern declaration on '{DotNetMethodIdentity.DisplayName(method)}' has an {(resolved ? "undefined" : "unresolved")} value; independently inferred classification is retained",
                    Source = range,
                    Subject = DotNetMethodIdentity.SubjectFor(project, method)
                });
            }
        }

        GenerationSliceKind? trigger = IsAuthoredTimeout(request, project) ? GenerationSliceKind.Automation : null;
        if (endpoint is not null)
        {
            // HEAD and OPTIONS are safe verbs that are still analyzed for effects, so the verb
            // proves neither a view nor a state change; a declaration may classify them.
            trigger = endpoint switch
            {
                { IsQuery: true } => GenerationSliceKind.StateView,
                { IsUnclassifiedSafeVerb: true } => null,
                _ => GenerationSliceKind.StateChange
            };
        }
        if (declared is not null && trigger is not null && declared != trigger)
        {
            diagnostics.Add(new()
            {
                Code = WolverineDiagnosticCodes.SlicePatternConflict,
                Severity = GenerationDiagnosticSeverity.Warning,
                Outcome = GenerationDiagnosticOutcome.Conflict,
                Message = $"The SlicePattern declaration '{declared}' conflicts with the proven {(endpoint is null ? "TimeoutMessage" : "HTTP")} trigger '{trigger}' on '{DotNetMethodIdentity.DisplayName(method)}'; the trigger takes precedence",
                Source = range,
                Subject = DotNetMethodIdentity.SubjectFor(project, method)
            });
        }

        var kind = trigger ?? declared;
        var explanation = trigger is not null
            ? $"Proven {(endpoint is null ? "TimeoutMessage scheduler" : $"HTTP {endpoint.Verb}")} trigger classifies the slice as {trigger}"
            : $"Exact Wolverine SlicePattern declaration classifies the slice as {declared}";
        if (trigger is not null && declared is not null)
        {
            explanation += $"; declared SlicePattern={declared}";
        }

        var evidence = kind is null ? null : new Evidence
        {
            Adapter = adapter,
            Strength = EvidenceStrength.Exact,
            Source = range ?? CritterStackSource.EvidenceFor(method, adapter, project, EvidenceStrength.Exact).Source,
            Explanation = explanation
        };
        return new(kind, evidence, declared is not null);
    }

    public static void Apply(
        WolverineSlicePattern decision,
        IMethodSymbol method,
        DotNetProjectCompilation project,
        List<CritterStackPlacementIntent> placements,
        int firstPlacement,
        List<GenerationDiagnostic> diagnostics)
    {
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

    static AttributeData? Declaration(IMethodSymbol method, DotNetProjectCompilation project)
    {
        for (var current = method; current is not null; current = current.OverriddenMethod)
        {
            if (AttributeOn(current, project) is { } attribute)
            {
                return attribute;
            }
        }

        for (var current = method.ContainingType; current is not null; current = current.BaseType)
        {
            if (AttributeOn(current, project) is { } attribute)
            {
                return attribute;
            }
        }

        return null;
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
