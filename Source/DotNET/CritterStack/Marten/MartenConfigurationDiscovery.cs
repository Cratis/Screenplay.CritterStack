// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Cratis.Screenplay.Generation;
using Cratis.Screenplay.Generation.DotNet;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Cratis.CritterStack.Screenplay.Marten;

sealed record MartenConfigurationDiscoveryResult(
    IReadOnlyList<GenerationFact> Facts,
    IReadOnlyList<GenerationDiagnostic> Diagnostics,
    bool SideEffectsEnabled);

static class MartenConfigurationDiscovery
{
    static readonly HashSet<string> _projectionMetadataTypes =
    [
        WellKnownTypes.JasperFxProjectionBase,
        WellKnownTypes.MartenProjectionBase
    ];
    static readonly HashSet<string> _daemonModeTypes =
    [
        WellKnownTypes.JasperFxDaemonMode,
        WellKnownTypes.MartenDaemonMode
    ];
    static readonly HashSet<string> _daemonSettingTypes =
    [
        "JasperFx.Events.Daemon.DaemonSettings",
        "Marten.Events.Daemon.DaemonSettings"
    ];
    static readonly HashSet<string> _subscriptionOptionTypes =
    [
        WellKnownTypes.JasperFxSubscriptionOptions,
        WellKnownTypes.MartenSubscriptionOptions,
        WellKnownTypes.MartenSubscriptionBase,
        "JasperFx.Events.Projections.EventFilterable",
        "JasperFx.Events.Projections.IEventFilterable",
        "JasperFx.Events.Subscriptions.JasperFxSubscriptionBase`3"
    ];
    static readonly HashSet<string> _subscriptionStartTypes =
    [
        "JasperFx.Events.Projections.AsyncOptions",
        "Marten.Events.Daemon.AsyncOptions"
    ];
    static readonly HashSet<string> _projectionMetadataProperties = ["Name", "ProjectionName", "Version"];
    static readonly HashSet<string> _subscriptionOptionProperties = ["Name", "SubscriptionName", "Version", "SubscriptionVersion", "IncludeArchivedEvents"];
    static readonly HashSet<string> _customProcessingMethods = ["Apply", "ApplyAsync", "ProcessEventsAsync"];
    static readonly HashSet<string> _subscriptionRegistrationTypes = ["IEventStoreOptions", "EventStoreOptions", "ProjectionOptions"];

    public static MartenConfigurationDiscoveryResult Discover(
        DotNetProjectCompilation project,
        AdapterIdentity adapter,
        CritterStackSubjectResolver subjects,
        IReadOnlyList<ProjectionRegistration> registrations)
    {
        var facts = new List<GenerationFact>();
        var diagnostics = MartenConventionAlterationDiscovery.Discover(project, subjects)
            .Concat(MartenSessionListenerDiscovery.Discover(project, subjects))
            .ToList();
        var sideEffectsEnabled = false;
        var projections = registrations
            .Where(_ => _.Projection is not null)
            .Select(_ => _.Projection!)
            .Distinct<INamedTypeSymbol>(SymbolEqualityComparer.Default)
            .ToArray();

        foreach (var projection in projections)
        {
            DiscoverProjectionMetadata(project, subjects, projection, diagnostics);
            if (IsRawProjection(projection))
            {
                AddCustomProcessingDiagnostics(project, subjects, projection, diagnostics, "custom projection");
            }
        }

        foreach (var tree in project.Compilation.SyntaxTrees.Where(project.AuthoredSyntaxTrees.Contains))
        {
            var semanticModel = project.Compilation.GetSemanticModel(tree);
            var root = tree.GetRoot();
            foreach (var invocation in root.DescendantNodes().OfType<InvocationExpressionSyntax>())
            {
                if (semanticModel.GetSymbolInfo(invocation).Symbol is not IMethodSymbol method)
                {
                    continue;
                }

                DiscoverRegisteredValueType(project, adapter, subjects, invocation, method, semanticModel, facts);
                DiscoverDaemonConfiguration(project, invocation, method, semanticModel, diagnostics);
                DiscoverProjectionRegistrationMetadata(project, subjects, invocation, method, semanticModel, diagnostics);
                DiscoverUnresolvedLifecycle(project, subjects, invocation, method, semanticModel, diagnostics);
                DiscoverSubscriptionRegistration(project, subjects, invocation, method, semanticModel, diagnostics);
            }

            foreach (var assignment in root.DescendantNodes().OfType<AssignmentExpressionSyntax>())
            {
                sideEffectsEnabled |= EnablesInlineProjectionSideEffects(assignment, semanticModel);
                DiscoverDaemonAssignment(project, assignment, semanticModel, diagnostics);
            }
        }

        return new(
            facts,
            [
                .. diagnostics
                    .GroupBy(_ => new DiagnosticKey(_.Code, _.Message, _.Source?.Path, _.Source?.StartLine, _.Source?.StartColumn, _.Subject))
                    .Select(_ => _.First())
                    .OrderBy(_ => _.Source?.Path, StringComparer.Ordinal)
                    .ThenBy(_ => _.Source?.StartLine)
                    .ThenBy(_ => _.Source?.StartColumn)
                    .ThenBy(_ => _.Code, StringComparer.Ordinal)
                    .ThenBy(_ => _.Message, StringComparer.Ordinal)
            ],
            sideEffectsEnabled);
    }

