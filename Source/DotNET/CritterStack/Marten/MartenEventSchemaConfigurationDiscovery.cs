// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Cratis.Screenplay.Generation;
using Cratis.Screenplay.Generation.DotNet;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Cratis.CritterStack.Screenplay.Marten;

static class MartenEventSchemaConfigurationDiscovery
{
    static readonly HashSet<string> _eventNamingStyles = ["ClassicTypeName", "SmarterTypeName", "FullTypeName"];
    static readonly HashSet<string> _clrUpcasterBases =
    [
        WellKnownTypes.MartenClrEventUpcaster,
        WellKnownTypes.MartenClrAsyncOnlyEventUpcaster
    ];
    static readonly HashSet<string> _rawUpcasterBases =
    [
        WellKnownTypes.MartenRawEventUpcaster,
        WellKnownTypes.MartenSystemTextJsonEventUpcaster,
        WellKnownTypes.MartenSystemTextJsonAsyncOnlyEventUpcaster,
        WellKnownTypes.MartenJsonNetEventUpcaster,
        WellKnownTypes.MartenJsonNetAsyncOnlyEventUpcaster
    ];
    static readonly HashSet<string> _asyncUpcasterBases =
    [
        WellKnownTypes.MartenClrAsyncOnlyEventUpcaster,
        WellKnownTypes.MartenSystemTextJsonAsyncOnlyEventUpcaster,
        WellKnownTypes.MartenJsonNetAsyncOnlyEventUpcaster
    ];

    public static IReadOnlyList<GenerationDiagnostic> Discover(DotNetProjectCompilation project)
    {
        var diagnostics = new List<GenerationDiagnostic>();
        foreach (var tree in project.Compilation.SyntaxTrees.Where(_ =>
                     project.AuthoredSyntaxTrees.Contains(_) &&
                     !DotNetGeneratedSource.IsGenerated(_)))
        {
            var semanticModel = project.Compilation.GetSemanticModel(tree);
            var root = tree.GetRoot();
            foreach (var invocation in root.DescendantNodes().OfType<InvocationExpressionSyntax>())
            {
                if (semanticModel.GetSymbolInfo(invocation).Symbol is not IMethodSymbol method)
                {
                    continue;
                }

                DiscoverEventTypeConfiguration(project, invocation, method, semanticModel, diagnostics);
                DiscoverUpcastConfiguration(project, invocation, method, semanticModel, diagnostics);
            }

            foreach (var assignment in root.DescendantNodes().OfType<AssignmentExpressionSyntax>())
            {
                DiscoverEventNamingStyle(project, assignment, semanticModel, diagnostics);
            }

            foreach (var attribute in root.DescendantNodes().OfType<AttributeSyntax>())
            {
                DiscoverEventAliasAttribute(project, attribute, semanticModel, diagnostics);
            }
        }

        return
        [
            .. diagnostics
                .OrderBy(_ => _.Source?.Path, StringComparer.Ordinal)
                .ThenBy(_ => _.Source?.StartLine)
                .ThenBy(_ => _.Source?.StartColumn)
                .ThenBy(_ => _.Code, StringComparer.Ordinal)
                .ThenBy(_ => _.Message, StringComparer.Ordinal)
        ];
    }

