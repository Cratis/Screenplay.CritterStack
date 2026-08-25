// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Cratis.Screenplay.Generation;
using Cratis.Screenplay.Generation.DotNet;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Cratis.CritterStack.Screenplay.Marten;

sealed record MartenCompiledQueryPlan(
    INamedTypeSymbol PlanType,
    INamedTypeSymbol DocumentType,
    ITypeSymbol OutputType,
    IReadOnlyList<PropertyDefinition> Parameters);

sealed record MartenCompiledQueryLink(
    INamedTypeSymbol PlanType,
    INamedTypeSymbol DocumentType,
    ITypeSymbol OutputType,
    IReadOnlyList<PropertyDefinition> Parameters,
    Evidence Evidence);

sealed record MartenCompiledQueryDiscoveryResult(
    IReadOnlyList<MartenCompiledQueryLink> Links,
    IReadOnlyList<GenerationDiagnostic> Diagnostics);

static class MartenCompiledQueryDiscovery
{
    static readonly HashSet<string> _executionMethods = ["Query", "QueryAsync", "QueryByPlan", "QueryByPlanAsync"];
    static readonly HashSet<string> _executionOwners =
    [
        WellKnownTypes.MartenQuerySession,
        WellKnownTypes.MartenBatchedQuery
    ];

    public static MartenCompiledQueryDiscoveryResult Discover(
        IMethodSymbol entryPoint,
        SubjectId entryPointSubject,
        DotNetProjectCompilation project,
        AdapterIdentity adapter)
    {
        var links = new List<MartenCompiledQueryLink>();
        var diagnostics = new List<GenerationDiagnostic>();
        foreach (var syntaxReference in entryPoint.DeclaringSyntaxReferences)
        {
            if (syntaxReference.GetSyntax() is not MethodDeclarationSyntax declaration ||
                DotNetGeneratedSource.IsGenerated(declaration.SyntaxTree))
            {
                continue;
            }

            var semanticModel = project.Compilation.GetSemanticModel(declaration.SyntaxTree);
            foreach (var invocation in declaration.DescendantNodes().OfType<InvocationExpressionSyntax>())
            {
                if (!TryResolve(invocation, semanticModel, out var plan))
                {
                    continue;
                }

                if (!IsInProvenEndpointFlow(invocation, declaration, semanticModel))
                {
                    diagnostics.Add(new GenerationDiagnostic
                    {
                        Code = MartenDiagnosticCodes.CompiledQueryFlowUnresolved,
                        Severity = GenerationDiagnosticSeverity.Warning,
                        Outcome = GenerationDiagnosticOutcome.Unknown,
                        Message = $"Marten compiled query '{plan.PlanType.Name}' is inside a nested executable scope whose invocation from endpoint '{entryPoint.ContainingType.Name}.{entryPoint.Name}' could not be proven",
                        Source = CritterStackSource.RangeForProject(invocation.GetLocation(), project),
                        Subject = entryPointSubject
                    });
                    continue;
                }

                links.Add(new(
                    plan.PlanType,
                    plan.DocumentType,
                    plan.OutputType,
                    plan.Parameters,
                    new Evidence
                    {
                        Adapter = adapter,
                        Strength = EvidenceStrength.Exact,
                        Source = CritterStackSource.RangeForProject(invocation.GetLocation(), project),
                        Explanation = $"Marten compiled query '{plan.PlanType.Name}' is executed by application query entry point '{entryPoint.ContainingType.Name}.{entryPoint.Name}'"
                    }));
            }
        }

        return new(links, diagnostics);
    }

    public static bool TryResolve(
        InvocationExpressionSyntax invocation,
        SemanticModel semanticModel,
        out MartenCompiledQueryPlan plan)
    {
        plan = null!;
        if (semanticModel.GetSymbolInfo(invocation).Symbol is not IMethodSymbol method ||
            !IsCompiledQueryExecution(method, out var compiledParameter))
        {
            return false;
        }

        var argument = ArgumentFor(invocation, method, compiledParameter);
        var argumentType = argument is null ? default : semanticModel.GetTypeInfo(argument.Expression);
        if (argumentType.Type is not INamedTypeSymbol planType)
        {
            return false;
        }

        var selectedCompiledInterface = CompiledInterfaceOf(argumentType.ConvertedType ?? compiledParameter.Type);
        var compiledInterface = selectedCompiledInterface is null
            ? null
            : CompiledInterfaceOf(planType, selectedCompiledInterface);
        if (compiledInterface is null || !HasAuthoredQueryPlan(planType, compiledInterface) ||
            compiledInterface.TypeArguments[0] is not INamedTypeSymbol documentType ||
            documentType.TypeKind == TypeKind.Error ||
            !documentType.Locations.Any(_ => _.IsInSource))
        {
            return false;
        }

        plan = new(
            planType,
            documentType,
            compiledInterface.TypeArguments[1],
            ParametersOf(planType));
        return true;
    }