    public static bool IsUnresolvedProcessorType(INamedTypeSymbol type) =>
        IsSubscription(type) || IsRawProjection(type);

    static void DiscoverRegisteredValueType(
        DotNetProjectCompilation project,
        AdapterIdentity adapter,
        CritterStackSubjectResolver subjects,
        InvocationExpressionSyntax invocation,
        IMethodSymbol method,
        SemanticModel semanticModel,
        List<GenerationFact> facts)
    {
        var candidate = method.OriginalDefinition;
        if (!string.Equals(candidate.Name, "RegisterValueType", StringComparison.Ordinal) ||
            DotNetSubjectIds.MetadataName(candidate.ContainingType.OriginalDefinition) != WellKnownTypes.MartenStoreOptions)
        {
            return;
        }

        var type = method.TypeArguments.Length == 1
            ? method.TypeArguments[0] as INamedTypeSymbol
            : TypeFromTypeOfArgument(invocation, semanticModel);
        if (type is null || type.TypeKind == TypeKind.Error)
        {
            return;
        }

        var subject = subjects.SubjectForType(project, type);
        var factId = $"marten:registered-value-type:{subject.Value}";
        if (facts.OfType<ArtifactFact>().Any(_ => _.Id.Value == factId))
        {
            return;
        }

        var evidence = new Evidence
        {
            Adapter = adapter,
            Strength = EvidenceStrength.Configured,
            Source = CritterStackSource.RangeForProject(invocation.GetLocation(), project),
            Explanation = $"Marten registers '{type.Name}' as a value type"
        };
        facts.Add(new ArtifactFact
        {
            Id = new FactId { Value = factId },
            Subject = subject,
            Definition = new ArtifactDefinition
            {
                Key = new ArtifactKey { Subject = subject, Kind = ArtifactKind.Concept },
                Name = type.Name,
                File = CritterStackSource.EvidenceFor(type, adapter, project, EvidenceStrength.Exact).Source?.Path
            },
            Evidence = evidence
        });
    }

    static bool EnablesInlineProjectionSideEffects(
        AssignmentExpressionSyntax assignment,
        SemanticModel semanticModel) =>
        semanticModel.GetSymbolInfo(assignment.Left).Symbol is IPropertySymbol property &&
        string.Equals(property.Name, "EnableSideEffectsOnInlineProjections", StringComparison.Ordinal) &&
        DotNetSubjectIds.MetadataName(property.ContainingType.OriginalDefinition) == WellKnownTypes.MartenEventStoreOptions &&
        semanticModel.GetConstantValue(assignment.Right) is { HasValue: true, Value: true };

    static void DiscoverProjectionMetadata(
        DotNetProjectCompilation project,
        CritterStackSubjectResolver subjects,
        INamedTypeSymbol projection,
        List<GenerationDiagnostic> diagnostics)
    {
        var constructors = projection.DeclaringSyntaxReferences
            .Select(_ => _.GetSyntax())
            .OfType<TypeDeclarationSyntax>()
            .SelectMany(_ => _.Members.OfType<ConstructorDeclarationSyntax>())
            .ToArray();
        foreach (var constructor in constructors.Where(_ => project.AuthoredSyntaxTrees.Contains(_.SyntaxTree)))
        {
            var semanticModel = project.Compilation.GetSemanticModel(constructor.SyntaxTree);
            foreach (var assignment in constructor.DescendantNodes().OfType<AssignmentExpressionSyntax>())
            {
                if (semanticModel.GetSymbolInfo(assignment.Left).Symbol is not IPropertySymbol property ||
                    !IsProjectionMetadataProperty(property))
                {
                    continue;
                }

                var direct = constructors.Length == 1 && IsDirectConstructorStatement(assignment, constructor);
                var value = property.Name switch
                {
                    "Name" or "ProjectionName" => ConstantString(assignment.Right, semanticModel),
                    "Version" => ConstantUnsigned(assignment.Right, semanticModel)?.ToString(),
                    _ => null
                };
                var message = direct && value is not null
                    ? $"Projection '{projection.Name}' configures projection {MetadataLabel(property.Name)} '{value}', which is not expressible in the current Screenplay contracts"
                    : $"Projection '{projection.Name}' configures projection {MetadataLabel(property.Name)} with a conditional, computed, or otherwise non-constant value that could not be resolved safely";
                diagnostics.Add(Loss(
                    project,
                    subjects,
                    projection,
                    MartenDiagnosticCodes.ProjectionMetadataOmitted,
                    message,
                    assignment.GetLocation(),
                    direct && value is not null
                        ? GenerationDiagnosticOutcome.Unsupported
                        : GenerationDiagnosticOutcome.Unknown));
            }
        }
    }