    static void DiscoverEventTypeConfiguration(
        DotNetProjectCompilation project,
        InvocationExpressionSyntax invocation,
        IMethodSymbol method,
        SemanticModel semanticModel,
        List<GenerationDiagnostic> diagnostics)
    {
        if (IsDirectMapEventType(method))
        {
            DiscoverDirectEventTypeAlias(project, invocation, method, semanticModel, diagnostics);
            return;
        }

        if (!IsEventTypeHelper(method))
        {
            return;
        }

        var eventType = NamedType(method.TypeArguments.SingleOrDefault());
        if (eventType is null)
        {
            diagnostics.Add(UnresolvedEventConfiguration(project, invocation.GetLocation()));
            return;
        }

        if (method.Name == "MapEventTypeWithNameSuffix")
        {
            var suffix = ConstantString(ArgumentForParameter(invocation, method, "suffix")?.Expression, semanticModel);
            var baseAliasArgument = ArgumentForParameter(invocation, method, "eventTypeName");
            var baseAlias = baseAliasArgument is null ? null : ConstantString(baseAliasArgument.Expression, semanticModel);
            if (suffix is null || (baseAliasArgument is not null && baseAlias is null))
            {
                diagnostics.Add(UnresolvedEventConfiguration(project, invocation.GetLocation(), eventType));
                return;
            }

            diagnostics.Add(EventConfiguration(
                project,
                eventType,
                baseAliasArgument is null
                    ? $"Marten event type '{eventType.Name}' has an authored name-suffix declaration with suffix '{DiagnosticValue(suffix)}'; its convention-derived effective storage alias was not inferred"
                    : $"Marten event type '{eventType.Name}' has an authored name-suffix declaration with base alias '{DiagnosticValue(baseAlias!)}' and suffix '{DiagnosticValue(suffix)}', deriving storage alias '{DiagnosticValue(baseAlias!)}_{DiagnosticValue(suffix)}'",
                invocation.GetLocation()));
            return;
        }

        var version = ConstantUnsigned(ArgumentForParameter(invocation, method, "schemaVersion")?.Expression, semanticModel);
        var explicitBaseAliasArgument = ArgumentForParameter(invocation, method, "eventTypeName");
        var explicitBaseAlias = explicitBaseAliasArgument is null
            ? null
            : ConstantString(explicitBaseAliasArgument.Expression, semanticModel);
        if (version is null || (explicitBaseAliasArgument is not null && explicitBaseAlias is null))
        {
            diagnostics.Add(UnresolvedEventConfiguration(project, invocation.GetLocation(), eventType));
            return;
        }

        diagnostics.Add(EventConfiguration(
            project,
            eventType,
            explicitBaseAliasArgument is null
                ? $"Marten event type '{eventType.Name}' has an authored schema-version declaration for version '{version}'; its convention-derived effective storage alias was not inferred"
                : $"Marten event type '{eventType.Name}' has an authored schema-version declaration with base alias '{DiagnosticValue(explicitBaseAlias!)}' and version '{version}', deriving storage alias '{DiagnosticValue(explicitBaseAlias!)}_v{version}'",
            invocation.GetLocation()));
    }

    static void DiscoverDirectEventTypeAlias(
        DotNetProjectCompilation project,
        InvocationExpressionSyntax invocation,
        IMethodSymbol method,
        SemanticModel semanticModel,
        List<GenerationDiagnostic> diagnostics)
    {
        var eventType = method.TypeArguments.Length == 1
            ? NamedType(method.TypeArguments[0])
            : TypeFromTypeOf(ArgumentForParameter(invocation, method, "eventType")?.Expression, semanticModel);
        var alias = ConstantString(ArgumentForParameter(invocation, method, "eventTypeName")?.Expression, semanticModel);
        if (eventType is null || alias is null)
        {
            diagnostics.Add(UnresolvedEventConfiguration(project, invocation.GetLocation(), eventType));
            return;
        }

        diagnostics.Add(EventConfiguration(
            project,
            eventType,
            $"Marten event type '{eventType.Name}' has authored storage alias '{DiagnosticValue(alias)}'",
            invocation.GetLocation()));
    }

    static void DiscoverEventNamingStyle(
        DotNetProjectCompilation project,
        AssignmentExpressionSyntax assignment,
        SemanticModel semanticModel,
        List<GenerationDiagnostic> diagnostics)
    {
        if (semanticModel.GetSymbolInfo(assignment.Left).Symbol is not IPropertySymbol { Name: "EventNamingStyle" } property ||
            DotNetSubjectIds.MetadataName(property.ContainingType.OriginalDefinition) != WellKnownTypes.MartenEventStoreOptions)
        {
            return;
        }

        var style = EnumMember(assignment.Right, semanticModel, WellKnownTypes.JasperFxEventNamingStyle, _eventNamingStyles);
        diagnostics.Add(style is null
            ? UnresolvedEventConfiguration(project, assignment.GetLocation())
            : EventConfiguration(
                project,
                null,
                $"Marten has an authored global event naming-style declaration '{style}'; runtime precedence and effective aliases were not inferred",
                assignment.GetLocation()));
    }

