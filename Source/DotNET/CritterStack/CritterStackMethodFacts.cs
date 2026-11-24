// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Cratis.Screenplay.Generation;
using Cratis.Screenplay.Generation.DotNet;
using Microsoft.CodeAnalysis;

namespace Cratis.CritterStack.Screenplay;

/// <summary>
/// Emits exact executable observations for the shared method-eligibility resolver.
/// </summary>
static class CritterStackMethodFacts
{
    public static SubjectId CompatibilitySubjectFor(DotNetProjectCompilation project, IMethodSymbol method) => new()
    {
        Value = $"{project.SubjectForType(method.ContainingType).Value}#method:{Uri.EscapeDataString(method.GetDocumentationCommentId() ?? DotNetMethodIdentity.DisplayName(method))}"
    };

    public static SubjectId SubjectFor(DotNetProjectCompilation project, IMethodSymbol method)
    {
        if (method.MethodKind is not (MethodKind.AnonymousFunction or MethodKind.LocalFunction))
        {
            return CompatibilitySubjectFor(project, method);
        }

        var parent = method.ContainingSymbol is IMethodSymbol enclosing
            ? SubjectFor(project, enclosing).Value
            : $"{project.SubjectForType(method.ContainingType).Value}#member:{Uri.EscapeDataString(method.ContainingSymbol.GetDocumentationCommentId() ?? method.ContainingSymbol.MetadataName)}";
        var declaration = method.DeclaringSyntaxReferences.Single();
        return new()
        {
            Value = $"{parent}#executable:{method.MethodKind}:{declaration.Span.Start}:{declaration.Span.Length}"
        };
    }

    public static MethodFactSupportAlternative Observe(
        DotNetProjectCompilation project,
        IMethodSymbol method,
        BackendScopeKey? scope = null,
        IReadOnlyList<GenerationDiagnostic>? diagnostics = null) => new()
        {
            Method = SubjectFor(project, method),
            EnclosingExecutables = EnclosingExecutables(project, method),
            Scope = scope,
            Diagnostics = diagnostics ?? []
        };

    public static MethodFactSupportFact Support(
        GenerationFact candidate,
        IReadOnlyList<MethodFactSupportAlternative> alternatives) => new()
        {
            Id = new() { Value = $"{candidate.Id.Value}:method-support" },
            Subject = candidate.Subject,
            Evidence = candidate.Evidence,
            Definition = new()
            {
                Candidate = candidate.Id,
                Alternatives = alternatives
            }
        };

    public static MethodExecutionClassificationFact FailureOnly(
        MethodFactSupportAlternative observation,
        Evidence evidence,
        GenerationDiagnostic reason)
    {
        var subject = observation.Method;
        return new()
        {
            Id = new()
            {
                Value = BackendOccurrenceKeys.FactId(
                    $"{evidence.Adapter.Id}:failure-only:{Uri.EscapeDataString(subject.Value)}",
                    observation.Scope)
            },
            Subject = subject,
            Evidence = evidence,
            Definition = new()
            {
                Method = subject,
                Scope = observation.Scope,
                Kind = MethodExecutionKind.FailureOnly,
                OmissionReason = reason
            }
        };
    }

    static List<SubjectId> EnclosingExecutables(DotNetProjectCompilation project, IMethodSymbol method)
    {
        var subjects = new List<SubjectId>();
        for (var enclosing = method.ContainingSymbol; enclosing is not null; enclosing = enclosing.ContainingSymbol)
        {
            if (enclosing is IMethodSymbol executable)
            {
                subjects.Add(SubjectFor(project, executable));
            }
        }

        return subjects;
    }
}