    static void DiscoverDaemonConfiguration(
        DotNetProjectCompilation project,
        InvocationExpressionSyntax invocation,
        IMethodSymbol method,
        SemanticModel semanticModel,
        List<GenerationDiagnostic> diagnostics)
    {
        var candidate = method.ReducedFrom ?? method;
        if (candidate.Name != "AddAsyncDaemon" || !IsMartenServiceExpression(candidate.ContainingType))
        {
            return;
        }

        var mode = invocation.ArgumentList.Arguments.FirstOrDefault()?.Expression is { } expression
            ? EnumMember(expression, semanticModel, _daemonModeTypes)
            : null;
        diagnostics.Add(new GenerationDiagnostic
        {
            Code = MartenDiagnosticCodes.DaemonConfigurationOmitted,
            Severity = GenerationDiagnosticSeverity.Warning,
            Outcome = mode is null
                ? GenerationDiagnosticOutcome.Unknown
                : GenerationDiagnosticOutcome.Unsupported,
            Message = mode is null
                ? "Marten async daemon mode is computed or otherwise non-constant and could not be resolved safely"
                : $"Marten async daemon mode '{mode}' is configured; daemon hosting and shard execution configuration are not expressible in the current Screenplay contracts",
            Source = CritterStackSource.RangeForProject(invocation.GetLocation(), project),
            Subject = ProjectSubject(project)
        });
    }

    static void DiscoverDaemonAssignment(
        DotNetProjectCompilation project,
        AssignmentExpressionSyntax assignment,
        SemanticModel semanticModel,
        List<GenerationDiagnostic> diagnostics)
    {
        if (semanticModel.GetSymbolInfo(assignment.Left).Symbol is not IPropertySymbol { Name: "AsyncMode" } property ||
            !_daemonSettingTypes.Contains(DotNetSubjectIds.MetadataName(property.ContainingType)))
        {
            return;
        }

        var mode = EnumMember(assignment.Right, semanticModel, _daemonModeTypes);
        diagnostics.Add(new GenerationDiagnostic
        {
            Code = MartenDiagnosticCodes.DaemonConfigurationOmitted,
            Severity = GenerationDiagnosticSeverity.Warning,
            Outcome = mode is null
                ? GenerationDiagnosticOutcome.Unknown
                : GenerationDiagnosticOutcome.Unsupported,
            Message = mode is null
                ? "Marten projection daemon AsyncMode is computed or otherwise non-constant and could not be resolved safely"
                : $"Marten projection daemon AsyncMode '{mode}' is configured; daemon hosting and shard execution configuration are not expressible in the current Screenplay contracts",
            Source = CritterStackSource.RangeForProject(assignment.GetLocation(), project),
            Subject = ProjectSubject(project)
        });
    }

