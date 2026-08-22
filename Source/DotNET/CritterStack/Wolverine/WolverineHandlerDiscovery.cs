// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Cratis.Screenplay.Generation;
using Cratis.Screenplay.Generation.DotNet;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Cratis.CritterStack.Screenplay.Wolverine;

static class WolverineHandlerDiscovery
{
    public static WolverineHandlerDiscoveryResult Discover(DotNetProjectCompilation project)
    {
        var calls = ConfigurationCalls(project).ToArray();
        var explicitTypes = new List<INamedTypeSymbol>();
        var diagnostics = new List<GenerationDiagnostic>();
        var disableValues = new List<bool>();
        var conventionalDiscoveryResolvable = true;

        foreach (var call in calls)
        {
            switch (call.Method.Name)
            {
                case "DisableConventionalDiscovery":
                    if (TryGetDisabledValue(call, out var disabled))
                    {
                        disableValues.Add(disabled);
                    }
                    else
                    {
                        conventionalDiscoveryResolvable = false;
                        diagnostics.Add(UnresolvedDiagnostic(project, call, "the enabled state is not a compile-time constant"));
                    }
                    break;
                case "IncludeType":
                    if (TryGetIncludedType(call, out var includedType) && IsAuthoredSourceType(includedType))
                    {
                        if (!explicitTypes.Exists(_ => SymbolEqualityComparer.Default.Equals(_, includedType)))
                        {
                            explicitTypes.Add(includedType);
                        }
                    }
                    else
                    {
                        diagnostics.Add(UnresolvedDiagnostic(project, call, "the included handler type is not authored source in this compilation"));
                    }
                    break;
                case "IncludeAssembly":
                    if (!ReferencesCurrentAssembly(project, call))
                    {
                        diagnostics.Add(UnresolvedDiagnostic(project, call, "assembly scanning outside the current source compilation is not supported"));
                    }
                    break;
                case "CustomizeHandlerDiscovery":
                    conventionalDiscoveryResolvable = false;
                    diagnostics.Add(UnresolvedDiagnostic(project, call, "custom handler predicates and lambdas are not statically interpreted"));
                    break;
                case "IgnoreAssembly":
                    conventionalDiscoveryResolvable = false;
                    diagnostics.Add(UnresolvedDiagnostic(project, call, "assembly removal cannot be resolved from the current source compilation"));
                    break;
            }
        }

        var distinctDisableValues = disableValues.Distinct().ToArray();
        if (distinctDisableValues.Length > 1)
        {
            conventionalDiscoveryResolvable = false;
            var call = calls.First(_ => _.Method.Name == "DisableConventionalDiscovery");
            diagnostics.Add(UnresolvedDiagnostic(project, call, "conflicting enabled states depend on runtime execution order"));
        }

        var conventionalDiscoveryEnabled = distinctDisableValues.Length == 0 || !distinctDisableValues[0];
        var policy = new WolverineHandlerDiscoveryPolicy(
            conventionalDiscoveryEnabled,
            conventionalDiscoveryResolvable,
            explicitTypes);

        return new(policy, diagnostics);
    }

    static IEnumerable<ConfigurationCall> ConfigurationCalls(DotNetProjectCompilation project)
    {
        foreach (var tree in project.Compilation.SyntaxTrees
                     .Where(_ => !DotNetGeneratedSource.IsGenerated(_))
                     .OrderBy(_ => _.FilePath, StringComparer.Ordinal))
        {
            var semanticModel = project.Compilation.GetSemanticModel(tree);
            foreach (var invocation in tree.GetRoot()
                         .DescendantNodes()
                         .OfType<InvocationExpressionSyntax>()
                         .OrderBy(_ => _.SpanStart))
            {
                if (semanticModel.GetSymbolInfo(invocation).Symbol is not IMethodSymbol method ||
                    DotNetSubjectIds.MetadataName(method.ContainingType) != WellKnownTypes.WolverineHandlerDiscovery)
                {
                    continue;
                }

                yield return new(invocation, method, semanticModel);
            }
        }
    }

    static bool TryGetDisabledValue(ConfigurationCall call, out bool disabled)
    {
        if (call.Invocation.ArgumentList.Arguments.Count == 0)
        {
            disabled = true;
            return true;
        }

        var constant = call.SemanticModel.GetConstantValue(call.Invocation.ArgumentList.Arguments[0].Expression);
        if (constant is { HasValue: true, Value: bool value })
        {
            disabled = value;
            return true;
        }

        disabled = false;
        return false;
    }

