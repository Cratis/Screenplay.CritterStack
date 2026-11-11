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
        CritterStackSubjectResolver subjects,
        INamedTypeSymbol projection)
    {
        var scope = new ProjectionScope(project, adapter, subjects, projection);
        if (projection.BaseType is null ||
            DotNetSubjectIds.MetadataName(projection.BaseType.OriginalDefinition) != WellKnownTypes.MartenMultiStreamProjection)
        {
            return new(
                [],
                [],
                [
                scope.Loss(
                    CritterStackSource.EvidenceFor(
                        projection,
                        adapter,
                        project,
                        EvidenceStrength.Exact,
                        "The projection derives indirectly from MultiStreamProjection<T,TId>"),
                    $"Multi-stream projection '{projection.Name}' does not directly derive from the exact Marten MultiStreamProjection<T,TId> base, so inherited grouping configuration was not interpreted",
                    GenerationDiagnosticOutcome.Unknown)
            ]);
        }

        foreach (var constructor in projection.DeclaringSyntaxReferences
                     .Select(_ => _.GetSyntax())
                     .OfType<TypeDeclarationSyntax>()
                     .SelectMany(_ => _.Members.OfType<ConstructorDeclarationSyntax>()))
        {
            var constructorScope = new ConstructorScope(scope, constructor, project.Compilation.GetSemanticModel(constructor.SyntaxTree));
            DiscoverConfigurationInvocations(constructorScope);
            DiscoverTenancyGroupingAssignments(constructorScope);
        }

        return new(
            [.. scope.Identities.OrderBy(_ => DotNetSubjectIds.MetadataName(_.EventType), StringComparer.Ordinal).ThenBy(_ => _.TargetMember, StringComparer.Ordinal)],
            [.. scope.FanOuts.OrderBy(_ => DotNetSubjectIds.MetadataName(_.ParentEventType), StringComparer.Ordinal).ThenBy(_ => DotNetSubjectIds.MetadataName(_.ChildEventType), StringComparer.Ordinal).ThenBy(_ => _.SourceMember, StringComparer.Ordinal)],
            [.. scope.Diagnostics.OrderBy(_ => _.Source?.Path, StringComparer.Ordinal).ThenBy(_ => _.Source?.StartLine).ThenBy(_ => _.Message, StringComparer.Ordinal)]);
    }

    static void DiscoverConfigurationInvocations(ConstructorScope constructorScope)
    {
        foreach (var invocation in constructorScope.Constructor.DescendantNodes().OfType<InvocationExpressionSyntax>())
        {
            if (constructorScope.SemanticModel.GetSymbolInfo(invocation).Symbol is not IMethodSymbol method ||
                !IsExactConfigurationMethod(method))
            {
                continue;
            }

            var site = new ConfigurationSite(
                invocation,
                method,
                constructorScope.SemanticModel,
                InvocationEvidence(constructorScope.Projection, invocation, method.Name));
            if (!IsTopLevelConfiguration(invocation, constructorScope.Constructor))
            {
                constructorScope.Projection.Report(
                    site.Evidence,
                    $"Marten {method.Name} configuration in '{constructorScope.Projection.Projection.Name}' is conditional or nested and cannot be resolved safely",
                    GenerationDiagnosticOutcome.Unknown);
                continue;
            }

            DiscoverConfiguration(constructorScope.Projection, site);
        }
    }

    static void DiscoverConfiguration(ProjectionScope scope, ConfigurationSite site)
    {
        switch (site.Method.Name)
        {
            case "Identity":
            case "Identities":
                DiscoverIdentity(scope, site);
                break;
            case "FanOut":
                DiscoverFanOut(scope, site);
                break;
            case "CustomGrouping":
                scope.Report(
                    site.Evidence,
                    $"Multi-stream projection '{scope.Projection.Name}' uses arbitrary custom grouping through CustomGrouping and no identity mapping was inferred");
                break;
            case "RollUpByTenant":
                scope.Report(
                    site.Evidence,
                    $"Multi-stream projection '{scope.Projection.Name}' groups by tenant through RollUpByTenant and no identity mapping was inferred");
                break;
        }
    }

    static void DiscoverTenancyGroupingAssignments(ConstructorScope constructorScope)
    {
        foreach (var assignment in constructorScope.Constructor.DescendantNodes().OfType<AssignmentExpressionSyntax>())
        {
            if (!IsTenancyGroupingAssignment(assignment, constructorScope.SemanticModel))
            {
                continue;
            }

            var evidence = new Evidence
            {
                Adapter = constructorScope.Projection.Adapter,
                Strength = EvidenceStrength.Exact,
                Source = CritterStackSource.RangeForProject(assignment.GetLocation(), constructorScope.Projection.Project),
                Explanation = "Marten multi-stream tenancy grouping assignment"
            };
            constructorScope.Projection.Report(
                evidence,
                $"Multi-stream projection '{constructorScope.Projection.Projection.Name}' configures tenancy-dependent grouping and no identity mapping was inferred from that assignment");
        }
    }

    static bool IsTenancyGroupingAssignment(AssignmentExpressionSyntax assignment, SemanticModel semanticModel) =>
        semanticModel.GetSymbolInfo(assignment.Left).Symbol is IPropertySymbol property &&
        string.Equals(property.Name, "TenancyGrouping", StringComparison.Ordinal) &&
        DotNetSubjectIds.MetadataName(property.ContainingType.OriginalDefinition) == JasperFxMultiStreamProjection;

    static void DiscoverIdentity(ProjectionScope scope, ConfigurationSite site)
    {
        var method = site.Method;
        var projectionName = scope.Projection.Name;
        if (method.TypeArguments is not [INamedTypeSymbol configuredEventType] ||
            site.Invocation.ArgumentList.Arguments.FirstOrDefault()?.Expression is not LambdaExpressionSyntax lambda ||
            !TryMemberPath(lambda, site.SemanticModel, out var path, out var lambdaParameterType))
        {
            scope.Report(
                site.Evidence,
                $"Marten {method.Name} configuration in '{projectionName}' is not a simple member-selector lambda and no identity mapping was inferred",
                GenerationDiagnosticOutcome.Unknown);
            return;
        }

        if (path.Split('.').Any(_ => string.Equals(_, "tenantId", StringComparison.OrdinalIgnoreCase)))
        {
            scope.Report(
                site.Evidence,
                $"Marten {method.Name} configuration in '{projectionName}' depends on tenant identity and no identity mapping was inferred");
            return;
        }

        var eventType = NormalizeEventType(configuredEventType);
        path = NormalizeWrapperPath(path, lambdaParameterType);
        if (path.Length == 0)
        {
            scope.Report(
                site.Evidence,
                $"Marten {method.Name} configuration in '{projectionName}' does not select a member of the authored event and no identity mapping was inferred",
                GenerationDiagnosticOutcome.Unknown);
            return;
        }

        scope.Identities.Add(new(
            eventType,
            path,
            method.Name == "Identities",
            site.Evidence));
    }

    static void DiscoverFanOut(ProjectionScope scope, ConfigurationSite site)
    {
        var projectionName = scope.Projection.Name;
        if (site.Method.TypeArguments is not [INamedTypeSymbol parentEventType, INamedTypeSymbol childEventType] ||
            site.Invocation.ArgumentList.Arguments.FirstOrDefault()?.Expression is not LambdaExpressionSyntax lambda ||
            !TryMemberPath(lambda, site.SemanticModel, out var path, out var lambdaParameterType) ||
            !TryFanOutMode(site.Invocation, site.SemanticModel, out var mode))
        {
            scope.Report(
                site.Evidence,
                $"Marten FanOut configuration in '{projectionName}' is not an exact declaration with a simple member-selector lambda and no fan-out mapping was inferred",
                GenerationDiagnosticOutcome.Unknown);
            return;
        }

        path = NormalizeWrapperPath(path, lambdaParameterType);
        if (path.Length == 0)
        {
            scope.Report(
                site.Evidence,
                $"Marten FanOut configuration in '{projectionName}' does not select an authored parent-event member and no fan-out mapping was inferred",
                GenerationDiagnosticOutcome.Unknown);
            return;
        }

        scope.FanOuts.Add(new(
            NormalizeEventType(parentEventType),
            NormalizeEventType(childEventType),
            path,
            mode,
            site.Evidence));
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
        if (CollectMemberSegments(UnwrapParentheses(lambda.ExpressionBody), semanticModel, segments) is not { } current)
        {
            return false;
        }

        if (!IsReferenceToParameter(current, parameter, semanticModel) || segments.Count == 0)
        {
            return false;
        }

        path = string.Join('.', segments);
        return true;
    }

    static ExpressionSyntax? CollectMemberSegments(ExpressionSyntax start, SemanticModel semanticModel, Stack<string> segments)
    {
        var current = start;
        while (current is MemberAccessExpressionSyntax memberAccess)
        {
            var member = semanticModel.GetSymbolInfo(memberAccess).Symbol;
            if (member is not (IPropertySymbol or IFieldSymbol))
            {
                return null;
            }

            segments.Push(LowerFirst(member.Name));
            current = UnwrapParentheses(memberAccess.Expression);
        }

        return current;
    }

    static bool IsReferenceToParameter(ExpressionSyntax expression, ParameterSyntax parameter, SemanticModel semanticModel) =>
        expression is IdentifierNameSyntax identifier &&
        semanticModel.GetSymbolInfo(identifier).Symbol is IParameterSymbol identifierParameter &&
        semanticModel.GetDeclaredSymbol(parameter) is IParameterSymbol lambdaParameter &&
        SymbolEqualityComparer.Default.Equals(identifierParameter, lambdaParameter);

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

        if (FanoutModeField(invocation.ArgumentList.Arguments[1].Expression, semanticModel) is not { } field)
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

    static IFieldSymbol? FanoutModeField(ExpressionSyntax expression, SemanticModel semanticModel) =>
        semanticModel.GetSymbolInfo(expression).Symbol is IFieldSymbol field &&
        field.ContainingType.Name == "FanoutMode" &&
        field.ContainingNamespace.ToDisplayString() is var fieldNamespace &&
        (string.Equals(fieldNamespace, "JasperFx.Events.Projections", StringComparison.Ordinal) ||
         string.Equals(fieldNamespace, "Marten.Events.Projections", StringComparison.Ordinal))
            ? field
            : null;

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
        ProjectionScope scope,
        InvocationExpressionSyntax invocation,
        string methodName) => new()
        {
            Adapter = scope.Adapter,
            Strength = EvidenceStrength.Exact,
            Source = CritterStackSource.RangeForProject(invocation.GetLocation(), scope.Project),
            Explanation = $"Marten multi-stream configuration through {methodName}"
        };

    static string LowerFirst(string value) => value.Length == 0
        ? value
        : $"{char.ToLowerInvariant(value[0])}{value[1..]}";

    sealed record ConfigurationSite(
        InvocationExpressionSyntax Invocation,
        IMethodSymbol Method,
        SemanticModel SemanticModel,
        Evidence Evidence);

    sealed record ConstructorScope(
        ProjectionScope Projection,
        ConstructorDeclarationSyntax Constructor,
        SemanticModel SemanticModel);

    sealed class ProjectionScope(
        DotNetProjectCompilation project,
        AdapterIdentity adapter,
        CritterStackSubjectResolver subjects,
        INamedTypeSymbol projection)
    {
        public DotNetProjectCompilation Project { get; } = project;

        public AdapterIdentity Adapter { get; } = adapter;

        public INamedTypeSymbol Projection { get; } = projection;

        public List<MartenMultiStreamIdentity> Identities { get; } = [];

        public List<MartenMultiStreamFanOut> FanOuts { get; } = [];

        public List<GenerationDiagnostic> Diagnostics { get; } = [];

        public void Report(
            Evidence evidence,
            string message,
            GenerationDiagnosticOutcome outcome = GenerationDiagnosticOutcome.Unsupported) =>
            Diagnostics.Add(Loss(evidence, message, outcome));

        public GenerationDiagnostic Loss(
            Evidence evidence,
            string message,
            GenerationDiagnosticOutcome outcome = GenerationDiagnosticOutcome.Unsupported) => new()
            {
                Code = MartenDiagnosticCodes.MultiStreamGroupingOmitted,
                Severity = GenerationDiagnosticSeverity.Warning,
                Outcome = outcome,
                Message = message,
                Source = evidence.Source,
                Subject = subjects.SubjectForType(Project, Projection)
            };
    }
}
