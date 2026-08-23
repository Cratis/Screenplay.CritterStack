// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Cratis.Screenplay.Generation;
using Cratis.Screenplay.Generation.DotNet;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Cratis.CritterStack.Screenplay.Marten;

sealed record MartenMultiStreamIdentity(
    INamedTypeSymbol EventType,
    string TargetMember,
    bool IsOneToMany,
    Evidence Evidence);

sealed record MartenMultiStreamFanOut(
    INamedTypeSymbol ParentEventType,
    INamedTypeSymbol ChildEventType,
    string SourceMember,
    string Mode,
    Evidence Evidence);

sealed record MartenMultiStreamConfiguration(
    IReadOnlyList<MartenMultiStreamIdentity> Identities,
    IReadOnlyList<MartenMultiStreamFanOut> FanOuts,
    IReadOnlyList<GenerationDiagnostic> Diagnostics)
{
    public static MartenMultiStreamConfiguration Empty { get; } = new([], [], []);
}

static class MartenMultiStreamConfigurationDiscovery
{
    const string JasperFxMultiStreamProjection = "JasperFx.Events.Aggregation.JasperFxMultiStreamProjectionBase`4";
    static readonly HashSet<string> _configurationDeclaringTypes =
    [
        WellKnownTypes.MartenMultiStreamProjection,
        JasperFxMultiStreamProjection
    ];
    static readonly HashSet<string> _eventWrapperTypes =
    [
        "JasperFx.Events.IEvent`1",
        "Marten.Events.IEvent`1"
    ];

    public static MartenMultiStreamConfiguration Discover(
        DotNetProjectCompilation project,
        AdapterIdentity adapter,
        INamedTypeSymbol projection)
    {
        if (projection.BaseType is null ||
            DotNetSubjectIds.MetadataName(projection.BaseType.OriginalDefinition) != WellKnownTypes.MartenMultiStreamProjection)
        {
            return new(
                [],
                [],
                [
                Loss(
                    project,
                    projection,
                    CritterStackSource.EvidenceFor(
                        projection,
                        adapter,
                        project,
                        EvidenceStrength.Exact,
                        "The projection derives indirectly from MultiStreamProjection<T,TId>"),
                    $"Multi-stream projection '{projection.Name}' does not directly derive from the exact Marten MultiStreamProjection<T,TId> base, so inherited grouping configuration was not interpreted")
            ]);
        }

        var identities = new List<MartenMultiStreamIdentity>();
        var fanOuts = new List<MartenMultiStreamFanOut>();
        var diagnostics = new List<GenerationDiagnostic>();
        foreach (var constructor in projection.DeclaringSyntaxReferences
                     .Select(_ => _.GetSyntax())
                     .OfType<TypeDeclarationSyntax>()
                     .SelectMany(_ => _.Members.OfType<ConstructorDeclarationSyntax>()))
        {
            var semanticModel = project.Compilation.GetSemanticModel(constructor.SyntaxTree);
            foreach (var invocation in constructor.DescendantNodes().OfType<InvocationExpressionSyntax>())
            {
                if (semanticModel.GetSymbolInfo(invocation).Symbol is not IMethodSymbol method ||
                    !IsExactConfigurationMethod(method))
                {
                    continue;
                }

                var evidence = InvocationEvidence(project, adapter, invocation, method.Name);
                if (!IsTopLevelConfiguration(invocation, constructor))
                {
                    diagnostics.Add(Loss(
                        project,
                        projection,
                        evidence,
                        $"Marten {method.Name} configuration in '{projection.Name}' is conditional or nested and cannot be resolved safely"));
                    continue;
                }

                switch (method.Name)
                {
                    case "Identity":
                    case "Identities":
                        DiscoverIdentity(project, projection, invocation, method, semanticModel, evidence, identities, diagnostics);
                        break;
                    case "FanOut":
                        DiscoverFanOut(project, projection, invocation, method, semanticModel, evidence, fanOuts, diagnostics);
                        break;
                    case "CustomGrouping":
                        diagnostics.Add(Loss(
                            project,
                            projection,
                            evidence,
                            $"Multi-stream projection '{projection.Name}' uses arbitrary custom grouping through CustomGrouping and no identity mapping was inferred"));
                        break;
                    case "RollUpByTenant":
                        diagnostics.Add(Loss(
                            project,
                            projection,
                            evidence,
                            $"Multi-stream projection '{projection.Name}' groups by tenant through RollUpByTenant and no identity mapping was inferred"));
                        break;
                }
            }

            foreach (var assignment in constructor.DescendantNodes().OfType<AssignmentExpressionSyntax>())
            {
                if (semanticModel.GetSymbolInfo(assignment.Left).Symbol is not IPropertySymbol property ||
                    property.Name != "TenancyGrouping" ||
                    DotNetSubjectIds.MetadataName(property.ContainingType.OriginalDefinition) != JasperFxMultiStreamProjection)
                {
                    continue;
                }

                var evidence = new Evidence
                {
                    Adapter = adapter,
                    Strength = EvidenceStrength.Exact,
                    Source = CritterStackSource.RangeForProject(assignment.GetLocation(), project),
                    Explanation = "Marten multi-stream tenancy grouping assignment"
                };
                diagnostics.Add(Loss(
                    project,
                    projection,
                    evidence,
                    $"Multi-stream projection '{projection.Name}' configures tenancy-dependent grouping and no identity mapping was inferred from that assignment"));
            }
        }

        return new(
            [.. identities.OrderBy(_ => DotNetSubjectIds.MetadataName(_.EventType), StringComparer.Ordinal).ThenBy(_ => _.TargetMember, StringComparer.Ordinal)],
            [.. fanOuts.OrderBy(_ => DotNetSubjectIds.MetadataName(_.ParentEventType), StringComparer.Ordinal).ThenBy(_ => DotNetSubjectIds.MetadataName(_.ChildEventType), StringComparer.Ordinal).ThenBy(_ => _.SourceMember, StringComparer.Ordinal)],
            [.. diagnostics.OrderBy(_ => _.Source?.Path, StringComparer.Ordinal).ThenBy(_ => _.Source?.StartLine).ThenBy(_ => _.Message, StringComparer.Ordinal)]);
    }

