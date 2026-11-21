// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Cratis.Screenplay.Generation;
using Cratis.Screenplay.Generation.DotNet;
using Microsoft.CodeAnalysis;

namespace Cratis.CritterStack.Screenplay.Wolverine;

internal static partial class WolverineFacts
{
    static bool HasReadModelHandlerConsequences(
        WolverineFactScope scope,
        IMethodSymbol method,
        bool hasAutomationConsequences)
    {
        if (hasAutomationConsequences)
        {
            return true;
        }

        return method.Parameters.Any(parameter => IsReadModelParameter(parameter, scope.Project));
    }

    static bool HasWritableAggregateWorkflow(IMethodSymbol method, DotNetProjectCompilation project)
    {
        if (!IsAggregateWorkflow(method))
        {
            return false;
        }

        if (!method.Parameters.Any(parameter => IsReadModelParameter(parameter, project)))
        {
            return true;
        }

        return AggregateParameter(method, RequestParameter(method, project), aggregateWorkflow: true, project) is not null;
    }

    static bool IsReadModelParameter(IParameterSymbol parameter, DotNetProjectCompilation project) =>
        WolverineReadModelParameters.ReadAttribute(parameter, project) is not null;

    static bool IsStateInputParameter(IParameterSymbol parameter, DotNetProjectCompilation project)
    {
        if (IsReadModelParameter(parameter, project))
        {
            return true;
        }

        if (IsPersistenceBoundParameter(parameter))
        {
            return true;
        }

        return WolverineDcb.HasAttributedParameter(parameter, project);
    }

    static void AddReadModelParameterFacts(WolverineFactScope scope, SubjectId sourceSubject, IMethodSymbol method)
    {
        foreach (var parameter in method.Parameters)
        {
            var attribute = WolverineReadModelParameters.ReadAttribute(parameter, scope.Project);
            if (attribute is null)
            {
                continue;
            }

            if (parameter.Type is INamedTypeSymbol modelType)
            {
                AddReadModelParameterFact(scope, sourceSubject, new(parameter, modelType, attribute));
            }
        }
    }

    static void AddReadModelParameterFact(
        WolverineFactScope scope,
        SubjectId sourceSubject,
        ReadModelInput input)
    {
        var (parameter, modelType, attribute) = input;
        var modelSubject = scope.Subjects.SubjectForType(scope.Project, modelType);
        var discriminator = $"read-model:{parameter.Ordinal}:{parameter.Name}";
        var evidence = new Evidence
        {
            Adapter = scope.Adapter,
            Strength = EvidenceStrength.Exact,
            Source = WolverineReadModelParameters.SourceOf(attribute, scope.Project),
            Explanation = $"Wolverine reads '{modelType.Name}' through read-only aggregate parameter '{parameter.Name}'"
        };
        AddAggregateArtifact(scope, modelSubject, modelType, evidence);
        scope.Facts.Add(Relationship(
            $"wolverine:reads:{sourceSubject.Value}:{discriminator}:{modelSubject.Value}",
            sourceSubject,
            RelationshipKind.Reads,
            modelSubject,
            evidence,
            discriminator: discriminator));
    }

    static void AddConsistentAggregateDiagnostics(WolverineFactScope scope, StateChangeShape shape)
    {
        var method = shape.Method;
        AddConsistentAggregateDiagnostic(scope, method, method);
        AddConsistentAggregateDiagnostic(scope, method, method.ContainingType);
        foreach (var parameter in method.Parameters)
        {
            if (shape.ParameterBindings.Any(binding => SymbolEqualityComparer.Default.Equals(binding.Parameter, parameter)))
            {
                continue;
            }

            AddConsistentAggregateDiagnostic(scope, method, parameter);
        }
    }

    static void AddConsistentAggregateDiagnostic(WolverineFactScope scope, IMethodSymbol method, ISymbol symbol)
    {
        var attribute = WolverineReadModelParameters.ConsistentAttribute(symbol, scope.Project);
        if (attribute is null)
        {
            return;
        }

        if (!WolverineReadModelParameters.EnforcesConsistency(attribute))
        {
            return;
        }

        var source = WolverineReadModelParameters.SourceOf(attribute, scope.Project);
        if (scope.Diagnostics.Where(diagnostic => diagnostic.Code == WolverineDiagnosticCodes.StreamVersionOmitted).Any(diagnostic => diagnostic.Source == source))
        {
            return;
        }

        scope.Diagnostics.Add(new GenerationDiagnostic
        {
            Code = WolverineDiagnosticCodes.StreamVersionOmitted,
            Severity = GenerationDiagnosticSeverity.Information,
            Outcome = GenerationDiagnosticOutcome.Unsupported,
            Message = $"The always-enforced consistency metadata for '{method.Name}' cannot be represented exactly by Screenplay concurrency",
            Source = source,
            Subject = scope.Subjects.SubjectForType(scope.Project, method.ContainingType)
        });
    }

    sealed record ReadModelInput(IParameterSymbol Parameter, INamedTypeSymbol ModelType, AttributeData Attribute);
}