    static void DiscoverEventAliasAttribute(
        DotNetProjectCompilation project,
        AttributeSyntax attribute,
        SemanticModel semanticModel,
        List<GenerationDiagnostic> diagnostics)
    {
        if (semanticModel.GetSymbolInfo(attribute).Symbol is not IMethodSymbol constructor ||
            DotNetSubjectIds.MetadataName(constructor.ContainingType) != WellKnownTypes.MartenEventAttribute ||
            attribute.ArgumentList?.Arguments.FirstOrDefault(_ => _.NameEquals?.Name.Identifier.ValueText == "Alias") is not { } aliasArgument ||
            ConstantString(aliasArgument.Expression, semanticModel) is not { Length: > 0 } alias ||
            attribute.FirstAncestorOrSelf<TypeDeclarationSyntax>() is not { } declaration ||
            semanticModel.GetDeclaredSymbol(declaration) is not INamedTypeSymbol eventType)
        {
            return;
        }

        diagnostics.Add(EventConfiguration(
            project,
            eventType,
            $"Marten event type '{eventType.Name}' has authored MartenEvent alias '{DiagnosticValue(alias)}'; the alias only applies when Marten AutoRegister discovers the type",
            attribute.GetLocation()));
    }

    static void DiscoverUpcastConfiguration(
        DotNetProjectCompilation project,
        InvocationExpressionSyntax invocation,
        IMethodSymbol method,
        SemanticModel semanticModel,
        List<GenerationDiagnostic> diagnostics)
    {
        if (IsDirectUpcast(method))
        {
            DiscoverDirectUpcast(project, invocation, method, semanticModel, diagnostics);
            return;
        }

        if (IsExtensionUpcast(method))
        {
            DiscoverExtensionUpcast(project, invocation, method, semanticModel, diagnostics);
        }
    }

    static void DiscoverDirectUpcast(
        DotNetProjectCompilation project,
        InvocationExpressionSyntax invocation,
        IMethodSymbol method,
        SemanticModel semanticModel,
        List<GenerationDiagnostic> diagnostics)
    {
        if (IsGenericUpcasterRegistration(method))
        {
            var upcasterType = NamedType(method.TypeArguments[0]);
            if (upcasterType is null)
            {
                diagnostics.Add(UnresolvedUpcastConfiguration(project, invocation.GetLocation()));
                return;
            }

            if (HasOnlyGeneratedSource(upcasterType, project))
            {
                diagnostics.Add(UnresolvedUpcastConfiguration(project, invocation.GetLocation()));
                return;
            }

            if (UpcasterShapeOf(upcasterType, project) is { } shape)
            {
                diagnostics.Add(UpcastConfiguration(project, shape.Subject, ClassUpcastMessage(upcasterType, shape), invocation.GetLocation()));
            }
            return;
        }

        if (IsInlineUpcasterRegistration(method))
        {
            DiscoverInlineUpcasterRegistration(project, invocation, semanticModel, diagnostics);
            return;
        }

        if (TryDirectRawJsonUpcast(method, out var targetFromGeneric))
        {
            var target = targetFromGeneric
                ? NamedType(method.TypeArguments[0])
                : TypeFromTypeOf(ArgumentForParameter(invocation, method, "eventType")?.Expression, semanticModel);
            var rawAlias = ConstantString(ArgumentForParameter(invocation, method, "eventTypeName")?.Expression, semanticModel);
            if (target is null || rawAlias is null)
            {
                diagnostics.Add(UnresolvedUpcastConfiguration(project, invocation.GetLocation(), target));
                return;
            }

            diagnostics.Add(UpcastConfiguration(
                project,
                target,
                $"Marten has an authored raw JSON upcast declaration from unknown source schema alias '{DiagnosticValue(rawAlias)}' to '{target.Name}'",
                invocation.GetLocation()));
            return;
        }

        if (!TryTypedUpcast(method, out var isAsync))
        {
            return;
        }

        var oldEvent = NamedType(method.TypeArguments[0]);
        var newEvent = NamedType(method.TypeArguments[1]);
        var aliasArgument = ArgumentForParameter(invocation, method, "eventTypeName");
        var alias = aliasArgument is null ? null : ConstantString(aliasArgument.Expression, semanticModel);
        if (oldEvent is null || newEvent is null || (aliasArgument is not null && alias is null))
        {
            diagnostics.Add(UnresolvedUpcastConfiguration(project, invocation.GetLocation(), newEvent));
            return;
        }

        diagnostics.Add(UpcastConfiguration(
            project,
            newEvent,
            aliasArgument is null
                ? $"Marten has an authored {ShapeLabel(isAsync)} typed upcast declaration '{oldEvent.Name} -> {newEvent.Name}' using a convention-derived storage alias that was not inferred"
                : $"Marten has an authored {ShapeLabel(isAsync)} typed upcast declaration '{oldEvent.Name} -> {newEvent.Name}' for storage alias '{DiagnosticValue(alias!)}'",
            invocation.GetLocation()));
    }