    static void DiscoverIdentity(
        DotNetProjectCompilation project,
        INamedTypeSymbol projection,
        InvocationExpressionSyntax invocation,
        IMethodSymbol method,
        SemanticModel semanticModel,
        Evidence evidence,
        List<MartenMultiStreamIdentity> identities,
        List<GenerationDiagnostic> diagnostics)
    {
        if (method.TypeArguments is not [INamedTypeSymbol configuredEventType] ||
            invocation.ArgumentList.Arguments.FirstOrDefault()?.Expression is not LambdaExpressionSyntax lambda ||
            !TryMemberPath(lambda, semanticModel, out var path, out var lambdaParameterType))
        {
            diagnostics.Add(Loss(
                project,
                projection,
                evidence,
                $"Marten {method.Name} configuration in '{projection.Name}' is not a simple member-selector lambda and no identity mapping was inferred"));
            return;
        }

        if (path.Split('.').Any(_ => string.Equals(_, "tenantId", StringComparison.OrdinalIgnoreCase)))
        {
            diagnostics.Add(Loss(
                project,
                projection,
                evidence,
                $"Marten {method.Name} configuration in '{projection.Name}' depends on tenant identity and no identity mapping was inferred"));
            return;
        }

        var eventType = NormalizeEventType(configuredEventType);
        path = NormalizeWrapperPath(path, lambdaParameterType);
        if (path.Length == 0)
        {
            diagnostics.Add(Loss(
                project,
                projection,
                evidence,
                $"Marten {method.Name} configuration in '{projection.Name}' does not select a member of the authored event and no identity mapping was inferred"));
            return;
        }

        identities.Add(new(
            eventType,
            path,
            method.Name == "Identities",
            evidence));
    }

    static void DiscoverFanOut(
        DotNetProjectCompilation project,
        INamedTypeSymbol projection,
        InvocationExpressionSyntax invocation,
        IMethodSymbol method,
        SemanticModel semanticModel,
        Evidence evidence,
        List<MartenMultiStreamFanOut> fanOuts,
        List<GenerationDiagnostic> diagnostics)
    {
        if (method.TypeArguments is not [INamedTypeSymbol parentEventType, INamedTypeSymbol childEventType] ||
            invocation.ArgumentList.Arguments.FirstOrDefault()?.Expression is not LambdaExpressionSyntax lambda ||
            !TryMemberPath(lambda, semanticModel, out var path, out var lambdaParameterType) ||
            !TryFanOutMode(invocation, semanticModel, out var mode))
        {
            diagnostics.Add(Loss(
                project,
                projection,
                evidence,
                $"Marten FanOut configuration in '{projection.Name}' is not an exact declaration with a simple member-selector lambda and no fan-out mapping was inferred"));
            return;
        }

        path = NormalizeWrapperPath(path, lambdaParameterType);
        if (path.Length == 0)
        {
            diagnostics.Add(Loss(
                project,
                projection,
                evidence,
                $"Marten FanOut configuration in '{projection.Name}' does not select an authored parent-event member and no fan-out mapping was inferred"));
            return;
        }

        fanOuts.Add(new(
            NormalizeEventType(parentEventType),
            NormalizeEventType(childEventType),
            path,
            mode,
            evidence));
    }