    static void DiscoverProjectionRegistrationMetadata(
        DotNetProjectCompilation project,
        CritterStackSubjectResolver subjects,
        InvocationExpressionSyntax invocation,
        IMethodSymbol method,
        SemanticModel semanticModel,
        List<GenerationDiagnostic> diagnostics)
    {
        if (!IsProjectionRegistration(method, invocation, semanticModel) ||
            ProjectionTypeFrom(method, invocation, semanticModel) is not { } projection)
        {
            return;
        }

        if (ArgumentForParameter(invocation, method, "projectionName") is { } nameArgument)
        {
            var value = ConstantString(nameArgument.Expression, semanticModel);
            diagnostics.Add(Loss(
                project,
                subjects,
                projection,
                MartenDiagnosticCodes.ProjectionMetadataOmitted,
                value is null
                    ? $"Projection '{projection.Name}' registers a computed or otherwise non-constant projection name that could not be resolved safely"
                    : $"Projection '{projection.Name}' registers projection name '{value}', which is not expressible in the current Screenplay contracts",
                nameArgument.GetLocation(),
                value is null
                    ? GenerationDiagnosticOutcome.Unknown
                    : GenerationDiagnosticOutcome.Unsupported));
        }

        foreach (var lambda in invocation.ArgumentList.Arguments.Select(_ => _.Expression).OfType<LambdaExpressionSyntax>())
        {
            foreach (var assignment in lambda.DescendantNodes().OfType<AssignmentExpressionSyntax>())
            {
                if (semanticModel.GetSymbolInfo(assignment.Left).Symbol is not IPropertySymbol property ||
                    !IsProjectionMetadataProperty(property))
                {
                    continue;
                }

                var value = property.Name switch
                {
                    "Name" or "ProjectionName" => ConstantString(assignment.Right, semanticModel),
                    "Version" => ConstantUnsigned(assignment.Right, semanticModel)?.ToString(),
                    _ => null
                };
                diagnostics.Add(Loss(
                    project,
                    subjects,
                    projection,
                    MartenDiagnosticCodes.ProjectionMetadataOmitted,
                    IsDirectScopeStatement(assignment, lambda) && value is not null
                        ? $"Projection '{projection.Name}' registers projection {MetadataLabel(property.Name)} '{value}', which is not expressible in the current Screenplay contracts"
                        : $"Projection '{projection.Name}' registers projection {MetadataLabel(property.Name)} conditionally or with a non-constant value that could not be resolved safely",
                    assignment.GetLocation(),
                    IsDirectScopeStatement(assignment, lambda) && value is not null
                        ? GenerationDiagnosticOutcome.Unsupported
                        : GenerationDiagnosticOutcome.Unknown));
            }
        }
    }

    static void DiscoverUnresolvedLifecycle(
        DotNetProjectCompilation project,
        CritterStackSubjectResolver subjects,
        InvocationExpressionSyntax invocation,
        IMethodSymbol method,
        SemanticModel semanticModel,
        List<GenerationDiagnostic> diagnostics)
    {
        if (!IsProjectionRegistration(method, invocation, semanticModel) ||
            !method.Parameters.Any(_ => _.Name == "lifecycle"))
        {
            return;
        }

        var lifecycleArgument = ArgumentForParameter(invocation, method, "lifecycle");
        if (lifecycleArgument is null ||
            EnumMember(lifecycleArgument.Expression, semanticModel, MartenProjectionDiscovery.ProjectionLifecycleTypes) is not null)
        {
            return;
        }

        var projection = ProjectionTypeFrom(method, invocation, semanticModel);
        diagnostics.Add(new GenerationDiagnostic
        {
            Code = MartenDiagnosticCodes.ProjectionLifecycleOmitted,
            Severity = GenerationDiagnosticSeverity.Warning,
            Outcome = GenerationDiagnosticOutcome.Unknown,
            Message = projection is null
                ? "Marten projection lifecycle is computed or otherwise non-constant and could not be resolved safely"
                : $"Projection '{projection.Name}' uses a computed or otherwise non-constant lifecycle that could not be resolved safely",
            Source = CritterStackSource.RangeForProject(lifecycleArgument.GetLocation(), project),
            Subject = projection is null ? ProjectSubject(project) : subjects.SubjectForType(project, projection)
        });
    }

    static void DiscoverSubscriptionRegistration(
        DotNetProjectCompilation project,
        CritterStackSubjectResolver subjects,
        InvocationExpressionSyntax invocation,
        IMethodSymbol method,
        SemanticModel semanticModel,
        List<GenerationDiagnostic> diagnostics)
    {
        if (!IsSubscriptionRegistration(method) ||
            SubscriptionTypeFrom(method, invocation, semanticModel) is not { } subscription)
        {
            return;
        }

        diagnostics.Add(Loss(
            project,
            subjects,
            subscription,
            MartenDiagnosticCodes.SubscriptionConfigurationOmitted,
            $"Marten subscription '{subscription.Name}' is registered, but the current Screenplay contracts have no neutral subscription artifact",
            invocation.GetLocation()));

        DiscoverSubscriptionConstructorConfiguration(project, subjects, subscription, diagnostics);
        foreach (var lambda in invocation.ArgumentList.Arguments.Select(_ => _.Expression).OfType<LambdaExpressionSyntax>())
        {
            DiscoverSubscriptionScopeConfiguration(project, subjects, subscription, lambda, semanticModel, diagnostics);
        }

        AddCustomProcessingDiagnostics(project, subjects, subscription, diagnostics, "subscription");
    }