    static void DiscoverExtensionUpcast(
        DotNetProjectCompilation project,
        InvocationExpressionSyntax invocation,
        IMethodSymbol method,
        SemanticModel semanticModel,
        List<GenerationDiagnostic> diagnostics)
    {
        if (TryConventionRawJsonUpcast(method, out var conventionTargetFromGeneric))
        {
            var conventionTarget = conventionTargetFromGeneric
                ? NamedType(method.TypeArguments[0])
                : TypeFromTypeOf(ArgumentForParameter(invocation, method, "eventType")?.Expression, semanticModel);
            if (conventionTarget is null)
            {
                diagnostics.Add(UnresolvedUpcastConfiguration(project, invocation.GetLocation()));
                return;
            }

            diagnostics.Add(UpcastConfiguration(
                project,
                conventionTarget,
                $"Marten has an authored raw JSON upcast declaration from an unknown source schema to '{conventionTarget.Name}' using a convention-derived storage alias that was not inferred",
                invocation.GetLocation()));
            return;
        }

        var version = ConstantUnsigned(ArgumentForParameter(invocation, method, "schemaVersion")?.Expression, semanticModel);
        if (TrySchemaVersionRawJsonUpcast(method, out var targetFromGeneric))
        {
            var target = targetFromGeneric
                ? NamedType(method.TypeArguments[0])
                : TypeFromTypeOf(ArgumentForParameter(invocation, method, "eventType")?.Expression, semanticModel);
            if (target is null || version is null)
            {
                diagnostics.Add(UnresolvedUpcastConfiguration(project, invocation.GetLocation(), target));
                return;
            }

            diagnostics.Add(UpcastConfiguration(
                project,
                target,
                $"Marten has an authored raw JSON upcast declaration from unknown source schema version '{version}' to '{target.Name}'; its convention-derived effective storage alias was not inferred",
                invocation.GetLocation()));
            return;
        }

        if (!TryTypedUpcast(method, out var isAsync))
        {
            return;
        }

        var oldEvent = NamedType(method.TypeArguments[0]);
        var newEvent = NamedType(method.TypeArguments[1]);
        if (oldEvent is null || newEvent is null || version is null)
        {
            diagnostics.Add(UnresolvedUpcastConfiguration(project, invocation.GetLocation(), newEvent));
            return;
        }

        diagnostics.Add(UpcastConfiguration(
            project,
            newEvent,
            $"Marten has an authored {ShapeLabel(isAsync)} typed upcast declaration '{oldEvent.Name} -> {newEvent.Name}' for source schema version '{version}'; its convention-derived effective storage alias was not inferred",
            invocation.GetLocation()));
    }

    static void DiscoverInlineUpcasterRegistration(
        DotNetProjectCompilation project,
        InvocationExpressionSyntax invocation,
        SemanticModel semanticModel,
        List<GenerationDiagnostic> diagnostics)
    {
        var registrations = new List<(ObjectCreationExpressionSyntax Creation, INamedTypeSymbol Type, UpcasterShape Shape)>();
        foreach (var argument in invocation.ArgumentList.Arguments)
        {
            if (argument.Expression is not ObjectCreationExpressionSyntax creation ||
                semanticModel.GetTypeInfo(creation).Type is not INamedTypeSymbol upcasterType ||
                HasOnlyGeneratedSource(upcasterType, project) ||
                UpcasterShapeOf(upcasterType, project) is not { } shape)
            {
                diagnostics.Add(UnresolvedUpcastConfiguration(project, invocation.GetLocation()));
                return;
            }

            registrations.Add((creation, upcasterType, shape));
        }

        if (registrations.Count == 0)
        {
            return;
        }

        diagnostics.AddRange(registrations.Select(_ => UpcastConfiguration(
            project,
            _.Shape.Subject,
            ClassUpcastMessage(_.Type, _.Shape),
            _.Creation.GetLocation())));
    }

