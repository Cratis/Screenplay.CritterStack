// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Cratis.Screenplay.Generation;
using Cratis.Screenplay.Generation.DotNet;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Cratis.CritterStack.Screenplay.Marten;

sealed partial class MartenEventSchemaConfigurationDiscovery
{
    const string EventConfigurationNote = ". This authored declaration is retained as diagnostic evidence only; it does not rename, version, originate, or duplicate a Screenplay Event artifact, and runtime execution or precedence is not asserted";

    static readonly HashSet<string> _eventNamingStyles = ["ClassicTypeName", "SmarterTypeName", "FullTypeName"];

    static string NameSuffixMessage(INamedTypeSymbol eventType, string suffix, string? baseAlias) => baseAlias is null
        ? $"Marten event type '{eventType.Name}' has an authored name-suffix declaration with suffix '{DiagnosticValue(suffix)}'; its convention-derived effective storage alias was not inferred"
        : $"Marten event type '{eventType.Name}' has an authored name-suffix declaration with base alias '{DiagnosticValue(baseAlias)}' and suffix '{DiagnosticValue(suffix)}', deriving storage alias '{DiagnosticValue(baseAlias)}_{DiagnosticValue(suffix)}'";

    static string SchemaVersionMessage(INamedTypeSymbol eventType, uint version, string? baseAlias) => baseAlias is null
        ? $"Marten event type '{eventType.Name}' has an authored schema-version declaration for version '{version}'; its convention-derived effective storage alias was not inferred"
        : $"Marten event type '{eventType.Name}' has an authored schema-version declaration with base alias '{DiagnosticValue(baseAlias)}' and version '{version}', deriving storage alias '{DiagnosticValue(baseAlias)}_v{version}'";

    static bool IsDirectMapEventType(IMethodSymbol method)
    {
        var candidate = method.OriginalDefinition;
        if (!IsEventStoreOptionsMethod(candidate, "MapEventType"))
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
        return IsEventStoreOptionsExtension(candidate) &&
               HasSingleTypeArgument(candidate, method) &&
               HasEventTypeHelperParameters(candidate);
    }

    static bool HasSingleTypeArgument(IMethodSymbol candidate, IMethodSymbol method) =>
        candidate.TypeParameters.Length == 1 && method.TypeArguments.Length == 1;

    static bool HasEventTypeHelperParameters(IMethodSymbol candidate) => candidate.Name switch
    {
        "MapEventTypeWithNameSuffix" =>
            HasParameters(candidate, null, IsString) || HasParameters(candidate, null, IsString, IsString),
        "MapEventTypeWithSchemaVersion" =>
            HasParameters(candidate, null, IsUnsigned) || HasParameters(candidate, null, IsString, IsUnsigned),
        _ => false
    };

    static bool IsEventNamingStyleProperty(IPropertySymbol property) =>
        property.Name == "EventNamingStyle" &&
        DotNetSubjectIds.MetadataName(property.ContainingType.OriginalDefinition) == WellKnownTypes.MartenEventStoreOptions;

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
        var hasBaseAlias = TryOptionalBaseAlias(invocation, method, out var baseAlias);
        if (suffix is null || !hasBaseAlias)
        {
            diagnostics.Add(UnresolvedEventConfiguration(invocation.GetLocation(), eventType));
            return;
        }

        diagnostics.Add(EventConfiguration(eventType, NameSuffixMessage(eventType, suffix, baseAlias), invocation.GetLocation()));
    }

    void DiscoverSchemaVersion(InvocationExpressionSyntax invocation, IMethodSymbol method, INamedTypeSymbol eventType)
    {
        var version = ConstantUnsigned(ArgumentForParameter(invocation, method, "schemaVersion")?.Expression, semanticModel);
        var hasBaseAlias = TryOptionalBaseAlias(invocation, method, out var baseAlias);
        if (version is null || !hasBaseAlias)
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
        if (semanticModel.GetSymbolInfo(assignment.Left).Symbol is not IPropertySymbol property ||
            !IsEventNamingStyleProperty(property))
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

    GenerationDiagnostic EventConfiguration(
        INamedTypeSymbol? eventType,
        string message,
        Location location,
        GenerationDiagnosticOutcome outcome = GenerationDiagnosticOutcome.Unsupported) =>
        Configuration(
            MartenDiagnosticCodes.EventTypeConfigurationOmitted,
            new(eventType, message + EventConfigurationNote, location, outcome));

    GenerationDiagnostic UnresolvedEventConfiguration(Location location, INamedTypeSymbol? eventType = null) => EventConfiguration(
        eventType,
        "Marten has an authored event alias, schema-version, or naming-style declaration with a computed or otherwise non-constant value that could not be resolved safely; no storage alias, suffix, version, naming style, or effective value was guessed",
        location,
        GenerationDiagnosticOutcome.Unknown);
}