    static void DiscoverSubscriptionConstructorConfiguration(
        DotNetProjectCompilation project,
        CritterStackSubjectResolver subjects,
        INamedTypeSymbol subscription,
        List<GenerationDiagnostic> diagnostics)
    {
        var constructors = subscription.DeclaringSyntaxReferences
            .Select(_ => _.GetSyntax())
            .OfType<TypeDeclarationSyntax>()
            .SelectMany(_ => _.Members.OfType<ConstructorDeclarationSyntax>())
            .ToArray();
        foreach (var constructor in constructors.Where(_ => project.AuthoredSyntaxTrees.Contains(_.SyntaxTree)))
        {
            var semanticModel = project.Compilation.GetSemanticModel(constructor.SyntaxTree);
            DiscoverSubscriptionScopeConfiguration(
                project,
                subjects,
                subscription,
                constructor,
                semanticModel,
                diagnostics,
                constructors.Length == 1);
        }
    }

    static void DiscoverSubscriptionScopeConfiguration(
        DotNetProjectCompilation project,
        CritterStackSubjectResolver subjects,
        INamedTypeSymbol subscription,
        SyntaxNode scope,
        SemanticModel semanticModel,
        List<GenerationDiagnostic> diagnostics,
        bool allowExact = true)
    {
        foreach (var assignment in scope.DescendantNodes().OfType<AssignmentExpressionSyntax>())
        {
            if (semanticModel.GetSymbolInfo(assignment.Left).Symbol is not IPropertySymbol property ||
                !IsSubscriptionOptionProperty(property))
            {
                continue;
            }

            var direct = allowExact && IsDirectScopeStatement(assignment, scope);
            var value = property.Name switch
            {
                "Name" or "SubscriptionName" => ConstantString(assignment.Right, semanticModel),
                "Version" or "SubscriptionVersion" => ConstantUnsigned(assignment.Right, semanticModel)?.ToString(),
                "IncludeArchivedEvents" => ConstantBoolean(assignment.Right, semanticModel)?.ToString().ToLowerInvariant(),
                _ => null
            };
            diagnostics.Add(Loss(
                project,
                subjects,
                subscription,
                MartenDiagnosticCodes.SubscriptionConfigurationOmitted,
                direct && value is not null
                    ? $"Marten subscription '{subscription.Name}' configures {SubscriptionLabel(property.Name)} '{value}', which is not expressible in the current Screenplay contracts"
                    : $"Marten subscription '{subscription.Name}' configures {SubscriptionLabel(property.Name)} conditionally or with a non-constant value that could not be resolved safely",
                assignment.GetLocation(),
                direct && value is not null
                    ? GenerationDiagnosticOutcome.Unsupported
                    : GenerationDiagnosticOutcome.Unknown));
        }

        foreach (var invocation in scope.DescendantNodes().OfType<InvocationExpressionSyntax>())
        {
            if (semanticModel.GetSymbolInfo(invocation).Symbol is not IMethodSymbol method ||
                !IsSubscriptionOptionMethod(method))
            {
                continue;
            }

            var direct = allowExact && IsDirectScopeStatement(invocation, scope);
            var value = direct ? SubscriptionMethodValue(invocation, method, semanticModel) : null;
            diagnostics.Add(Loss(
                project,
                subjects,
                subscription,
                MartenDiagnosticCodes.SubscriptionConfigurationOmitted,
                value is not null
                    ? $"Marten subscription '{subscription.Name}' configures {value}, which is not expressible in the current Screenplay contracts"
                    : $"Marten subscription '{subscription.Name}' uses {method.Name} conditionally or with arguments that could not be resolved exactly",
                invocation.GetLocation(),
                value is not null
                    ? GenerationDiagnosticOutcome.Unsupported
                    : GenerationDiagnosticOutcome.Unknown));
        }
    }