    static bool IsDirectMapEventType(IMethodSymbol method)
    {
        var candidate = method.OriginalDefinition;
        if (candidate.Name != "MapEventType" ||
            DotNetSubjectIds.MetadataName(candidate.ContainingType.OriginalDefinition) != WellKnownTypes.MartenEventStoreOptions)
        {
            return false;
        }

        return candidate switch
        {
            { TypeParameters.Length: 1, Parameters.Length: 1 } => IsString(candidate.Parameters[0].Type),
            { TypeParameters.Length: 0, Parameters.Length: 2 } =>
                IsSystemType(candidate.Parameters[0].Type) && IsString(candidate.Parameters[1].Type),
            _ => false
        };
    }

    static bool IsEventTypeHelper(IMethodSymbol method)
    {
        var candidate = (method.ReducedFrom ?? method).OriginalDefinition;
        if (!candidate.IsExtensionMethod ||
            DotNetSubjectIds.MetadataName(candidate.ContainingType.OriginalDefinition) != WellKnownTypes.MartenEventStoreOptionsExtensions ||
            candidate.TypeParameters.Length != 1 ||
            !HasEventStoreReceiver(candidate) ||
            method.TypeArguments.Length != 1)
        {
            return false;
        }

        return candidate.Name switch
        {
            "MapEventTypeWithNameSuffix" => candidate.Parameters.Length switch
            {
                2 => IsString(candidate.Parameters[1].Type),
                3 => IsString(candidate.Parameters[1].Type) && IsString(candidate.Parameters[2].Type),
                _ => false
            },
            "MapEventTypeWithSchemaVersion" => candidate.Parameters.Length switch
            {
                2 => IsUnsigned(candidate.Parameters[1].Type),
                3 => IsString(candidate.Parameters[1].Type) && IsUnsigned(candidate.Parameters[2].Type),
                _ => false
            },
            _ => false
        };
    }

    static bool IsDirectUpcast(IMethodSymbol method)
    {
        var candidate = method.OriginalDefinition;
        return candidate.Name == "Upcast" &&
               DotNetSubjectIds.MetadataName(candidate.ContainingType.OriginalDefinition) == WellKnownTypes.MartenEventStoreOptions;
    }

    static bool IsExtensionUpcast(IMethodSymbol method)
    {
        var candidate = (method.ReducedFrom ?? method).OriginalDefinition;
        return candidate.IsExtensionMethod &&
               string.Equals(candidate.Name, "Upcast", StringComparison.Ordinal) &&
               DotNetSubjectIds.MetadataName(candidate.ContainingType.OriginalDefinition) == WellKnownTypes.MartenEventStoreOptionsExtensions &&
               HasEventStoreReceiver(candidate);
    }

    static bool IsGenericUpcasterRegistration(IMethodSymbol method)
    {
        var candidate = method.OriginalDefinition;
        return candidate.TypeParameters is [{ HasConstructorConstraint: true } upcaster] &&
               candidate.Parameters.Length == 0 &&
               upcaster.ConstraintTypes.Any(_ => MetadataName(_) == WellKnownTypes.MartenEventUpcasterInterface);
    }

    static bool IsInlineUpcasterRegistration(IMethodSymbol method)
    {
        var candidate = method.OriginalDefinition;
        return candidate.TypeParameters.Length == 0 &&
               candidate.Parameters is [{ IsParams: true, Type: IArrayTypeSymbol array }] &&
               MetadataName(array.ElementType) == WellKnownTypes.MartenEventUpcasterInterface;
    }

    static bool TryDirectRawJsonUpcast(IMethodSymbol method, out bool targetFromGeneric)
    {
        var candidate = method.OriginalDefinition;
        targetFromGeneric = candidate.TypeParameters.Length == 1;
        return candidate switch
        {
            { TypeParameters.Length: 1, Parameters.Length: 2 } =>
                IsString(candidate.Parameters[0].Type) && IsJsonTransformation(candidate.Parameters[1].Type),
            { TypeParameters.Length: 0, Parameters.Length: 3 } =>
                IsSystemType(candidate.Parameters[0].Type) &&
                IsString(candidate.Parameters[1].Type) &&
                IsJsonTransformation(candidate.Parameters[2].Type),
            _ => false
        };
    }

    static bool TryConventionRawJsonUpcast(IMethodSymbol method, out bool targetFromGeneric)
    {
        var candidate = (method.ReducedFrom ?? method).OriginalDefinition;
        targetFromGeneric = candidate.TypeParameters.Length == 1;
        return candidate switch
        {
            { TypeParameters.Length: 1, Parameters.Length: 2 } => IsJsonTransformation(candidate.Parameters[1].Type),
            { TypeParameters.Length: 0, Parameters.Length: 3 } =>
                IsSystemType(candidate.Parameters[1].Type) && IsJsonTransformation(candidate.Parameters[2].Type),
            _ => false
        };
    }