    public static bool IsCompiledQueryExecution(IMethodSymbol method) =>
        IsCompiledQueryExecution(method, out _);

    static bool IsCompiledQueryExecution(IMethodSymbol method, out IParameterSymbol compiledParameter)
    {
        compiledParameter = null!;
        if (!_executionMethods.Contains(method.Name))
        {
            return false;
        }

        var candidate = method.ReducedFrom ?? method;
        var owner = DotNetSubjectIds.MetadataName(candidate.ContainingType.OriginalDefinition);
        if (!_executionOwners.Contains(owner))
        {
            return false;
        }

        compiledParameter = method.Parameters.FirstOrDefault(parameter => IsCompiledQueryType(parameter.Type))!;
        return compiledParameter is not null;
    }

    static ArgumentSyntax? ArgumentFor(
        InvocationExpressionSyntax invocation,
        IMethodSymbol method,
        IParameterSymbol parameter)
    {
        var parameterIndex = method.Parameters.IndexOf(parameter);
        if (parameterIndex < 0)
        {
            return null;
        }

        var named = invocation.ArgumentList.Arguments.FirstOrDefault(_ =>
            _.NameColon is not null && string.Equals(_.NameColon.Name.Identifier.ValueText, parameter.Name, StringComparison.Ordinal));
        return named ?? (parameterIndex < invocation.ArgumentList.Arguments.Count
            ? invocation.ArgumentList.Arguments[parameterIndex]
            : null);
    }

    static bool IsCompiledQueryType(ITypeSymbol type) =>
        type is INamedTypeSymbol named &&
        (DotNetSubjectIds.MetadataName(named.OriginalDefinition) == WellKnownTypes.MartenCompiledQuery ||
         named.AllInterfaces.Any(_ => DotNetSubjectIds.MetadataName(_.OriginalDefinition) == WellKnownTypes.MartenCompiledQuery));

    static INamedTypeSymbol? CompiledInterfaceOf(ITypeSymbol type)
    {
        if (type is not INamedTypeSymbol named)
        {
            return null;
        }

        var candidates = named.AllInterfaces
            .Concat([named])
            .Where(_ => DotNetSubjectIds.MetadataName(_.OriginalDefinition) == WellKnownTypes.MartenCompiledQuery)
            .Distinct(SymbolEqualityComparer.Default)
            .OfType<INamedTypeSymbol>()
            .ToArray();
        return candidates.Length == 1 ? candidates[0] : null;
    }

    static INamedTypeSymbol? CompiledInterfaceOf(INamedTypeSymbol planType, INamedTypeSymbol boundInterface)
    {
        if (SymbolEqualityComparer.Default.Equals(planType, boundInterface))
        {
            return planType;
        }

        return planType.AllInterfaces.SingleOrDefault(_ => SymbolEqualityComparer.Default.Equals(_, boundInterface));
    }

    static bool HasAuthoredQueryPlan(INamedTypeSymbol planType, INamedTypeSymbol compiledInterface)
    {
        var queryIs = compiledInterface.GetMembers("QueryIs").OfType<IMethodSymbol>().SingleOrDefault();
        var implementation = queryIs is null ? null : planType.FindImplementationForInterfaceMember(queryIs);
        return implementation is IMethodSymbol method && method.Locations.Any(IsAuthoredSourceLocation);
    }

    static IReadOnlyList<PropertyDefinition> ParametersOf(INamedTypeSymbol planType)
    {
        var members = new List<ISymbol>();
        for (var current = planType; current is not null; current = current.BaseType)
        {
            members.AddRange(current.GetMembers().Where(IsReadableParameter));
        }

        return
        [
            .. members
                .Distinct(SymbolEqualityComparer.Default)
                .OrderBy(_ => _.Name, StringComparer.Ordinal)
                .Select(_ => new PropertyDefinition
                {
                    Name = LowerFirst(_.Name),
                    Type = DotNetTypeShapes.TypeReferenceFor(TypeOf(_))
                })
        ];
    }