    static string? SubscriptionMethodValue(
        InvocationExpressionSyntax invocation,
        IMethodSymbol method,
        SemanticModel semanticModel)
    {
        switch (method.Name)
        {
            case "IncludeType":
                var eventType = method.TypeArguments.FirstOrDefault() as INamedTypeSymbol ??
                                TypeFromTypeOfArgument(invocation, semanticModel);
                return eventType is not null ? $"an event-type filter for '{eventType.Name}'" : null;
            case "FilterIncomingEventsOnStreamType":
                return invocation.ArgumentList.Arguments.FirstOrDefault()?.Expression is TypeOfExpressionSyntax typeOf &&
                       semanticModel.GetTypeInfo(typeOf.Type).Type is INamedTypeSymbol streamType
                    ? $"a stream-type filter for '{streamType.Name}'"
                    : null;
            case "SubscribeFromPresent":
                return DatabaseSuffix(invocation, semanticModel) is { } presentDatabase
                    ? $"starting position 'present'{presentDatabase}"
                    : null;
            case "SubscribeFromSequence":
                if (invocation.ArgumentList.Arguments.FirstOrDefault()?.Expression is not { } sequenceExpression ||
                    ConstantLong(sequenceExpression, semanticModel) is not { } sequence ||
                    DatabaseSuffix(invocation, semanticModel) is not { } sequenceDatabase)
                {
                    return null;
                }
                return $"starting sequence '{sequence}'{sequenceDatabase}";
            case "SubscribeFromTime" or "SubscribeAsInlineToAsync":
                return null;
            default:
                return null;
        }
    }

    static INamedTypeSymbol? TypeFromTypeOfArgument(
        InvocationExpressionSyntax invocation,
        SemanticModel semanticModel) =>
        invocation.ArgumentList.Arguments.FirstOrDefault()?.Expression is TypeOfExpressionSyntax typeOf
            ? semanticModel.GetTypeInfo(typeOf.Type).Type as INamedTypeSymbol
            : null;

    static string? DatabaseSuffix(InvocationExpressionSyntax invocation, SemanticModel semanticModel)
    {
        if (invocation.ArgumentList.Arguments.Count == 0)
        {
            return string.Empty;
        }

        var expression = invocation.ArgumentList.Arguments[^1].Expression;
        if (semanticModel.GetTypeInfo(expression).ConvertedType?.SpecialType != SpecialType.System_String)
        {
            return invocation.ArgumentList.Arguments.Count == 1 ? string.Empty : null;
        }

        var database = ConstantString(expression, semanticModel);
        return database is null ? null : $" for database '{database}'";
    }

    static void AddCustomProcessingDiagnostics(
        DotNetProjectCompilation project,
        CritterStackSubjectResolver subjects,
        INamedTypeSymbol type,
        List<GenerationDiagnostic> diagnostics,
        string kind)
    {
        foreach (var method in type.GetMembers().OfType<IMethodSymbol>()
                     .Where(_ => !_.IsImplicitlyDeclared && _customProcessingMethods.Contains(_.Name)))
        {
            if (method.Locations.FirstOrDefault(_ => IsAuthoredSourceLocation(_, project.AuthoredSyntaxTrees)) is not { } location)
            {
                continue;
            }

            diagnostics.Add(new GenerationDiagnostic
            {
                Code = MartenDiagnosticCodes.CustomProcessingOmitted,
                Severity = GenerationDiagnosticSeverity.Warning,
                Outcome = GenerationDiagnosticOutcome.Unsupported,
                Message = $"Marten {kind} '{type.Name}' has arbitrary {method.Name} consequences; no State View, Automation, Translation, document operation, message, or event consequence was inferred",
                Source = CritterStackSource.RangeForProject(location, project),
                Subject = subjects.SubjectForType(project, type)
            });
        }
    }

    static bool IsProjectionMetadataProperty(IPropertySymbol property) =>
        _projectionMetadataTypes.Contains(DotNetSubjectIds.MetadataName(property.ContainingType)) &&
        _projectionMetadataProperties.Contains(property.Name);

    static bool IsSubscriptionOptionProperty(IPropertySymbol property) =>
        _subscriptionOptionProperties.Contains(property.Name) &&
        _subscriptionOptionTypes.Contains(DotNetSubjectIds.MetadataName(property.ContainingType));

    static bool IsSubscriptionOptionMethod(IMethodSymbol method)
    {
        var candidate = method.ReducedFrom ?? method;
        return candidate.Name switch
        {
            "IncludeType" or "FilterIncomingEventsOnStreamType" =>
                _subscriptionOptionTypes.Contains(DotNetSubjectIds.MetadataName(candidate.ContainingType)),
            "SubscribeFromPresent" or "SubscribeFromSequence" or "SubscribeFromTime" or "SubscribeAsInlineToAsync" =>
                _subscriptionStartTypes.Contains(DotNetSubjectIds.MetadataName(candidate.ContainingType)),
            _ => false
        };
    }