    static bool TrySchemaVersionRawJsonUpcast(IMethodSymbol method, out bool targetFromGeneric)
    {
        var candidate = (method.ReducedFrom ?? method).OriginalDefinition;
        targetFromGeneric = candidate.TypeParameters.Length == 1;
        return candidate switch
        {
            { TypeParameters.Length: 1, Parameters.Length: 3 } =>
                IsUnsigned(candidate.Parameters[1].Type) && IsJsonTransformation(candidate.Parameters[2].Type),
            { TypeParameters.Length: 0, Parameters.Length: 4 } =>
                IsSystemType(candidate.Parameters[1].Type) &&
                IsUnsigned(candidate.Parameters[2].Type) &&
                IsJsonTransformation(candidate.Parameters[3].Type),
            _ => false
        };
    }

    static bool TryTypedUpcast(IMethodSymbol method, out bool isAsync)
    {
        var candidate = (method.ReducedFrom ?? method).OriginalDefinition;
        isAsync = false;
        if (candidate.TypeParameters.Length != 2)
        {
            return false;
        }

        var delegateParameter = candidate.Parameters.LastOrDefault();
        if (delegateParameter is null || !TryTypedUpcastDelegate(delegateParameter.Type, candidate.TypeParameters, out isAsync))
        {
            return false;
        }

        var configurationParameters = candidate.Parameters.Take(candidate.Parameters.Length - 1).ToArray();
        if (!candidate.IsExtensionMethod)
        {
            return configurationParameters.Length switch
            {
                0 => true,
                1 => IsString(configurationParameters[0].Type),
                _ => false
            };
        }

        return configurationParameters.Length == 2 &&
               IsEventStoreOptions(configurationParameters[0].Type) &&
               IsUnsigned(configurationParameters[1].Type);
    }

    static bool TryTypedUpcastDelegate(
        ITypeSymbol type,
        IReadOnlyList<ITypeParameterSymbol> typeParameters,
        out bool isAsync)
    {
        isAsync = false;
        if (type is not INamedTypeSymbol delegateType)
        {
            return false;
        }

        if (DotNetSubjectIds.MetadataName(delegateType.OriginalDefinition) == "System.Func`2" &&
            SymbolEqualityComparer.Default.Equals(delegateType.TypeArguments[0], typeParameters[0]) &&
            SymbolEqualityComparer.Default.Equals(delegateType.TypeArguments[1], typeParameters[1]))
        {
            return true;
        }

        if (DotNetSubjectIds.MetadataName(delegateType.OriginalDefinition) != "System.Func`3" ||
            !SymbolEqualityComparer.Default.Equals(delegateType.TypeArguments[0], typeParameters[0]) ||
            MetadataName(delegateType.TypeArguments[1]) != "System.Threading.CancellationToken" ||
            delegateType.TypeArguments[2] is not INamedTypeSymbol taskType ||
            DotNetSubjectIds.MetadataName(taskType.OriginalDefinition) != "System.Threading.Tasks.Task`1" ||
            !SymbolEqualityComparer.Default.Equals(taskType.TypeArguments[0], typeParameters[1]))
        {
            return false;
        }

        isAsync = true;
        return true;
    }

    static UpcasterShape? UpcasterShapeOf(INamedTypeSymbol upcasterType, DotNetProjectCompilation project)
    {
        for (var derived = upcasterType; derived.BaseType is { } current; derived = current)
        {
            var metadataName = DotNetSubjectIds.MetadataName(current.OriginalDefinition);
            if (!_clrUpcasterBases.Contains(metadataName) && !_rawUpcasterBases.Contains(metadataName))
            {
                continue;
            }

            if (!HasAuthoredOrMetadataBaseEdge(derived, current, project))
            {
                return null;
            }

            if (_clrUpcasterBases.Contains(metadataName) &&
                current.TypeArguments is [INamedTypeSymbol oldEvent, INamedTypeSymbol newEvent])
            {
                return new(oldEvent, newEvent, _asyncUpcasterBases.Contains(metadataName), false);
            }

            if (_rawUpcasterBases.Contains(metadataName) &&
                current.TypeArguments is [INamedTypeSymbol target])
            {
                return new(null, target, _asyncUpcasterBases.Contains(metadataName), true);
            }
        }

        return null;
    }

