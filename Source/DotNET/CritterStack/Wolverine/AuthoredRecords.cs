// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Cratis.Screenplay.Generation;
using Cratis.Screenplay.Generation.DotNet;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Operations;

namespace Cratis.CritterStack.Screenplay.Wolverine;

sealed record AuthoredImplementation(DotNetProjectCompilation Project, IMethodSymbol Method, string Type);

sealed record AuthoredDispatch(DotNetProjectCompilation Project, INamedTypeSymbol Type, IMethodSymbol Contract);

sealed record AuthoredArgument(ITypeSymbol? RequestType, ISymbol? Identity);

sealed record AuthoredCall(IInvocationOperation Operation, InvocationExpressionSyntax Syntax, MethodDeclarationSyntax Declaration, DotNetProjectCompilation Project);

sealed class AuthoredTraversalBudget
{
    public int Calls { get; set; }
}

sealed record AuthoredFollowState(
    IReadOnlyDictionary<string, AuthoredArgument> Arguments,
    AuthoredTraversal Traversal,
    IReadOnlyList<SourceRange> Trail,
    bool Conditional,
    int Depth);

sealed class AuthoredTraversal(List<GenerationDiagnostic> diagnostics, AuthoredTraversalBudget budget, SourceRange? root, HashSet<string> path)
{
    public List<GenerationDiagnostic> Diagnostics { get; } = diagnostics;
    public AuthoredTraversalBudget Budget { get; } = budget;
    public SourceRange? Root { get; } = root;
    public HashSet<string> Path { get; } = path;
    public bool Failed { get; set; }

    public IReadOnlyList<WolverineProduction> Fail(AuthoredCall call, string reason)
    {
        Failed = true;
        var source = AuthoredCalls.SourceOf(call);
        var message = $"Cannot follow authored abstraction '{call.Operation.TargetMethod.ToDisplayString()}' because {reason}; persistence behind this call is not inferred";
        if (!Diagnostics.Exists(diagnostic => diagnostic.Code == WolverineDiagnosticCodes.AuthoredAbstractionUnresolved && diagnostic.Source == source && diagnostic.Message == message))
        {
            Diagnostics.Add(new GenerationDiagnostic
            {
                Code = WolverineDiagnosticCodes.AuthoredAbstractionUnresolved,
                Severity = GenerationDiagnosticSeverity.Warning,
                Outcome = GenerationDiagnosticOutcome.Unknown,
                Source = source,
                Message = message
            });
        }

        return [];
    }
}