    static bool IsProjectionRegistration(
        IMethodSymbol method,
        InvocationExpressionSyntax invocation,
        SemanticModel semanticModel)
    {
        var candidate = method.ReducedFrom ?? method;
        if (candidate.Name is not ("Add" or "AddProjectionWithServices" or "Snapshot"))
        {
            return false;
        }

        if (candidate.Name == "AddProjectionWithServices")
        {
            return IsMartenServiceExpression(candidate.ContainingType);
        }

        if (candidate.ContainingNamespace.ToDisplayString() == "Marten.Events.Projections")
        {
            return true;
        }

        return invocation.Expression is MemberAccessExpressionSyntax memberAccess &&
               semanticModel.GetTypeInfo(memberAccess.Expression).Type is INamedTypeSymbol receiver &&
               DotNetSubjectIds.MetadataName(receiver.OriginalDefinition) == WellKnownTypes.MartenProjectionOptions;
    }

    static bool IsSubscriptionRegistration(IMethodSymbol method)
    {
        var candidate = method.ReducedFrom ?? method;
        if (candidate.Name == "AddSubscriptionWithServices")
        {
            return IsMartenServiceExpression(candidate.ContainingType);
        }

        return candidate.Name == "Subscribe" &&
               candidate.ContainingNamespace.ToDisplayString().StartsWith("Marten.Events", StringComparison.Ordinal) &&
               _subscriptionRegistrationTypes.Contains(candidate.ContainingType.Name);
    }

    static bool IsMartenServiceExpression(INamedTypeSymbol type) =>
        type.ContainingNamespace.ToDisplayString() == "Marten" &&
        (type.Name == "MartenConfigurationExpression" || type.Name.StartsWith("MartenStoreExpression", StringComparison.Ordinal));

    static INamedTypeSymbol? SubscriptionTypeFrom(
        IMethodSymbol method,
        InvocationExpressionSyntax invocation,
        SemanticModel semanticModel)
    {
        if (method.TypeArguments.FirstOrDefault() is INamedTypeSymbol genericType && IsSubscription(genericType))
        {
            return genericType;
        }

        return invocation.ArgumentList.Arguments
            .Select(_ => semanticModel.GetTypeInfo(_.Expression).Type)
            .OfType<INamedTypeSymbol>()
            .FirstOrDefault(IsSubscription);
    }

    static INamedTypeSymbol? ProjectionTypeFrom(
        IMethodSymbol method,
        InvocationExpressionSyntax invocation,
        SemanticModel semanticModel)
    {
        if (method.TypeArguments.FirstOrDefault() is INamedTypeSymbol genericType)
        {
            return genericType;
        }

        return invocation.ArgumentList.Arguments
            .Select(_ => semanticModel.GetTypeInfo(_.Expression).Type)
            .OfType<INamedTypeSymbol>()
            .FirstOrDefault(_ => MartenProjectionDiscovery.ShapeOf(_) is not null || IsRawProjection(_));
    }

    static ArgumentSyntax? ArgumentForParameter(
        InvocationExpressionSyntax invocation,
        IMethodSymbol method,
        string parameterName)
    {
        var named = invocation.ArgumentList.Arguments.FirstOrDefault(_ => _.NameColon?.Name.Identifier.ValueText == parameterName);
        if (named is not null)
        {
            return named;
        }

        for (var index = 0; index < method.Parameters.Length && index < invocation.ArgumentList.Arguments.Count; index++)
        {
            if (method.Parameters[index].Name == parameterName)
            {
                return invocation.ArgumentList.Arguments[index];
            }
        }

        return null;
    }

    static bool IsSubscription(INamedTypeSymbol type) =>
        DotNetSubjectIds.MetadataName(type) == WellKnownTypes.MartenSubscription ||
        type.AllInterfaces.Any(_ => DotNetSubjectIds.MetadataName(_.OriginalDefinition) == WellKnownTypes.MartenSubscription);

    static bool IsRawProjection(INamedTypeSymbol type) =>
        MartenProjectionDiscovery.ShapeOf(type) is null &&
        type.AllInterfaces.Any(_ => DotNetSubjectIds.MetadataName(_.OriginalDefinition) == WellKnownTypes.MartenProjection);

    static bool IsDirectConstructorStatement(SyntaxNode node, ConstructorDeclarationSyntax constructor) =>
        constructor.ExpressionBody?.Expression == node ||
        (node.Ancestors().OfType<ExpressionStatementSyntax>().FirstOrDefault() is { } statement &&
         statement.Parent == constructor.Body);