    static string ClassUpcastMessage(INamedTypeSymbol upcasterType, UpcasterShape shape) => shape.IsRaw
        ? $"Marten has an authored {ShapeLabel(shape.IsAsync)} class-upcaster registration '{upcasterType.Name}' from unknown JSON source schema to '{shape.Target.Name}'; EventTypeName overrides and effective aliases were not inspected"
        : $"Marten has an authored {ShapeLabel(shape.IsAsync)} class-upcaster registration '{upcasterType.Name}' for '{shape.Source!.Name} -> {shape.Target.Name}'; EventTypeName overrides and effective aliases were not inspected";

    static GenerationDiagnostic EventConfiguration(
        DotNetProjectCompilation project,
        INamedTypeSymbol? eventType,
        string message,
        Location location) => new()
    {
        Code = MartenDiagnosticCodes.EventTypeConfigurationOmitted,
        Severity = GenerationDiagnosticSeverity.Warning,
        Message = $"{message}. This authored declaration is retained as diagnostic evidence only; it does not rename, version, originate, or duplicate a Screenplay Event artifact, and runtime execution or precedence is not asserted",
        Source = DotNetSource.Range(location, project.SourceRoot),
        Subject = eventType is null ? ProjectSubject(project) : project.SubjectForType(eventType)
    };

    static GenerationDiagnostic UnresolvedEventConfiguration(
        DotNetProjectCompilation project,
        Location location,
        INamedTypeSymbol? eventType = null) => EventConfiguration(
        project,
        eventType,
        "Marten has an authored event alias, schema-version, or naming-style declaration with a computed or otherwise non-constant value that could not be resolved safely; no storage alias, suffix, version, naming style, or effective value was guessed",
        location);

    static GenerationDiagnostic UpcastConfiguration(
        DotNetProjectCompilation project,
        INamedTypeSymbol? target,
        string message,
        Location location) => new()
    {
        Code = MartenDiagnosticCodes.EventUpcastConfigurationOmitted,
        Severity = GenerationDiagnosticSeverity.Warning,
        Message = $"{message}. This authored declaration is retained as diagnostic evidence only; it does not originate Event or Upcast artifacts or infer behavioral relationships, and runtime execution, reachability, ordering, or precedence is not asserted",
        Source = DotNetSource.Range(location, project.SourceRoot),
        Subject = target is null ? ProjectSubject(project) : project.SubjectForType(target)
    };

    static GenerationDiagnostic UnresolvedUpcastConfiguration(
        DotNetProjectCompilation project,
        Location location,
        INamedTypeSymbol? target = null) => UpcastConfiguration(
        project,
        target,
        "Marten has an authored upcast declaration with a computed, indirect, mixed inline collection, or otherwise unresolved type, alias, or schema version; no source type, target type, alias, version, shape, ordering, or effective value was guessed",
        location);

    static bool HasEventStoreReceiver(IMethodSymbol method) =>
        method.IsExtensionMethod &&
        method.Parameters.Length > 0 &&
        IsEventStoreOptions(method.Parameters[0].Type);

    static bool IsEventStoreOptions(ITypeSymbol type) =>
        MetadataName(type) == WellKnownTypes.MartenEventStoreOptions;

    static bool IsString(ITypeSymbol type) => type.SpecialType == SpecialType.System_String;

    static bool IsSystemType(ITypeSymbol type) => MetadataName(type) == "System.Type";

    static bool IsUnsigned(ITypeSymbol type) => type.SpecialType == SpecialType.System_UInt32;

    static bool IsJsonTransformation(ITypeSymbol type) =>
        MetadataName(type) == WellKnownTypes.MartenJsonTransformation;

    static bool HasOnlyGeneratedSource(INamedTypeSymbol type, DotNetProjectCompilation project)
    {
        var sourceTrees = type.Locations
            .Where(_ => _.IsInSource && _.SourceTree is not null)
            .Select(_ => _.SourceTree!)
            .ToArray();
        return sourceTrees.Length > 0 &&
               sourceTrees.All(_ => !project.AuthoredSyntaxTrees.Contains(_) || DotNetGeneratedSource.IsGenerated(_));
    }

