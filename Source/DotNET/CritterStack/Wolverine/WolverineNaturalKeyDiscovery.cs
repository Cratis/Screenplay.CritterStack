// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Cratis.Screenplay.Generation;
using Microsoft.CodeAnalysis;

namespace Cratis.CritterStack.Screenplay.Wolverine;

static class WolverineNaturalKeyDiscovery
{
    public static void AddDiagnostics(WolverineFactScope scope, INamedTypeSymbol aggregate)
    {
        var subject = scope.Subjects.SubjectForType(scope.Project, aggregate);
        if (scope.Diagnostics.Exists(diagnostic => IsNaturalKeyDiagnostic(diagnostic) && diagnostic.Subject == subject))
        {
            return;
        }

        foreach (var property in aggregate.GetMembers().OfType<IPropertySymbol>())
        {
            var attribute = WolverineReadModelParameters.AttributeFor(property, scope.Project, WellKnownTypes.JasperFxNaturalKeyAttribute);
            if (attribute is not null)
            {
                scope.Diagnostics.Add(DiagnosticFor(scope, property, attribute));
            }
        }

        OrderDiagnostics(scope.Diagnostics);
    }

    static GenerationDiagnostic DiagnosticFor(WolverineFactScope scope, IPropertySymbol property, AttributeData attribute) => new()
    {
        Code = WolverineDiagnosticCodes.NaturalKeyIdentityOmitted,
        Severity = GenerationDiagnosticSeverity.Information,
        Outcome = GenerationDiagnosticOutcome.Unsupported,
        Message = $"The natural-key identity of aggregate '{property.ContainingType.Name}' property '{property.Name}' cannot be represented without guessing a stream identity; existing surrogate-id handling is retained",
        Source = WolverineReadModelParameters.SourceOf(attribute, scope.Project),
        Subject = scope.Subjects.SubjectForType(scope.Project, property.ContainingType)
    };

    static bool IsNaturalKeyDiagnostic(GenerationDiagnostic diagnostic) =>
        diagnostic.Code == WolverineDiagnosticCodes.NaturalKeyIdentityOmitted;

    static void OrderDiagnostics(List<GenerationDiagnostic> diagnostics)
    {
        var ordered = diagnostics.Where(IsNaturalKeyDiagnostic)
            .OrderBy(diagnostic => diagnostic.Subject?.Value, StringComparer.Ordinal)
            .ThenBy(diagnostic => diagnostic.Source?.Path, StringComparer.Ordinal)
            .ThenBy(diagnostic => diagnostic.Source?.StartLine)
            .ThenBy(diagnostic => diagnostic.Source?.StartColumn)
            .ToArray();
        diagnostics.RemoveAll(IsNaturalKeyDiagnostic);
        diagnostics.AddRange(ordered);
    }
}
