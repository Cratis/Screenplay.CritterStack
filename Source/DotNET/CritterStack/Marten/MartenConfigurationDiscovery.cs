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
        var scope = new ProjectScope(project, adapter, subjects, facts, diagnostics);
        var sideEffectsEnabled = false;
        var projections = registrations
            .Where(_ => _.Projection is not null)
            .Select(_ => _.Projection!)
            .Distinct<INamedTypeSymbol>(SymbolEqualityComparer.Default)
            .ToArray();

        foreach (var projection in projections)
        {
            DiscoverProjectionMetadata(scope, projection);
            if (IsRawProjection(projection))
            {
                AddCustomProcessingDiagnostics(scope, projection, "custom projection");
            }
        }

        foreach (var tree in project.Compilation.SyntaxTrees.Where(project.AuthoredSyntaxTrees.Contains))
        {
            sideEffectsEnabled |= DiscoverTree(scope, tree);
        }

        return new(facts, OrderedUnique(diagnostics), sideEffectsEnabled);
    }

    public static bool IsUnresolvedProcessorType(INamedTypeSymbol type) =>
        IsSubscription(type) || IsRawProjection(type);

    static bool DiscoverTree(ProjectScope scope, SyntaxTree tree)
    {
        var semanticModel = scope.Project.Compilation.GetSemanticModel(tree);
        var root = tree.GetRoot();
        foreach (var invocation in root.DescendantNodes().OfType<InvocationExpressionSyntax>())
        {
            if (semanticModel.GetSymbolInfo(invocation).Symbol is not IMethodSymbol method)
            {
                continue;
            }

            var site = new InvocationSite(invocation, method, semanticModel);
            DiscoverRegisteredValueType(scope, site);
            DiscoverDaemonConfiguration(scope, site);
            DiscoverProjectionRegistrationMetadata(scope, site);
            DiscoverUnresolvedLifecycle(scope, site);
            DiscoverSubscriptionRegistration(scope, site);
        }

        var sideEffectsEnabled = false;
        foreach (var assignment in root.DescendantNodes().OfType<AssignmentExpressionSyntax>())
        {
            sideEffectsEnabled |= EnablesInlineProjectionSideEffects(assignment, semanticModel);
            DiscoverDaemonAssignment(scope, assignment, semanticModel);
        }

        return sideEffectsEnabled;
    }

    static IReadOnlyList<GenerationDiagnostic> OrderedUnique(List<GenerationDiagnostic> diagnostics) =>
    [
        .. diagnostics
            .GroupBy(_ => new DiagnosticKey(_.Code, _.Message, _.Source?.Path, _.Source?.StartLine, _.Source?.StartColumn, _.Subject))
            .Select(_ => _.First())
            .OrderBy(_ => _.Source?.Path, StringComparer.Ordinal)
            .ThenBy(_ => _.Source?.StartLine)
            .ThenBy(_ => _.Source?.StartColumn)
            .ThenBy(_ => _.Code, StringComparer.Ordinal)
            .ThenBy(_ => _.Message, StringComparer.Ordinal)
    ];

    static void DiscoverRegisteredValueType(ProjectScope scope, InvocationSite site)
    {
        var candidate = site.Method.OriginalDefinition;
        if (!string.Equals(candidate.Name, "RegisterValueType", StringComparison.Ordinal) ||
            DotNetSubjectIds.MetadataName(candidate.ContainingType.OriginalDefinition) != WellKnownTypes.MartenStoreOptions)
        {
            return;
        }

        var type = site.Method.TypeArguments.Length == 1
            ? site.Method.TypeArguments[0] as INamedTypeSymbol
            : TypeFromTypeOfArgument(site.Invocation, site.SemanticModel);
        if (type is null || type.TypeKind == TypeKind.Error)
        {
            return;
        }

        var subject = scope.Subjects.SubjectForType(scope.Project, type);
        var factId = $"marten:registered-value-type:{subject.Value}";
        if (scope.Facts.OfType<ArtifactFact>().Any(_ => _.Id.Value == factId))
        {
            return;
        }

        var evidence = new Evidence
        {
            Adapter = scope.Adapter,
            Strength = EvidenceStrength.Configured,
            Source = CritterStackSource.RangeForProject(site.Invocation.GetLocation(), scope.Project),
            Explanation = $"Marten registers '{type.Name}' as a value type"
        };
        scope.Facts.Add(new ArtifactFact
        {
            Id = new FactId { Value = factId },
            Subject = subject,
            Definition = new ArtifactDefinition
            {
                Key = new ArtifactKey { Subject = subject, Kind = ArtifactKind.Concept },
                Name = type.Name,
                File = CritterStackSource.EvidenceFor(type, scope.Adapter, scope.Project, EvidenceStrength.Exact).Source?.Path
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

    static void DiscoverProjectionMetadata(ProjectScope scope, INamedTypeSymbol projection)
    {
        var constructors = DeclaredConstructors(projection);
        foreach (var constructor in constructors.Where(_ => scope.Project.AuthoredSyntaxTrees.Contains(_.SyntaxTree)))
        {
            var semanticModel = scope.Project.Compilation.GetSemanticModel(constructor.SyntaxTree);
            foreach (var assignment in constructor.DescendantNodes().OfType<AssignmentExpressionSyntax>())
            {
                if (ProjectionMetadataProperty(assignment, semanticModel) is not { } property)
                {
                    continue;
                }

                var direct = constructors.Length == 1 && IsDirectConstructorStatement(assignment, constructor);
                var value = ProjectionMetadataValue(property, assignment, semanticModel);
                scope.Report(
                    projection,
                    MartenDiagnosticCodes.ProjectionMetadataOmitted,
                    ExactOrUnknown(
                        direct && value is not null,
                        $"Projection '{projection.Name}' configures projection {MetadataLabel(property.Name)} '{value}', which is not expressible in the current Screenplay contracts",
                        $"Projection '{projection.Name}' configures projection {MetadataLabel(property.Name)} with a conditional, computed, or otherwise non-constant value that could not be resolved safely"),
                    assignment.GetLocation());
            }
        }
    }

    static void DiscoverDaemonConfiguration(ProjectScope scope, InvocationSite site)
    {
        var candidate = site.Method.ReducedFrom ?? site.Method;
        if (candidate.Name != "AddAsyncDaemon" || !IsMartenServiceExpression(candidate.ContainingType))
        {
            return;
        }

        var mode = site.Invocation.ArgumentList.Arguments.FirstOrDefault()?.Expression is { } expression
            ? EnumMember(expression, site.SemanticModel, _daemonModeTypes)
            : null;
        ReportDaemonMode(scope, mode, "async daemon mode", site.Invocation.GetLocation());
    }

    static void DiscoverDaemonAssignment(
        ProjectScope scope,
        AssignmentExpressionSyntax assignment,
        SemanticModel semanticModel)
    {
        if (semanticModel.GetSymbolInfo(assignment.Left).Symbol is not IPropertySymbol { Name: "AsyncMode" } property ||
            !_daemonSettingTypes.Contains(DotNetSubjectIds.MetadataName(property.ContainingType)))
        {
            return;
        }

        var mode = EnumMember(assignment.Right, semanticModel, _daemonModeTypes);
        ReportDaemonMode(scope, mode, "projection daemon AsyncMode", assignment.GetLocation());
    }

    static void ReportDaemonMode(ProjectScope scope, string? mode, string label, Location location) =>
        scope.ReportOnProject(
            MartenDiagnosticCodes.DaemonConfigurationOmitted,
            mode is null
                ? Finding.Unknown($"Marten {label} is computed or otherwise non-constant and could not be resolved safely")
                : Finding.Unsupported($"Marten {label} '{mode}' is configured; daemon hosting and shard execution configuration are not expressible in the current Screenplay contracts"),
            location);

    static void DiscoverProjectionRegistrationMetadata(ProjectScope scope, InvocationSite site)
    {
        if (!IsProjectionRegistration(site.Method, site.Invocation, site.SemanticModel) ||
            ProjectionTypeFrom(site.Method, site.Invocation, site.SemanticModel) is not { } projection)
        {
            return;
        }

        DiscoverRegisteredProjectionName(scope, site, projection);
        DiscoverRegistrationMetadataAssignments(scope, site, projection);
    }

    static void DiscoverRegisteredProjectionName(ProjectScope scope, InvocationSite site, INamedTypeSymbol projection)
    {
        if (ArgumentForParameter(site.Invocation, site.Method, "projectionName") is not { } nameArgument)
        {
            return;
        }

        var value = ConstantString(nameArgument.Expression, site.SemanticModel);
        scope.Report(
            projection,
            MartenDiagnosticCodes.ProjectionMetadataOmitted,
            ExactOrUnknown(
                value is not null,
                $"Projection '{projection.Name}' registers projection name '{value}', which is not expressible in the current Screenplay contracts",
                $"Projection '{projection.Name}' registers a computed or otherwise non-constant projection name that could not be resolved safely"),
            nameArgument.GetLocation());
    }

    static void DiscoverRegistrationMetadataAssignments(ProjectScope scope, InvocationSite site, INamedTypeSymbol projection)
    {
        foreach (var lambda in LambdaArguments(site.Invocation))
        {
            foreach (var assignment in lambda.DescendantNodes().OfType<AssignmentExpressionSyntax>())
            {
                if (ProjectionMetadataProperty(assignment, site.SemanticModel) is not { } property)
                {
                    continue;
                }

                var value = ProjectionMetadataValue(property, assignment, site.SemanticModel);
                scope.Report(
                    projection,
                    MartenDiagnosticCodes.ProjectionMetadataOmitted,
                    ExactOrUnknown(
                        IsDirectScopeStatement(assignment, lambda) && value is not null,
                        $"Projection '{projection.Name}' registers projection {MetadataLabel(property.Name)} '{value}', which is not expressible in the current Screenplay contracts",
                        $"Projection '{projection.Name}' registers projection {MetadataLabel(property.Name)} conditionally or with a non-constant value that could not be resolved safely"),
                    assignment.GetLocation());
            }
        }
    }

    static IPropertySymbol? ProjectionMetadataProperty(AssignmentExpressionSyntax assignment, SemanticModel semanticModel) =>
        semanticModel.GetSymbolInfo(assignment.Left).Symbol is IPropertySymbol property &&
        IsProjectionMetadataProperty(property)
            ? property
            : null;

    static string? ProjectionMetadataValue(
        IPropertySymbol property,
        AssignmentExpressionSyntax assignment,
        SemanticModel semanticModel) => property.Name switch
        {
            "Name" or "ProjectionName" => ConstantString(assignment.Right, semanticModel),
            "Version" => ConstantUnsigned(assignment.Right, semanticModel)?.ToString(),
            _ => null
        };

    static IEnumerable<LambdaExpressionSyntax> LambdaArguments(InvocationExpressionSyntax invocation) =>
        invocation.ArgumentList.Arguments.Select(_ => _.Expression).OfType<LambdaExpressionSyntax>();

    static void DiscoverUnresolvedLifecycle(ProjectScope scope, InvocationSite site)
    {
        if (!IsProjectionRegistration(site.Method, site.Invocation, site.SemanticModel) ||
            !site.Method.Parameters.Any(_ => _.Name == "lifecycle"))
        {
            return;
        }

        var lifecycleArgument = ArgumentForParameter(site.Invocation, site.Method, "lifecycle");
        if (lifecycleArgument is null ||
            EnumMember(lifecycleArgument.Expression, site.SemanticModel, MartenProjectionDiscovery.ProjectionLifecycleTypes) is not null)
        {
            return;
        }

        var projection = ProjectionTypeFrom(site.Method, site.Invocation, site.SemanticModel);
        scope.Add(
            projection is null ? scope.ProjectSubject : scope.Subjects.SubjectForType(scope.Project, projection),
            MartenDiagnosticCodes.ProjectionLifecycleOmitted,
            Finding.Unknown(
                projection is null
                    ? "Marten projection lifecycle is computed or otherwise non-constant and could not be resolved safely"
                    : $"Projection '{projection.Name}' uses a computed or otherwise non-constant lifecycle that could not be resolved safely"),
            lifecycleArgument.GetLocation());
    }

    static void DiscoverSubscriptionRegistration(ProjectScope scope, InvocationSite site)
    {
        if (!IsSubscriptionRegistration(site.Method) ||
            SubscriptionTypeFrom(site.Method, site.Invocation, site.SemanticModel) is not { } subscription)
        {
            return;
        }

        scope.Report(
            subscription,
            MartenDiagnosticCodes.SubscriptionConfigurationOmitted,
            Finding.Unsupported($"Marten subscription '{subscription.Name}' is registered, but the current Screenplay contracts have no neutral subscription artifact"),
            site.Invocation.GetLocation());

        DiscoverSubscriptionConstructorConfiguration(scope, subscription);
        foreach (var lambda in LambdaArguments(site.Invocation))
        {
            DiscoverSubscriptionScopeConfiguration(new SubscriptionScope(scope, subscription, lambda, site.SemanticModel, AllowExact: true));
        }

        AddCustomProcessingDiagnostics(scope, subscription, "subscription");
    }

    static void DiscoverSubscriptionConstructorConfiguration(ProjectScope scope, INamedTypeSymbol subscription)
    {
        var constructors = DeclaredConstructors(subscription);
        foreach (var constructor in constructors.Where(_ => scope.Project.AuthoredSyntaxTrees.Contains(_.SyntaxTree)))
        {
            var semanticModel = scope.Project.Compilation.GetSemanticModel(constructor.SyntaxTree);
            DiscoverSubscriptionScopeConfiguration(
                new SubscriptionScope(scope, subscription, constructor, semanticModel, constructors.Length == 1));
        }
    }

    static ConstructorDeclarationSyntax[] DeclaredConstructors(INamedTypeSymbol type) =>
        [
            .. type.DeclaringSyntaxReferences
                .Select(_ => _.GetSyntax())
                .OfType<TypeDeclarationSyntax>()
                .SelectMany(_ => _.Members.OfType<ConstructorDeclarationSyntax>())
        ];

    static void DiscoverSubscriptionScopeConfiguration(SubscriptionScope scope)
    {
        DiscoverSubscriptionOptionAssignments(scope);
        DiscoverSubscriptionOptionInvocations(scope);
    }

    static void DiscoverSubscriptionOptionAssignments(SubscriptionScope scope)
    {
        foreach (var assignment in scope.Node.DescendantNodes().OfType<AssignmentExpressionSyntax>())
        {
            if (scope.SemanticModel.GetSymbolInfo(assignment.Left).Symbol is not IPropertySymbol property ||
                !IsSubscriptionOptionProperty(property))
            {
                continue;
            }

            var direct = scope.AllowExact && IsDirectScopeStatement(assignment, scope.Node);
            var value = SubscriptionOptionValue(property, assignment, scope.SemanticModel);
            scope.Project.Report(
                scope.Subscription,
                MartenDiagnosticCodes.SubscriptionConfigurationOmitted,
                ExactOrUnknown(
                    direct && value is not null,
                    $"Marten subscription '{scope.Subscription.Name}' configures {SubscriptionLabel(property.Name)} '{value}', which is not expressible in the current Screenplay contracts",
                    $"Marten subscription '{scope.Subscription.Name}' configures {SubscriptionLabel(property.Name)} conditionally or with a non-constant value that could not be resolved safely"),
                assignment.GetLocation());
        }
    }

    static string? SubscriptionOptionValue(
        IPropertySymbol property,
        AssignmentExpressionSyntax assignment,
        SemanticModel semanticModel) => property.Name switch
        {
            "Name" or "SubscriptionName" => ConstantString(assignment.Right, semanticModel),
            "Version" or "SubscriptionVersion" => ConstantUnsigned(assignment.Right, semanticModel)?.ToString(),
            "IncludeArchivedEvents" => ConstantBoolean(assignment.Right, semanticModel)?.ToString().ToLowerInvariant(),
            _ => null
        };

    static void DiscoverSubscriptionOptionInvocations(SubscriptionScope scope)
    {
        foreach (var invocation in scope.Node.DescendantNodes().OfType<InvocationExpressionSyntax>())
        {
            if (scope.SemanticModel.GetSymbolInfo(invocation).Symbol is not IMethodSymbol method ||
                !IsSubscriptionOptionMethod(method))
            {
                continue;
            }

            var direct = scope.AllowExact && IsDirectScopeStatement(invocation, scope.Node);
            var value = direct ? SubscriptionMethodValue(invocation, method, scope.SemanticModel) : null;
            scope.Project.Report(
                scope.Subscription,
                MartenDiagnosticCodes.SubscriptionConfigurationOmitted,
                ExactOrUnknown(
                    value is not null,
                    $"Marten subscription '{scope.Subscription.Name}' configures {value}, which is not expressible in the current Screenplay contracts",
                    $"Marten subscription '{scope.Subscription.Name}' uses {method.Name} conditionally or with arguments that could not be resolved exactly"),
                invocation.GetLocation());
        }
    }

    static string? SubscriptionMethodValue(
        InvocationExpressionSyntax invocation,
        IMethodSymbol method,
        SemanticModel semanticModel) => method.Name switch
        {
            "IncludeType" => EventTypeFilter(invocation, method, semanticModel),
            "FilterIncomingEventsOnStreamType" => StreamTypeFilter(invocation, semanticModel),
            "SubscribeFromPresent" => PresentStartingPosition(invocation, semanticModel),
            "SubscribeFromSequence" => SequenceStartingPosition(invocation, semanticModel),
            _ => null
        };

    static string? EventTypeFilter(InvocationExpressionSyntax invocation, IMethodSymbol method, SemanticModel semanticModel)
    {
        var eventType = method.TypeArguments.FirstOrDefault() as INamedTypeSymbol ??
                        TypeFromTypeOfArgument(invocation, semanticModel);
        return eventType is not null ? $"an event-type filter for '{eventType.Name}'" : null;
    }

    static string? StreamTypeFilter(InvocationExpressionSyntax invocation, SemanticModel semanticModel) =>
        TypeFromTypeOfArgument(invocation, semanticModel) is { } streamType
            ? $"a stream-type filter for '{streamType.Name}'"
            : null;

    static string? PresentStartingPosition(InvocationExpressionSyntax invocation, SemanticModel semanticModel) =>
        DatabaseSuffix(invocation, semanticModel) is { } presentDatabase
            ? $"starting position 'present'{presentDatabase}"
            : null;

    static string? SequenceStartingPosition(InvocationExpressionSyntax invocation, SemanticModel semanticModel)
    {
        if (invocation.ArgumentList.Arguments.FirstOrDefault()?.Expression is not { } sequenceExpression ||
            ConstantLong(sequenceExpression, semanticModel) is not { } sequence ||
            DatabaseSuffix(invocation, semanticModel) is not { } sequenceDatabase)
        {
            return null;
        }

        return $"starting sequence '{sequence}'{sequenceDatabase}";
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

    static void AddCustomProcessingDiagnostics(ProjectScope scope, INamedTypeSymbol type, string kind)
    {
        foreach (var method in type.GetMembers().OfType<IMethodSymbol>()
                     .Where(_ => !_.IsImplicitlyDeclared && _customProcessingMethods.Contains(_.Name)))
        {
            if (method.Locations.FirstOrDefault(_ => IsAuthoredSourceLocation(_, scope.Project.AuthoredSyntaxTrees)) is not { } location)
            {
                continue;
            }

            scope.Report(
                type,
                MartenDiagnosticCodes.CustomProcessingOmitted,
                Finding.Unsupported($"Marten {kind} '{type.Name}' has arbitrary {method.Name} consequences; no State View, Automation, Translation, document operation, message, or event consequence was inferred"),
                location);
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

        return ConstantEnumMemberName(expression, semanticModel, declaringTypes);
    }

    static string? ConstantEnumMemberName(
        ExpressionSyntax expression,
        SemanticModel semanticModel,
        IReadOnlySet<string> declaringTypes)
    {
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

    static Finding ExactOrUnknown(bool exact, string exactMessage, string unresolvedMessage) =>
        exact ? Finding.Unsupported(exactMessage) : Finding.Unknown(unresolvedMessage);

    static string MetadataLabel(string propertyName) => propertyName == "Version" ? "version" : "name";

    static string SubscriptionLabel(string propertyName) => propertyName switch
    {
        "Name" or "SubscriptionName" => "name",
        "Version" or "SubscriptionVersion" => "version",
        "IncludeArchivedEvents" => "archived-event policy",
        _ => propertyName
    };

    readonly record struct Finding(string Message, GenerationDiagnosticOutcome Outcome)
    {
        public static Finding Unsupported(string message) => new(message, GenerationDiagnosticOutcome.Unsupported);

        public static Finding Unknown(string message) => new(message, GenerationDiagnosticOutcome.Unknown);
    }

    sealed record DiagnosticKey(
        string Code,
        string Message,
        string? Path,
        int? Line,
        int? Column,
        SubjectId? Subject);

    sealed record InvocationSite(
        InvocationExpressionSyntax Invocation,
        IMethodSymbol Method,
        SemanticModel SemanticModel);

    sealed record SubscriptionScope(
        ProjectScope Project,
        INamedTypeSymbol Subscription,
        SyntaxNode Node,
        SemanticModel SemanticModel,
        bool AllowExact);

    sealed class ProjectScope(
        DotNetProjectCompilation project,
        AdapterIdentity adapter,
        CritterStackSubjectResolver subjects,
        List<GenerationFact> facts,
        List<GenerationDiagnostic> diagnostics)
    {
        public DotNetProjectCompilation Project { get; } = project;

        public AdapterIdentity Adapter { get; } = adapter;

        public CritterStackSubjectResolver Subjects { get; } = subjects;

        public List<GenerationFact> Facts { get; } = facts;

        public SubjectId ProjectSubject { get; } = new() { Value = $"dotnet:project/{project.Name}" };

        public void Report(INamedTypeSymbol subject, string code, Finding finding, Location location) =>
            Add(Subjects.SubjectForType(Project, subject), code, finding, location);

        public void ReportOnProject(string code, Finding finding, Location location) =>
            Add(ProjectSubject, code, finding, location);

        public void Add(SubjectId subject, string code, Finding finding, Location location) =>
            diagnostics.Add(new()
            {
                Code = code,
                Severity = GenerationDiagnosticSeverity.Warning,
                Outcome = finding.Outcome,
                Message = finding.Message,
                Source = CritterStackSource.RangeForProject(location, Project),
                Subject = subject
            });
    }
}
