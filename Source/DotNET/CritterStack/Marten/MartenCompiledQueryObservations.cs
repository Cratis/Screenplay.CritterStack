// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Cratis.Screenplay.Generation;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Cratis.CritterStack.Screenplay.Marten;

sealed record MartenCompiledQueryObservation(
    INamedTypeSymbol PlanType,
    INamedTypeSymbol DocumentType,
    ITypeSymbol OutputType);

sealed record MartenCompiledQueryExecution(MartenCompiledQueryObservation Plan, Evidence Evidence)
{
    public required InvocationExpressionSyntax Invocation { get; init; }
}

sealed record MartenCompiledQueryUnprovenFlow(
    MartenCompiledQueryObservation Plan,
    InvocationExpressionSyntax Invocation,
    GenerationDiagnostic Diagnostic);

sealed record MartenCompiledQueryObservations(
    IReadOnlyList<MartenCompiledQueryExecution> Links,
    IReadOnlyList<GenerationDiagnostic> Diagnostics)
{
    public IReadOnlyList<MartenCompiledQueryUnprovenFlow> UnprovenFlows { get; init; } = [];
}