    static bool HasAuthoredOrMetadataBaseEdge(
        INamedTypeSymbol derived,
        INamedTypeSymbol baseType,
        DotNetProjectCompilation project)
    {
        var declarations = derived.DeclaringSyntaxReferences
            .Select(_ => _.GetSyntax())
            .OfType<TypeDeclarationSyntax>()
            .ToArray();
        if (declarations.Length == 0)
        {
            return true;
        }

        foreach (var declaration in declarations.Where(_ =>
                     project.AuthoredSyntaxTrees.Contains(_.SyntaxTree) &&
                     !DotNetGeneratedSource.IsGenerated(_.SyntaxTree)))
        {
            var semanticModel = project.Compilation.GetSemanticModel(declaration.SyntaxTree);
            if (declaration.BaseList?.Types.Any(_ =>
                    semanticModel.GetTypeInfo(_.Type).Type is INamedTypeSymbol declaredBase &&
                    SymbolEqualityComparer.Default.Equals(declaredBase.OriginalDefinition, baseType.OriginalDefinition)) == true)
            {
                return true;
            }
        }

        return false;
    }

    static string? MetadataName(ITypeSymbol type) => type is INamedTypeSymbol named
        ? DotNetSubjectIds.MetadataName(named.OriginalDefinition)
        : null;

    static INamedTypeSymbol? NamedType(ITypeSymbol? type) => type is INamedTypeSymbol { TypeKind: not TypeKind.Error } named
        ? named
        : null;

    static INamedTypeSymbol? TypeFromTypeOf(ExpressionSyntax? expression, SemanticModel semanticModel) =>
        expression is TypeOfExpressionSyntax typeOf && semanticModel.GetTypeInfo(typeOf.Type).Type is INamedTypeSymbol { TypeKind: not TypeKind.Error } type
            ? type
            : null;

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

        var parameter = method.Parameters.FirstOrDefault(_ => _.Name == parameterName);
        var parameterIndex = parameter is null ? -1 : method.Parameters.IndexOf(parameter);
        return parameterIndex >= 0 && parameterIndex < invocation.ArgumentList.Arguments.Count
            ? invocation.ArgumentList.Arguments[parameterIndex]
            : null;
    }

    static string? ConstantString(ExpressionSyntax? expression, SemanticModel semanticModel) =>
        expression is not null && semanticModel.GetConstantValue(expression) is { HasValue: true, Value: string value }
            ? value
            : null;

    static uint? ConstantUnsigned(ExpressionSyntax? expression, SemanticModel semanticModel) =>
        expression is not null && semanticModel.GetConstantValue(expression) is { HasValue: true, Value: not null } constant
            ? constant.Value switch
            {
                byte value => value,
                ushort value => value,
                uint value => value,
                int value when value >= 0 => (uint)value,
                _ => null
            }
            : null;

    static string? EnumMember(
        ExpressionSyntax expression,
        SemanticModel semanticModel,
        string enumMetadataName,
        HashSet<string> admittedMembers)
    {
        if (semanticModel.GetSymbolInfo(expression).Symbol is IFieldSymbol field &&
            DotNetSubjectIds.MetadataName(field.ContainingType) == enumMetadataName &&
            admittedMembers.Contains(field.Name))
        {
            return field.Name;
        }

        if (semanticModel.GetTypeInfo(expression).ConvertedType is not INamedTypeSymbol enumType ||
            DotNetSubjectIds.MetadataName(enumType) != enumMetadataName ||
            semanticModel.GetConstantValue(expression) is not { HasValue: true, Value: not null } constant)
        {
            return null;
        }

        return enumType.GetMembers()
            .OfType<IFieldSymbol>()
            .FirstOrDefault(_ =>
                admittedMembers.Contains(_.Name) &&
                _.HasConstantValue &&
                Equals(_.ConstantValue, constant.Value))?.Name;
    }

    static string DiagnosticValue(string value) => string.Concat(value.Select(_ =>
        char.IsControl(_) ? $"\\u{(int)_:x4}" : _.ToString()));

    static string ShapeLabel(bool isAsync) => isAsync ? "async-only" : "sync";

    static SubjectId ProjectSubject(DotNetProjectCompilation project) => new()
    {
        Value = $"dotnet:project/{project.Name}"
    };

    sealed record UpcasterShape(
        INamedTypeSymbol? Source,
        INamedTypeSymbol Target,
        bool IsAsync,
        bool IsRaw)
    {
        public INamedTypeSymbol Subject => Target;
    }
}
