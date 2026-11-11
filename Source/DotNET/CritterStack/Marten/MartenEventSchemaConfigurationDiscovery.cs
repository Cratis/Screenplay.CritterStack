// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Cratis.Screenplay.Generation;
using Cratis.Screenplay.Generation.DotNet;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Cratis.CritterStack.Screenplay.Marten;

sealed class MartenEventSchemaConfigurationDiscovery(
    DotNetProjectCompilation project,
    CritterStackSubjectResolver subjects,
    SemanticModel semanticModel,
    List<GenerationDiagnostic> diagnostics)
{
    const string EventConfigurationNote = ". This authored declaration is retained as diagnostic evidence only; it does not rename, version, originate, or duplicate a Screenplay Event artifact, and runtime execution or precedence is not asserted";
    const string UpcastConfigurationNote = ". This authored declaration is retained as diagnostic evidence only; it does not originate Event or Upcast artifacts or infer behavioral relationships, and runtime execution, reachability, ordering, or precedence is not asserted";

    static readonly HashSet<string> _eventNamingStyles = ["ClassicTypeName", "SmarterTypeName", "FullTypeName"];
    static readonly HashSet<string> _eventAppendModes = ["Quick", "QuickWithServerTimestamps", "Rich"];
    static readonly HashSet<string> _streamIdentities = ["AsGuid", "AsString"];
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

    public static IReadOnlyList<GenerationDiagnostic> Discover(
        DotNetProjectCompilation project,
        CritterStackSubjectResolver subjects)
    {
        var diagnostics = new List<GenerationDiagnostic>();
        foreach (var tree in project.Compilation.SyntaxTrees.Where(_ =>
                     project.AuthoredSyntaxTrees.Contains(_) &&
                     !DotNetGeneratedSource.IsGenerated(_)))
        {
            var semanticModel = project.Compilation.GetSemanticModel(tree);
            var scan = new MartenEventSchemaConfigurationDiscovery(project, subjects, semanticModel, diagnostics);
            var root = tree.GetRoot();
            foreach (var invocation in root.DescendantNodes().OfType<InvocationExpressionSyntax>())
            {
                if (semanticModel.GetSymbolInfo(invocation).Symbol is not IMethodSymbol method)
                {
                    continue;
                }

                scan.DiscoverBinarySerializerConfiguration(invocation, method);
                scan.DiscoverEventTypeConfiguration(invocation, method);
                scan.DiscoverUpcastConfiguration(invocation, method);
            }

            foreach (var assignment in root.DescendantNodes().OfType<AssignmentExpressionSyntax>())
            {
                scan.DiscoverWireConfiguration(assignment);
                scan.DiscoverEventNamingStyle(assignment);
            }

            foreach (var attribute in root.DescendantNodes().OfType<AttributeSyntax>())
            {
                scan.DiscoverEventAliasAttribute(attribute);
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

    static WireSetting? WireSettingFor(string propertyName) => propertyName switch
    {
        "AppendMode" => new(WellKnownTypes.JasperFxEventAppendMode, _eventAppendModes, "event append-mode"),
        "StreamIdentity" => new(WellKnownTypes.JasperFxStreamIdentity, _streamIdentities, "stream-identity"),
        _ => null
    };

    static string WireMessage(string label, string? value) => value is null
        ? $"Marten has an authored {label} declaration with a computed or otherwise unresolved value; no effective setting was guessed"
        : $"Marten has an authored {label} declaration '{value}'";

    static string NameSuffixMessage(INamedTypeSymbol eventType, string suffix, string? baseAlias) => baseAlias is null
        ? $"Marten event type '{eventType.Name}' has an authored name-suffix declaration with suffix '{DiagnosticValue(suffix)}'; its convention-derived effective storage alias was not inferred"
        : $"Marten event type '{eventType.Name}' has an authored name-suffix declaration with base alias '{DiagnosticValue(baseAlias)}' and suffix '{DiagnosticValue(suffix)}', deriving storage alias '{DiagnosticValue(baseAlias)}_{DiagnosticValue(suffix)}'";

    static string SchemaVersionMessage(INamedTypeSymbol eventType, uint version, string? baseAlias) => baseAlias is null
        ? $"Marten event type '{eventType.Name}' has an authored schema-version declaration for version '{version}'; its convention-derived effective storage alias was not inferred"
        : $"Marten event type '{eventType.Name}' has an authored schema-version declaration with base alias '{DiagnosticValue(baseAlias)}' and version '{version}', deriving storage alias '{DiagnosticValue(baseAlias)}_v{version}'";

    static string TypedUpcastAliasMessage(INamedTypeSymbol oldEvent, INamedTypeSymbol newEvent, bool isAsync, string? alias) => alias is null
        ? $"Marten has an authored {ShapeLabel(isAsync)} typed upcast declaration '{oldEvent.Name} -> {newEvent.Name}' using a convention-derived storage alias that was not inferred"
        : $"Marten has an authored {ShapeLabel(isAsync)} typed upcast declaration '{oldEvent.Name} -> {newEvent.Name}' for storage alias '{DiagnosticValue(alias)}'";

    static string ClassUpcastMessage(INamedTypeSymbol upcasterType, UpcasterShape shape) => shape.IsRaw
        ? $"Marten has an authored {ShapeLabel(shape.IsAsync)} class-upcaster registration '{upcasterType.Name}' from unknown JSON source schema to '{shape.Target.Name}'; EventTypeName overrides and effective aliases were not inspected"
        : $"Marten has an authored {ShapeLabel(shape.IsAsync)} class-upcaster registration '{upcasterType.Name}' for '{shape.Source!.Name} -> {shape.Target.Name}'; EventTypeName overrides and effective aliases were not inspected";

    static bool IsDirectMapEventType(IMethodSymbol method)
    {
        var candidate = method.OriginalDefinition;
        if (candidate.Name != "MapEventType" ||
            DotNetSubjectIds.MetadataName(candidate.ContainingType.OriginalDefinition) != WellKnownTypes.MartenEventStoreOptions)
        {
            return false;
        }

        return candidate.TypeParameters.Length switch
        {
            1 => HasParameters(candidate, IsString),
            0 => HasParameters(candidate, IsSystemType, IsString),
            _ => false
        };
    }

    static bool IsEventTypeHelper(IMethodSymbol method)
    {
        var candidate = (method.ReducedFrom ?? method).OriginalDefinition;
        if (!IsEventStoreOptionsExtension(candidate) ||
            candidate.TypeParameters.Length != 1 ||
            method.TypeArguments.Length != 1)
        {
            return false;
        }

        return HasEventTypeHelperParameters(candidate);
    }

    static bool HasEventTypeHelperParameters(IMethodSymbol candidate) => candidate.Name switch
    {
        "MapEventTypeWithNameSuffix" =>
            HasParameters(candidate, null, IsString) || HasParameters(candidate, null, IsString, IsString),
        "MapEventTypeWithSchemaVersion" =>
            HasParameters(candidate, null, IsUnsigned) || HasParameters(candidate, null, IsString, IsUnsigned),
        _ => false
    };

    static bool IsDirectUpcast(IMethodSymbol method)
    {
        var candidate = method.OriginalDefinition;
        return candidate.Name == "Upcast" &&
               DotNetSubjectIds.MetadataName(candidate.ContainingType.OriginalDefinition) == WellKnownTypes.MartenEventStoreOptions;
    }

    static bool IsExtensionUpcast(IMethodSymbol method)
    {
        var candidate = (method.ReducedFrom ?? method).OriginalDefinition;
        return string.Equals(candidate.Name, "Upcast", StringComparison.Ordinal) && IsEventStoreOptionsExtension(candidate);
    }

    static bool IsEventStoreOptionsExtension(IMethodSymbol candidate) =>
        candidate.IsExtensionMethod &&
        DotNetSubjectIds.MetadataName(candidate.ContainingType.OriginalDefinition) == WellKnownTypes.MartenEventStoreOptionsExtensions &&
        HasEventStoreReceiver(candidate);

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

    static bool TryDirectRawJsonUpcast(IMethodSymbol method, out bool targetFromGeneric) =>
        TryRawJsonUpcast(
            method.OriginalDefinition,
            [IsString, IsJsonTransformation],
            [IsSystemType, IsString, IsJsonTransformation],
            out targetFromGeneric);

    static bool TryConventionRawJsonUpcast(IMethodSymbol method, out bool targetFromGeneric) =>
        TryRawJsonUpcast(
            (method.ReducedFrom ?? method).OriginalDefinition,
            [null, IsJsonTransformation],
            [null, IsSystemType, IsJsonTransformation],
            out targetFromGeneric);

    static bool TrySchemaVersionRawJsonUpcast(IMethodSymbol method, out bool targetFromGeneric) =>
        TryRawJsonUpcast(
            (method.ReducedFrom ?? method).OriginalDefinition,
            [null, IsUnsigned, IsJsonTransformation],
            [null, IsSystemType, IsUnsigned, IsJsonTransformation],
            out targetFromGeneric);

    static bool TryRawJsonUpcast(
        IMethodSymbol candidate,
        Func<ITypeSymbol, bool>?[] genericParameters,
        Func<ITypeSymbol, bool>?[] typeOfParameters,
        out bool targetFromGeneric)
    {
        targetFromGeneric = candidate.TypeParameters.Length == 1;
        return candidate.TypeParameters.Length switch
        {
            1 => HasParameters(candidate, genericParameters),
            0 => HasParameters(candidate, typeOfParameters),
            _ => false
        };
    }

    static bool HasParameters(IMethodSymbol candidate, params Func<ITypeSymbol, bool>?[] expected) =>
        candidate.Parameters.Length == expected.Length &&
        expected.Select((check, index) => check is null || check(candidate.Parameters[index].Type)).All(_ => _);

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

        return HasTypedUpcastConfigurationParameters(candidate);
    }

    static bool HasTypedUpcastConfigurationParameters(IMethodSymbol candidate)
    {
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

        if (IsSyncUpcastDelegate(delegateType, typeParameters))
        {
            return true;
        }

        isAsync = IsAsyncUpcastDelegate(delegateType, typeParameters);
        return isAsync;
    }

    static bool IsSyncUpcastDelegate(INamedTypeSymbol delegateType, IReadOnlyList<ITypeParameterSymbol> typeParameters) =>
        MetadataName(delegateType) == "System.Func`2" &&
        SameType(delegateType.TypeArguments[0], typeParameters[0]) &&
        SameType(delegateType.TypeArguments[1], typeParameters[1]);

    static bool IsAsyncUpcastDelegate(INamedTypeSymbol delegateType, IReadOnlyList<ITypeParameterSymbol> typeParameters) =>
        MetadataName(delegateType) == "System.Func`3" &&
        SameType(delegateType.TypeArguments[0], typeParameters[0]) &&
        MetadataName(delegateType.TypeArguments[1]) == "System.Threading.CancellationToken" &&
        delegateType.TypeArguments[2] is INamedTypeSymbol taskType &&
        MetadataName(taskType) == "System.Threading.Tasks.Task`1" &&
        SameType(taskType.TypeArguments[0], typeParameters[1]);

    static bool SameType(ITypeSymbol left, ITypeSymbol right) => SymbolEqualityComparer.Default.Equals(left, right);

    static UpcasterShape? UpcasterShapeOf(INamedTypeSymbol upcasterType, DotNetProjectCompilation project)
    {
        for (var derived = upcasterType; derived.BaseType is { } current; derived = current)
        {
            var metadataName = DotNetSubjectIds.MetadataName(current.OriginalDefinition);
            var isClr = _clrUpcasterBases.Contains(metadataName);
            var isRaw = _rawUpcasterBases.Contains(metadataName);
            if (!isClr && !isRaw)
            {
                continue;
            }

            if (!HasAuthoredOrMetadataBaseEdge(derived, current, project))
            {
                return null;
            }

            var isAsync = _asyncUpcasterBases.Contains(metadataName);
            if (isClr && ClrShape(current, isAsync) is { } clrShape)
            {
                return clrShape;
            }

            if (isRaw && RawShape(current, isAsync) is { } rawShape)
            {
                return rawShape;
            }
        }

        return null;
    }

    static UpcasterShape? ClrShape(INamedTypeSymbol upcasterBase, bool isAsync) =>
        upcasterBase.TypeArguments is [INamedTypeSymbol oldEvent, INamedTypeSymbol newEvent]
            ? new(oldEvent, newEvent, isAsync, false)
            : null;

    static UpcasterShape? RawShape(INamedTypeSymbol upcasterBase, bool isAsync) =>
        upcasterBase.TypeArguments is [INamedTypeSymbol target]
            ? new(null, target, isAsync, true)
            : null;

    static bool HasEventStoreReceiver(IMethodSymbol method) =>
        method.IsExtensionMethod &&
        method.Parameters.Length > 0 &&
        IsEventStoreOptions(method.Parameters[0].Type);

    static bool IsEventStoreOptions(ITypeSymbol type) =>
        type is INamedTypeSymbol named &&
        (MetadataName(named) == WellKnownTypes.MartenEventStoreOptions ||
         DotNetSymbols.Implements(named, WellKnownTypes.MartenEventStoreOptions));

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
        HashSet<string> admittedMembers) =>
        NamedEnumMember(expression, semanticModel, enumMetadataName, admittedMembers) ??
        ConstantEnumMember(expression, semanticModel, enumMetadataName, admittedMembers);

    static string? NamedEnumMember(
        ExpressionSyntax expression,
        SemanticModel semanticModel,
        string enumMetadataName,
        HashSet<string> admittedMembers) =>
        semanticModel.GetSymbolInfo(expression).Symbol is IFieldSymbol field &&
        DotNetSubjectIds.MetadataName(field.ContainingType) == enumMetadataName &&
        admittedMembers.Contains(field.Name)
            ? field.Name
            : null;

    static string? ConstantEnumMember(
        ExpressionSyntax expression,
        SemanticModel semanticModel,
        string enumMetadataName,
        HashSet<string> admittedMembers)
    {
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

    void DiscoverBinarySerializerConfiguration(InvocationExpressionSyntax invocation, IMethodSymbol method)
    {
        var candidate = (method.ReducedFrom ?? method).OriginalDefinition;
        if (!string.Equals(candidate.Name, "UseBinarySerializer", StringComparison.Ordinal) ||
            candidate.TypeParameters.Length != 1 ||
            !IsEventStoreOptions(candidate.ContainingType))
        {
            return;
        }

        var eventType = NamedType(method.TypeArguments.SingleOrDefault());
        var serializerExpression = invocation.ArgumentList.Arguments.FirstOrDefault()?.Expression;
        var serializerType = NamedType(serializerExpression is null ? null : semanticModel.GetTypeInfo(serializerExpression).Type);
        if (eventType is null || serializerType is null)
        {
            diagnostics.Add(EventConfiguration(
                eventType,
                "Marten has an authored per-event binary serializer declaration whose event or serializer type could not be resolved safely",
                invocation.GetLocation(),
                GenerationDiagnosticOutcome.Unknown));
            return;
        }

        diagnostics.Add(EventConfiguration(
            eventType,
            $"Marten event type '{eventType.Name}' has an authored binary serializer declaration using '{serializerType.Name}'",
            invocation.GetLocation()));
    }

    void DiscoverWireConfiguration(AssignmentExpressionSyntax assignment)
    {
        if (semanticModel.GetSymbolInfo(assignment.Left).Symbol is not IPropertySymbol property ||
            !IsEventStoreOptions(property.ContainingType) ||
            WireSettingFor(property.Name) is not { } setting)
        {
            return;
        }

        var value = EnumMember(assignment.Right, semanticModel, setting.EnumType, setting.AdmittedValues);
        diagnostics.Add(EventConfiguration(
            null,
            WireMessage(setting.Label, value),
            assignment.GetLocation(),
            value is null ? GenerationDiagnosticOutcome.Unknown : GenerationDiagnosticOutcome.Unsupported));
    }

    void DiscoverEventTypeConfiguration(InvocationExpressionSyntax invocation, IMethodSymbol method)
    {
        if (IsDirectMapEventType(method))
        {
            DiscoverDirectEventTypeAlias(invocation, method);
            return;
        }

        if (!IsEventTypeHelper(method))
        {
            return;
        }

        var eventType = NamedType(method.TypeArguments.SingleOrDefault());
        if (eventType is null)
        {
            diagnostics.Add(UnresolvedEventConfiguration(invocation.GetLocation()));
            return;
        }

        if (method.Name == "MapEventTypeWithNameSuffix")
        {
            DiscoverNameSuffix(invocation, method, eventType);
            return;
        }

        DiscoverSchemaVersion(invocation, method, eventType);
    }

    void DiscoverNameSuffix(InvocationExpressionSyntax invocation, IMethodSymbol method, INamedTypeSymbol eventType)
    {
        var suffix = ConstantString(ArgumentForParameter(invocation, method, "suffix")?.Expression, semanticModel);
        if (suffix is null || !TryOptionalBaseAlias(invocation, method, out var baseAlias))
        {
            diagnostics.Add(UnresolvedEventConfiguration(invocation.GetLocation(), eventType));
            return;
        }

        diagnostics.Add(EventConfiguration(eventType, NameSuffixMessage(eventType, suffix, baseAlias), invocation.GetLocation()));
    }

    void DiscoverSchemaVersion(InvocationExpressionSyntax invocation, IMethodSymbol method, INamedTypeSymbol eventType)
    {
        var version = ConstantUnsigned(ArgumentForParameter(invocation, method, "schemaVersion")?.Expression, semanticModel);
        if (version is null || !TryOptionalBaseAlias(invocation, method, out var baseAlias))
        {
            diagnostics.Add(UnresolvedEventConfiguration(invocation.GetLocation(), eventType));
            return;
        }

        diagnostics.Add(EventConfiguration(eventType, SchemaVersionMessage(eventType, version.Value, baseAlias), invocation.GetLocation()));
    }

    void DiscoverDirectEventTypeAlias(InvocationExpressionSyntax invocation, IMethodSymbol method)
    {
        var eventType = method.TypeArguments.Length == 1
            ? NamedType(method.TypeArguments[0])
            : TypeFromTypeOf(ArgumentForParameter(invocation, method, "eventType")?.Expression, semanticModel);
        var alias = ConstantString(ArgumentForParameter(invocation, method, "eventTypeName")?.Expression, semanticModel);
        if (eventType is null || alias is null)
        {
            diagnostics.Add(UnresolvedEventConfiguration(invocation.GetLocation(), eventType));
            return;
        }

        diagnostics.Add(EventConfiguration(
            eventType,
            $"Marten event type '{eventType.Name}' has authored storage alias '{DiagnosticValue(alias)}'",
            invocation.GetLocation()));
    }

    void DiscoverEventNamingStyle(AssignmentExpressionSyntax assignment)
    {
        if (semanticModel.GetSymbolInfo(assignment.Left).Symbol is not IPropertySymbol { Name: "EventNamingStyle" } property ||
            DotNetSubjectIds.MetadataName(property.ContainingType.OriginalDefinition) != WellKnownTypes.MartenEventStoreOptions)
        {
            return;
        }

        var style = EnumMember(assignment.Right, semanticModel, WellKnownTypes.JasperFxEventNamingStyle, _eventNamingStyles);
        diagnostics.Add(style is null
            ? UnresolvedEventConfiguration(assignment.GetLocation())
            : EventConfiguration(
                null,
                $"Marten has an authored global event naming-style declaration '{style}'; runtime precedence and effective aliases were not inferred",
                assignment.GetLocation()));
    }

    void DiscoverEventAliasAttribute(AttributeSyntax attribute)
    {
        if (!IsMartenEventAttribute(attribute) ||
            AuthoredAlias(attribute) is not { Length: > 0 } alias ||
            DeclaredEventType(attribute) is not { } eventType)
        {
            return;
        }

        diagnostics.Add(EventConfiguration(
            eventType,
            $"Marten event type '{eventType.Name}' has authored MartenEvent alias '{DiagnosticValue(alias)}'; the alias only applies when Marten AutoRegister discovers the type",
            attribute.GetLocation()));
    }

    bool IsMartenEventAttribute(AttributeSyntax attribute) =>
        semanticModel.GetSymbolInfo(attribute).Symbol is IMethodSymbol constructor &&
        DotNetSubjectIds.MetadataName(constructor.ContainingType) == WellKnownTypes.MartenEventAttribute;

    string? AuthoredAlias(AttributeSyntax attribute) =>
        attribute.ArgumentList?.Arguments.FirstOrDefault(_ => _.NameEquals?.Name.Identifier.ValueText == "Alias") is { } aliasArgument
            ? ConstantString(aliasArgument.Expression, semanticModel)
            : null;

    INamedTypeSymbol? DeclaredEventType(AttributeSyntax attribute) =>
        attribute.FirstAncestorOrSelf<TypeDeclarationSyntax>() is { } declaration
            ? semanticModel.GetDeclaredSymbol(declaration) as INamedTypeSymbol
            : null;

    void DiscoverUpcastConfiguration(InvocationExpressionSyntax invocation, IMethodSymbol method)
    {
        if (IsDirectUpcast(method))
        {
            DiscoverDirectUpcast(invocation, method);
            return;
        }

        if (IsExtensionUpcast(method))
        {
            DiscoverExtensionUpcast(invocation, method);
        }
    }

    void DiscoverDirectUpcast(InvocationExpressionSyntax invocation, IMethodSymbol method)
    {
        if (IsGenericUpcasterRegistration(method))
        {
            DiscoverGenericUpcasterRegistration(invocation, method);
            return;
        }

        if (IsInlineUpcasterRegistration(method))
        {
            DiscoverInlineUpcasterRegistration(invocation);
            return;
        }

        if (TryDirectRawJsonUpcast(method, out var targetFromGeneric))
        {
            DiscoverDirectRawJsonUpcast(invocation, method, targetFromGeneric);
            return;
        }

        if (TryTypedUpcast(method, out var isAsync))
        {
            DiscoverDirectTypedUpcast(invocation, method, isAsync);
        }
    }

    void DiscoverGenericUpcasterRegistration(InvocationExpressionSyntax invocation, IMethodSymbol method)
    {
        var upcasterType = NamedType(method.TypeArguments[0]);
        if (upcasterType is null || HasOnlyGeneratedSource(upcasterType, project))
        {
            diagnostics.Add(UnresolvedUpcastConfiguration(invocation.GetLocation()));
            return;
        }

        if (UpcasterShapeOf(upcasterType, project) is { } shape)
        {
            diagnostics.Add(UpcastConfiguration(shape.Subject, ClassUpcastMessage(upcasterType, shape), invocation.GetLocation()));
        }
    }

    void DiscoverDirectRawJsonUpcast(InvocationExpressionSyntax invocation, IMethodSymbol method, bool targetFromGeneric)
    {
        var target = RawJsonTarget(invocation, method, targetFromGeneric);
        var rawAlias = ConstantString(ArgumentForParameter(invocation, method, "eventTypeName")?.Expression, semanticModel);
        if (target is null || rawAlias is null)
        {
            diagnostics.Add(UnresolvedUpcastConfiguration(invocation.GetLocation(), target));
            return;
        }

        diagnostics.Add(UpcastConfiguration(
            target,
            $"Marten has an authored raw JSON upcast declaration from unknown source schema alias '{DiagnosticValue(rawAlias)}' to '{target.Name}'",
            invocation.GetLocation()));
    }

    void DiscoverDirectTypedUpcast(InvocationExpressionSyntax invocation, IMethodSymbol method, bool isAsync)
    {
        var oldEvent = NamedType(method.TypeArguments[0]);
        var newEvent = NamedType(method.TypeArguments[1]);
        if (oldEvent is null || newEvent is null || !TryOptionalBaseAlias(invocation, method, out var alias))
        {
            diagnostics.Add(UnresolvedUpcastConfiguration(invocation.GetLocation(), newEvent));
            return;
        }

        diagnostics.Add(UpcastConfiguration(
            newEvent,
            TypedUpcastAliasMessage(oldEvent, newEvent, isAsync, alias),
            invocation.GetLocation()));
    }

    void DiscoverExtensionUpcast(InvocationExpressionSyntax invocation, IMethodSymbol method)
    {
        if (TryConventionRawJsonUpcast(method, out var conventionTargetFromGeneric))
        {
            DiscoverConventionRawJsonUpcast(invocation, method, conventionTargetFromGeneric);
            return;
        }

        if (TrySchemaVersionRawJsonUpcast(method, out var targetFromGeneric))
        {
            DiscoverSchemaVersionRawJsonUpcast(invocation, method, targetFromGeneric);
            return;
        }

        if (TryTypedUpcast(method, out var isAsync))
        {
            DiscoverSchemaVersionTypedUpcast(invocation, method, isAsync);
        }
    }

    void DiscoverConventionRawJsonUpcast(InvocationExpressionSyntax invocation, IMethodSymbol method, bool targetFromGeneric)
    {
        var target = RawJsonTarget(invocation, method, targetFromGeneric);
        if (target is null)
        {
            diagnostics.Add(UnresolvedUpcastConfiguration(invocation.GetLocation()));
            return;
        }

        diagnostics.Add(UpcastConfiguration(
            target,
            $"Marten has an authored raw JSON upcast declaration from an unknown source schema to '{target.Name}' using a convention-derived storage alias that was not inferred",
            invocation.GetLocation()));
    }

    void DiscoverSchemaVersionRawJsonUpcast(InvocationExpressionSyntax invocation, IMethodSymbol method, bool targetFromGeneric)
    {
        var version = ConstantUnsigned(ArgumentForParameter(invocation, method, "schemaVersion")?.Expression, semanticModel);
        var target = RawJsonTarget(invocation, method, targetFromGeneric);
        if (target is null || version is null)
        {
            diagnostics.Add(UnresolvedUpcastConfiguration(invocation.GetLocation(), target));
            return;
        }

        diagnostics.Add(UpcastConfiguration(
            target,
            $"Marten has an authored raw JSON upcast declaration from unknown source schema version '{version}' to '{target.Name}'; its convention-derived effective storage alias was not inferred",
            invocation.GetLocation()));
    }

    void DiscoverSchemaVersionTypedUpcast(InvocationExpressionSyntax invocation, IMethodSymbol method, bool isAsync)
    {
        var version = ConstantUnsigned(ArgumentForParameter(invocation, method, "schemaVersion")?.Expression, semanticModel);
        var oldEvent = NamedType(method.TypeArguments[0]);
        var newEvent = NamedType(method.TypeArguments[1]);
        if (oldEvent is null || newEvent is null || version is null)
        {
            diagnostics.Add(UnresolvedUpcastConfiguration(invocation.GetLocation(), newEvent));
            return;
        }

        diagnostics.Add(UpcastConfiguration(
            newEvent,
            $"Marten has an authored {ShapeLabel(isAsync)} typed upcast declaration '{oldEvent.Name} -> {newEvent.Name}' for source schema version '{version}'; its convention-derived effective storage alias was not inferred",
            invocation.GetLocation()));
    }

    void DiscoverInlineUpcasterRegistration(InvocationExpressionSyntax invocation)
    {
        var registrations = new List<InlineRegistration>();
        foreach (var argument in invocation.ArgumentList.Arguments)
        {
            if (InlineRegistrationOf(argument) is not { } registration)
            {
                diagnostics.Add(UnresolvedUpcastConfiguration(invocation.GetLocation()));
                return;
            }

            registrations.Add(registration);
        }

        diagnostics.AddRange(registrations.Select(_ => UpcastConfiguration(
            _.Shape.Subject,
            ClassUpcastMessage(_.Type, _.Shape),
            _.Creation.GetLocation())));
    }

    InlineRegistration? InlineRegistrationOf(ArgumentSyntax argument)
    {
        if (argument.Expression is not ObjectCreationExpressionSyntax creation ||
            semanticModel.GetTypeInfo(creation).Type is not INamedTypeSymbol upcasterType ||
            HasOnlyGeneratedSource(upcasterType, project))
        {
            return null;
        }

        return UpcasterShapeOf(upcasterType, project) is { } shape ? new(creation, upcasterType, shape) : null;
    }

    INamedTypeSymbol? RawJsonTarget(InvocationExpressionSyntax invocation, IMethodSymbol method, bool targetFromGeneric) =>
        targetFromGeneric
            ? NamedType(method.TypeArguments[0])
            : TypeFromTypeOf(ArgumentForParameter(invocation, method, "eventType")?.Expression, semanticModel);

    bool TryOptionalBaseAlias(InvocationExpressionSyntax invocation, IMethodSymbol method, out string? alias)
    {
        var argument = ArgumentForParameter(invocation, method, "eventTypeName");
        alias = argument is null ? null : ConstantString(argument.Expression, semanticModel);
        return argument is null || alias is not null;
    }

    GenerationDiagnostic EventConfiguration(
        INamedTypeSymbol? eventType,
        string message,
        Location location,
        GenerationDiagnosticOutcome outcome = GenerationDiagnosticOutcome.Unsupported) =>
        Configuration(MartenDiagnosticCodes.EventTypeConfigurationOmitted, eventType, message + EventConfigurationNote, location, outcome);

    GenerationDiagnostic UnresolvedEventConfiguration(Location location, INamedTypeSymbol? eventType = null) => EventConfiguration(
        eventType,
        "Marten has an authored event alias, schema-version, or naming-style declaration with a computed or otherwise non-constant value that could not be resolved safely; no storage alias, suffix, version, naming style, or effective value was guessed",
        location,
        GenerationDiagnosticOutcome.Unknown);

    GenerationDiagnostic UpcastConfiguration(
        INamedTypeSymbol? target,
        string message,
        Location location,
        GenerationDiagnosticOutcome outcome = GenerationDiagnosticOutcome.Unsupported) =>
        Configuration(MartenDiagnosticCodes.EventUpcastConfigurationOmitted, target, message + UpcastConfigurationNote, location, outcome);

    GenerationDiagnostic UnresolvedUpcastConfiguration(Location location, INamedTypeSymbol? target = null) => UpcastConfiguration(
        target,
        "Marten has an authored upcast declaration with a computed, indirect, mixed inline collection, or otherwise unresolved type, alias, or schema version; no source type, target type, alias, version, shape, ordering, or effective value was guessed",
        location,
        GenerationDiagnosticOutcome.Unknown);

    GenerationDiagnostic Configuration(
        string code,
        INamedTypeSymbol? subjectType,
        string message,
        Location location,
        GenerationDiagnosticOutcome outcome) => new()
        {
            Code = code,
            Severity = GenerationDiagnosticSeverity.Warning,
            Outcome = outcome,
            Message = message,
            Source = CritterStackSource.RangeForProject(location, project),
            Subject = subjectType is null ? ProjectSubject(project) : subjects.SubjectForType(project, subjectType)
        };

    sealed record WireSetting(string EnumType, HashSet<string> AdmittedValues, string Label);

    sealed record InlineRegistration(
        ObjectCreationExpressionSyntax Creation,
        INamedTypeSymbol Type,
        UpcasterShape Shape);

    sealed record UpcasterShape(
        INamedTypeSymbol? Source,
        INamedTypeSymbol Target,
        bool IsAsync,
        bool IsRaw)
    {
        public INamedTypeSymbol Subject => Target;
    }
}