    static bool TryGetIncludedType(ConfigurationCall call, out INamedTypeSymbol type)
    {
        var genericName = call.Invocation.Expression switch
        {
            GenericNameSyntax direct => direct,
            MemberAccessExpressionSyntax { Name: GenericNameSyntax member } => member,
            _ => null
        };
        var syntaxType = genericName?.TypeArgumentList.Arguments.FirstOrDefault();
        if (syntaxType is not null && call.SemanticModel.GetTypeInfo(syntaxType).Type is INamedTypeSymbol typeArgument)
        {
            type = typeArgument;
            return true;
        }

        var typeOf = call.Invocation.ArgumentList.Arguments
            .Select(_ => _.Expression)
            .OfType<TypeOfExpressionSyntax>()
            .FirstOrDefault();
        if (typeOf is not null && call.SemanticModel.GetTypeInfo(typeOf.Type).Type is INamedTypeSymbol typeOfArgument)
        {
            type = typeOfArgument;
            return true;
        }

        type = null!;
        return false;
    }

    static bool ReferencesCurrentAssembly(DotNetProjectCompilation project, ConfigurationCall call)
    {
        var typeOf = call.Invocation.ArgumentList.Arguments
            .SelectMany(_ => _.Expression.DescendantNodesAndSelf().OfType<TypeOfExpressionSyntax>())
            .FirstOrDefault();
        return typeOf is not null &&
               call.SemanticModel.GetTypeInfo(typeOf.Type).Type is INamedTypeSymbol markerType &&
               SymbolEqualityComparer.Default.Equals(markerType.ContainingAssembly, project.Compilation.Assembly);
    }

    static bool IsAuthoredSourceType(INamedTypeSymbol type) => type.Locations.Any(_ =>
        _.IsInSource &&
        _.SourceTree is not null &&
        !DotNetGeneratedSource.IsGenerated(_.SourceTree));

    static GenerationDiagnostic UnresolvedDiagnostic(
        DotNetProjectCompilation project,
        ConfigurationCall call,
        string reason)
    {
        var containingType = call.SemanticModel.GetEnclosingSymbol(call.Invocation.SpanStart)?.ContainingType;
        var subject = containingType is null
            ? new SubjectId { Value = $"dotnet://{project.Name}/#wolverine-handler-discovery" }
            : project.SubjectForType(containingType);
        return new()
        {
            Code = WolverineDiagnosticCodes.HandlerDiscoveryConfigurationUnresolved,
            Severity = GenerationDiagnosticSeverity.Warning,
            Message = $"Wolverine handler discovery call '{call.Method.Name}' was not applied because {reason}",
            Source = DotNetSource.Range(call.Invocation.GetLocation(), project.SourceRoot),
            Subject = subject
        };
    }

    sealed record ConfigurationCall(
        InvocationExpressionSyntax Invocation,
        IMethodSymbol Method,
        SemanticModel SemanticModel);
}

sealed record WolverineHandlerDiscoveryResult(
    WolverineHandlerDiscoveryPolicy Policy,
    IReadOnlyList<GenerationDiagnostic> Diagnostics);

sealed class WolverineHandlerDiscoveryPolicy(
    bool conventionalDiscoveryEnabled,
    bool conventionalDiscoveryResolvable,
    IReadOnlyList<INamedTypeSymbol> explicitTypes)
{
    public bool Includes(INamedTypeSymbol type)
    {
        if (explicitTypes.Any(_ => SymbolEqualityComparer.Default.Equals(_, type)))
        {
            return true;
        }

        if (!conventionalDiscoveryEnabled || !conventionalDiscoveryResolvable)
        {
            return false;
        }

        return type.Name.EndsWith("Handler", StringComparison.Ordinal) ||
               type.Name.EndsWith("Consumer", StringComparison.Ordinal) ||
               DotNetSymbols.Implements(type, WellKnownTypes.WolverineHandlerInterface) ||
               HasHandlerAttribute(type) ||
               type.GetMembers().OfType<IMethodSymbol>().Any(HasHandlerAttribute);
    }

    bool HasHandlerAttribute(ISymbol symbol) =>
        DotNetSymbols.HasAttribute(symbol, WellKnownTypes.WolverineHandlerAttribute) ||
        DotNetSymbols.HasAttribute(symbol, WellKnownTypes.WolverineLegacyHandlerAttribute);
}