    static bool IsDirectScopeStatement(SyntaxNode node, SyntaxNode scope)
    {
        var statement = node.Ancestors().OfType<ExpressionStatementSyntax>().FirstOrDefault();
        return scope switch
        {
            ConstructorDeclarationSyntax constructor => statement?.Parent == constructor.Body,
            ParenthesizedLambdaExpressionSyntax lambda => IsDirectLambdaStatement(statement, node, lambda.Body),
            SimpleLambdaExpressionSyntax lambda => IsDirectLambdaStatement(statement, node, lambda.Body),
            _ => false
        };
    }

    static bool IsDirectLambdaStatement(ExpressionStatementSyntax? statement, SyntaxNode node, CSharpSyntaxNode body) =>
        body switch
        {
            BlockSyntax block => statement?.Parent == block,
            ExpressionSyntax expression => expression == node || expression.DescendantNodesAndSelf().Contains(node),
            _ => false
        };

    static string? EnumMember(
        ExpressionSyntax expression,
        SemanticModel semanticModel,
        IReadOnlySet<string> declaringTypes)
    {
        if (semanticModel.GetSymbolInfo(expression).Symbol is IFieldSymbol field &&
            declaringTypes.Contains(DotNetSubjectIds.MetadataName(field.ContainingType)))
        {
            return field.Name;
        }

        if (semanticModel.GetTypeInfo(expression).ConvertedType is not INamedTypeSymbol enumType ||
            !declaringTypes.Contains(DotNetSubjectIds.MetadataName(enumType)) ||
            semanticModel.GetConstantValue(expression) is not { HasValue: true, Value: not null } constant)
        {
            return null;
        }

        return enumType.GetMembers()
            .OfType<IFieldSymbol>()
            .FirstOrDefault(_ => _.HasConstantValue && Equals(_.ConstantValue, constant.Value))?.Name;
    }

    static bool IsAuthoredSourceLocation(Location location, IReadOnlySet<SyntaxTree> authoredSyntaxTrees) =>
        location.IsInSource &&
        location.SourceTree is not null &&
        authoredSyntaxTrees.Contains(location.SourceTree);

    static string? ConstantString(ExpressionSyntax expression, SemanticModel semanticModel) =>
        semanticModel.GetConstantValue(expression) is { HasValue: true, Value: string value } ? value : null;

    static bool? ConstantBoolean(ExpressionSyntax expression, SemanticModel semanticModel) =>
        semanticModel.GetConstantValue(expression) is { HasValue: true, Value: bool value } ? value : null;

    static uint? ConstantUnsigned(ExpressionSyntax expression, SemanticModel semanticModel) =>
        semanticModel.GetConstantValue(expression) is { HasValue: true, Value: not null } constant
            ? constant.Value switch
            {
                byte value => value,
                ushort value => value,
                uint value => value,
                int value when value >= 0 => (uint)value,
                _ => null
            }
            : null;

    static long? ConstantLong(ExpressionSyntax expression, SemanticModel semanticModel) =>
        semanticModel.GetConstantValue(expression) is { HasValue: true, Value: not null } constant
            ? constant.Value switch
            {
                byte value => value,
                short value => value,
                ushort value => value,
                int value => value,
                uint value => value,
                long value => value,
                _ => null
            }
            : null;

    static GenerationDiagnostic Loss(
        DotNetProjectCompilation project,
        CritterStackSubjectResolver subjects,
        INamedTypeSymbol subject,
        string code,
        string message,
        Location location,
        GenerationDiagnosticOutcome outcome = GenerationDiagnosticOutcome.Unsupported) => new()
        {
            Code = code,
            Severity = GenerationDiagnosticSeverity.Warning,
            Outcome = outcome,
            Message = message,
            Source = CritterStackSource.RangeForProject(location, project),
            Subject = subjects.SubjectForType(project, subject)
        };

    static SubjectId ProjectSubject(DotNetProjectCompilation project) => new()
    {
        Value = $"dotnet:project/{project.Name}"
    };

    static string MetadataLabel(string propertyName) => propertyName == "Version" ? "version" : "name";

    static string SubscriptionLabel(string propertyName) => propertyName switch
    {
        "Name" or "SubscriptionName" => "name",
        "Version" or "SubscriptionVersion" => "version",
        "IncludeArchivedEvents" => "archived-event policy",
        _ => propertyName
    };

    sealed record DiagnosticKey(
        string Code,
        string Message,
        string? Path,
        int? Line,
        int? Column,
        SubjectId? Subject);
}
