// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Cratis.Screenplay.Generation;
using Cratis.Screenplay.Generation.DotNet;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Cratis.CritterStack.Screenplay.Wolverine;

static class WolverineHandlerDiscovery
{
    public static WolverineHandlerDiscoveryResult Discover(
        DotNetProjectCompilation project,
        CritterStackSubjectResolver subjects)
    {
        var calls = ConfigurationCalls(project).ToArray();
        var state = new DiscoveryState(project, subjects);
        state.Diagnostics.AddRange(WolverineConventionAlterationDiscovery.Discover(project, subjects));

        foreach (var call in calls)
        {
            ApplyCall(state, call);
        }

        var distinctDisableValues = state.DisableValues.Distinct().ToArray();
        if (distinctDisableValues.Length > 1)
        {
            state.ConventionalDiscoveryResolvable = false;
            var call = calls.First(_ => _.Method.Name == "DisableConventionalDiscovery");
            state.Report(call, "conflicting enabled states depend on runtime execution order", GenerationDiagnosticOutcome.Conflict);
        }

        var conventionalDiscoveryEnabled = distinctDisableValues.Length == 0 || !distinctDisableValues[0];
        var policy = new WolverineHandlerDiscoveryPolicy(
            conventionalDiscoveryEnabled,
            state.ConventionalDiscoveryResolvable,
            state.ExplicitTypes);

        return new(policy, state.Diagnostics);
    }

    static void ApplyCall(DiscoveryState state, ConfigurationCall call)
    {
        switch (call.Method.Name)
        {
            case "DisableConventionalDiscovery":
                ApplyDisable(state, call);
                break;
            case "IncludeType":
                ApplyIncludeType(state, call);
                break;
            case "IncludeAssembly":
                if (!ReferencesCurrentAssembly(state.Project, call))
                {
                    state.Report(call, "assembly scanning outside the current source compilation is not supported");
                }

                break;
            case "CustomizeHandlerDiscovery":
                state.ConventionalDiscoveryResolvable = false;
                state.Report(call, "custom handler predicates and lambdas are not statically interpreted");
                break;
            case "IgnoreAssembly":
                state.ConventionalDiscoveryResolvable = false;
                state.Report(call, "assembly removal cannot be resolved from the current source compilation");
                break;
        }
    }

    static void ApplyDisable(DiscoveryState state, ConfigurationCall call)
    {
        if (TryGetDisabledValue(call, out var disabled))
        {
            state.DisableValues.Add(disabled);
            return;
        }

        state.ConventionalDiscoveryResolvable = false;
        state.Report(call, "the enabled state is not a compile-time constant");
    }

    static void ApplyIncludeType(DiscoveryState state, ConfigurationCall call)
    {
        if (!TryGetIncludedType(call, out var includedType) || !IsAuthoredSourceType(includedType, state.Project))
        {
            state.Report(call, "the included handler type is not authored source in this compilation");
            return;
        }

        if (!state.ExplicitTypes.Exists(_ => SymbolEqualityComparer.Default.Equals(_, includedType)))
        {
            state.ExplicitTypes.Add(includedType);
        }
    }

    static IEnumerable<ConfigurationCall> ConfigurationCalls(DotNetProjectCompilation project)
    {
        foreach (var tree in project.Compilation.SyntaxTrees
                     .Where(_ => project.AuthoredSyntaxTrees.Contains(_) && !DotNetGeneratedSource.IsGenerated(_))
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
        var found = TryGetGenericTypeArgument(call) ?? TryGetTypeOfArgument(call);
        type = found!;
        return found is not null;
    }

    static INamedTypeSymbol? TryGetGenericTypeArgument(ConfigurationCall call)
    {
        var genericName = call.Invocation.Expression switch
        {
            GenericNameSyntax direct => direct,
            MemberAccessExpressionSyntax { Name: GenericNameSyntax member } => member,
            _ => null
        };
        var syntaxType = genericName?.TypeArgumentList.Arguments.FirstOrDefault();
        return syntaxType is null ? null : call.SemanticModel.GetTypeInfo(syntaxType).Type as INamedTypeSymbol;
    }

    static INamedTypeSymbol? TryGetTypeOfArgument(ConfigurationCall call)
    {
        var typeOf = call.Invocation.ArgumentList.Arguments
            .Select(_ => _.Expression)
            .OfType<TypeOfExpressionSyntax>()
            .FirstOrDefault();
        return typeOf is null ? null : call.SemanticModel.GetTypeInfo(typeOf.Type).Type as INamedTypeSymbol;
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

    static bool IsAuthoredSourceType(INamedTypeSymbol type, DotNetProjectCompilation project) => type.Locations.Any(_ =>
        _.IsInSource &&
        _.SourceTree is not null &&
        project.AuthoredSyntaxTrees.Contains(_.SourceTree) &&
        !DotNetGeneratedSource.IsGenerated(_.SourceTree));

    sealed record ConfigurationCall(
        InvocationExpressionSyntax Invocation,
        IMethodSymbol Method,
        SemanticModel SemanticModel);

    sealed class DiscoveryState(DotNetProjectCompilation project, CritterStackSubjectResolver subjects)
    {
        public DotNetProjectCompilation Project { get; } = project;

        public List<INamedTypeSymbol> ExplicitTypes { get; } = [];

        public List<GenerationDiagnostic> Diagnostics { get; } = [];

        public List<bool> DisableValues { get; } = [];

        public bool ConventionalDiscoveryResolvable { get; set; } = true;

        public void Report(
            ConfigurationCall call,
            string reason,
            GenerationDiagnosticOutcome outcome = GenerationDiagnosticOutcome.Unknown)
        {
            var containingType = call.SemanticModel.GetEnclosingSymbol(call.Invocation.SpanStart)?.ContainingType;
            var subject = containingType is null
                ? new SubjectId { Value = $"dotnet://{Project.Name}/#wolverine-handler-discovery" }
                : subjects.SubjectForType(Project, containingType);
            Diagnostics.Add(new()
            {
                Code = WolverineDiagnosticCodes.HandlerDiscoveryConfigurationUnresolved,
                Severity = GenerationDiagnosticSeverity.Warning,
                Outcome = outcome,
                Message = $"Wolverine handler discovery call '{call.Method.Name}' was not applied because {reason}",
                Source = CritterStackSource.RangeForProject(call.Invocation.GetLocation(), Project),
                Subject = subject
            });
        }
    }
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

        return conventionalDiscoveryEnabled && conventionalDiscoveryResolvable && FollowsHandlerConvention(type);
    }

    static bool FollowsHandlerConvention(INamedTypeSymbol type) =>
        HasHandlerSuffix(type) ||
        DotNetSymbols.IsOrInheritsFrom(type, WellKnownTypes.WolverineSaga) ||
        DotNetSymbols.Implements(type, WellKnownTypes.WolverineHandlerInterface) ||
        HasHandlerAttribute(type) ||
        type.GetMembers().OfType<IMethodSymbol>().Any(HasHandlerAttribute);

    static bool HasHandlerSuffix(INamedTypeSymbol type) =>
        type.Name.EndsWith("Handler", StringComparison.Ordinal) ||
        type.Name.EndsWith("Consumer", StringComparison.Ordinal);

    static bool HasHandlerAttribute(ISymbol symbol) =>
        DotNetSymbols.HasAttribute(symbol, WellKnownTypes.WolverineHandlerAttribute) ||
        DotNetSymbols.HasAttribute(symbol, WellKnownTypes.WolverineLegacyHandlerAttribute);
}