    static bool IsReadableParameter(ISymbol member)
    {
        if (member.IsStatic || member.DeclaredAccessibility != Accessibility.Public || HasMartenIgnore(member))
        {
            return false;
        }

        return member switch
        {
            IFieldSymbol => true,
            IPropertySymbol property => property.Parameters.Length == 0 &&
                                        property.GetMethod?.DeclaredAccessibility == Accessibility.Public,
            _ => false
        };
    }

    static bool HasMartenIgnore(ISymbol member) => member.GetAttributes().Any(_ =>
        _.AttributeClass is not null &&
        DotNetSubjectIds.MetadataName(_.AttributeClass) == WellKnownTypes.MartenIgnoreAttribute);

    static ITypeSymbol TypeOf(ISymbol member) => member switch
    {
        IFieldSymbol field => field.Type,
        IPropertySymbol property => property.Type,
        _ => throw new InvalidOperationException($"Unsupported compiled query parameter member '{member.Name}'")
    };

    static bool IsInProvenEndpointFlow(
        InvocationExpressionSyntax invocation,
        MethodDeclarationSyntax declaration,
        SemanticModel semanticModel)
    {
        var boundary = ExecutableBoundaryOf(invocation, declaration);
        return boundary is null || IsProvenBoundary(boundary, declaration, semanticModel, []);
    }

    static SyntaxNode? ExecutableBoundaryOf(SyntaxNode node, MethodDeclarationSyntax declaration) => node
        .Ancestors()
        .TakeWhile(_ => _ != declaration)
        .FirstOrDefault(_ => _ is LocalFunctionStatementSyntax or AnonymousFunctionExpressionSyntax);

    static bool IsProvenBoundary(
        SyntaxNode boundary,
        MethodDeclarationSyntax declaration,
        SemanticModel semanticModel,
        HashSet<SyntaxNode> visited)
    {
        if (!visited.Add(boundary))
        {
            return false;
        }

        return boundary switch
        {
            LocalFunctionStatementSyntax localFunction => IsProvenLocalFunction(localFunction, declaration, semanticModel, visited),
            AnonymousFunctionExpressionSyntax anonymousFunction => IsImmediatelyInvoked(anonymousFunction, declaration, semanticModel, visited),
            _ => false
        };
    }

    static bool IsProvenLocalFunction(
        LocalFunctionStatementSyntax localFunction,
        MethodDeclarationSyntax declaration,
        SemanticModel semanticModel,
        HashSet<SyntaxNode> visited)
    {
        if (semanticModel.GetDeclaredSymbol(localFunction) is not IMethodSymbol localFunctionSymbol)
        {
            return false;
        }

        return declaration.DescendantNodes().OfType<InvocationExpressionSyntax>().Any(invocation =>
            semanticModel.GetSymbolInfo(invocation).Symbol is IMethodSymbol invokedMethod &&
            SymbolEqualityComparer.Default.Equals(invokedMethod.OriginalDefinition, localFunctionSymbol.OriginalDefinition) &&
            IsProvenCallSite(invocation, declaration, semanticModel, visited));
    }

    static bool IsImmediatelyInvoked(
        AnonymousFunctionExpressionSyntax anonymousFunction,
        MethodDeclarationSyntax declaration,
        SemanticModel semanticModel,
        HashSet<SyntaxNode> visited)
    {
        SyntaxNode expression = anonymousFunction;
        while (expression.Parent is ParenthesizedExpressionSyntax or CastExpressionSyntax)
        {
            expression = expression.Parent;
        }

        return expression.Parent is InvocationExpressionSyntax invocation &&
               invocation.Expression == expression &&
               IsProvenCallSite(invocation, declaration, semanticModel, visited);
    }

    static bool IsProvenCallSite(
        InvocationExpressionSyntax invocation,
        MethodDeclarationSyntax declaration,
        SemanticModel semanticModel,
        HashSet<SyntaxNode> visited)
    {
        var boundary = ExecutableBoundaryOf(invocation, declaration);
        return boundary is null || IsProvenBoundary(boundary, declaration, semanticModel, visited);
    }

    static bool IsAuthoredSourceLocation(Location location) =>
        location.IsInSource &&
        location.SourceTree is not null &&
        !DotNetGeneratedSource.IsGenerated(location.SourceTree);

    static string LowerFirst(string value) => value.Length == 0
        ? value
        : $"{char.ToLowerInvariant(value[0])}{value[1..]}";
}