    static bool TryMemberPath(
        LambdaExpressionSyntax lambda,
        SemanticModel semanticModel,
        out string path,
        out INamedTypeSymbol? parameterType)
    {
        path = string.Empty;
        parameterType = null;
        if (lambda.ExpressionBody is null || LambdaParameter(lambda) is not { } parameter)
        {
            return false;
        }

        parameterType = (semanticModel.GetDeclaredSymbol(parameter) as IParameterSymbol)?.Type as INamedTypeSymbol;
        var segments = new Stack<string>();
        var current = UnwrapParentheses(lambda.ExpressionBody);
        while (current is MemberAccessExpressionSyntax memberAccess)
        {
            var member = semanticModel.GetSymbolInfo(memberAccess).Symbol;
            if (member is not (IPropertySymbol or IFieldSymbol))
            {
                return false;
            }

            segments.Push(LowerFirst(member.Name));
            current = UnwrapParentheses(memberAccess.Expression);
        }

        if (current is not IdentifierNameSyntax identifier ||
            semanticModel.GetSymbolInfo(identifier).Symbol is not IParameterSymbol identifierParameter ||
            semanticModel.GetDeclaredSymbol(parameter) is not IParameterSymbol lambdaParameter ||
            !SymbolEqualityComparer.Default.Equals(identifierParameter, lambdaParameter) ||
            segments.Count == 0)
        {
            return false;
        }

        path = string.Join('.', segments);
        return true;
    }

    static ParameterSyntax? LambdaParameter(LambdaExpressionSyntax lambda) => lambda switch
    {
        SimpleLambdaExpressionSyntax simple => simple.Parameter,
        ParenthesizedLambdaExpressionSyntax { ParameterList.Parameters.Count: 1 } parenthesized => parenthesized.ParameterList.Parameters[0],
        _ => null
    };

    static ExpressionSyntax UnwrapParentheses(ExpressionSyntax expression)
    {
        while (expression is ParenthesizedExpressionSyntax parenthesized)
        {
            expression = parenthesized.Expression;
        }

        return expression;
    }

    static bool TryFanOutMode(
        InvocationExpressionSyntax invocation,
        SemanticModel semanticModel,
        out string mode)
    {
        mode = "after-grouping";
        if (invocation.ArgumentList.Arguments.Count < 2)
        {
            return true;
        }

        if (semanticModel.GetSymbolInfo(invocation.ArgumentList.Arguments[1].Expression).Symbol is not IFieldSymbol field ||
            field.ContainingType.Name != "FanoutMode" ||
            field.ContainingNamespace.ToDisplayString() is not ("JasperFx.Events.Projections" or "Marten.Events.Projections"))
        {
            return false;
        }

        mode = field.Name switch
        {
            "BeforeGrouping" => "before-grouping",
            "AfterGrouping" => "after-grouping",
            _ => string.Empty
        };
        return mode.Length > 0;
    }

    static bool IsExactConfigurationMethod(IMethodSymbol method)
    {
        var candidate = method.ReducedFrom ?? method;
        if (!_configurationDeclaringTypes.Contains(DotNetSubjectIds.MetadataName(candidate.ContainingType.OriginalDefinition)))
        {
            return false;
        }

        return candidate.Name switch
        {
            "Identity" or "Identities" => candidate.TypeParameters.Length == 1,
            "FanOut" => candidate.TypeParameters.Length == 2,
            "CustomGrouping" or "RollUpByTenant" => true,
            _ => false
        };
    }

    static bool IsTopLevelConfiguration(
        InvocationExpressionSyntax invocation,
        ConstructorDeclarationSyntax constructor)
    {
        if (constructor.ExpressionBody?.Expression == invocation)
        {
            return true;
        }

        var statement = invocation.Ancestors().OfType<ExpressionStatementSyntax>().FirstOrDefault();
        return statement?.Expression == invocation && statement.Parent == constructor.Body;
    }

    static INamedTypeSymbol NormalizeEventType(INamedTypeSymbol eventType) =>
        eventType.IsGenericType &&
        _eventWrapperTypes.Contains(DotNetSubjectIds.MetadataName(eventType.OriginalDefinition)) &&
        eventType.TypeArguments[0] is INamedTypeSymbol inner
            ? inner
            : eventType;

    static string NormalizeWrapperPath(string path, INamedTypeSymbol? parameterType) =>
        parameterType?.IsGenericType == true &&
        _eventWrapperTypes.Contains(DotNetSubjectIds.MetadataName(parameterType.OriginalDefinition)) &&
        path.StartsWith("data.", StringComparison.Ordinal)
            ? path["data.".Length..]
            : path;

    static Evidence InvocationEvidence(
        DotNetProjectCompilation project,
        AdapterIdentity adapter,
        InvocationExpressionSyntax invocation,
        string methodName) => new()
        {
            Adapter = adapter,
            Strength = EvidenceStrength.Exact,
            Source = CritterStackSource.RangeForProject(invocation.GetLocation(), project),
            Explanation = $"Marten multi-stream configuration through {methodName}"
        };

    static GenerationDiagnostic Loss(
        DotNetProjectCompilation project,
        INamedTypeSymbol projection,
        Evidence evidence,
        string message) => new()
        {
            Code = MartenDiagnosticCodes.MultiStreamGroupingOmitted,
            Severity = GenerationDiagnosticSeverity.Warning,
            Message = message,
            Source = evidence.Source,
            Subject = project.SubjectForType(projection)
        };

    static string LowerFirst(string value) => value.Length == 0
        ? value
        : $"{char.ToLowerInvariant(value[0])}{value[1..]}";
}
