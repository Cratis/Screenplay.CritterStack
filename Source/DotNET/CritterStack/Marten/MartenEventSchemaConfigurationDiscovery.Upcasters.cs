// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Cratis.Screenplay.Generation;
using Cratis.Screenplay.Generation.DotNet;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Cratis.CritterStack.Screenplay.Marten;

sealed partial class MartenEventSchemaConfigurationDiscovery
{
    const string UpcastConfigurationNote = ". This authored declaration is retained as diagnostic evidence only; it does not originate Event or Upcast artifacts or infer behavioral relationships, and runtime execution, reachability, ordering, or precedence is not asserted";

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

    static string TypedUpcastAliasMessage(INamedTypeSymbol oldEvent, INamedTypeSymbol newEvent, bool isAsync, string? alias) => alias is null
        ? $"Marten has an authored {ShapeLabel(isAsync)} typed upcast declaration '{oldEvent.Name} -> {newEvent.Name}' using a convention-derived storage alias that was not inferred"
        : $"Marten has an authored {ShapeLabel(isAsync)} typed upcast declaration '{oldEvent.Name} -> {newEvent.Name}' for storage alias '{DiagnosticValue(alias)}'";

    static string ClassUpcastMessage(INamedTypeSymbol upcasterType, UpcasterShape shape) => shape.IsRaw
        ? $"Marten has an authored {ShapeLabel(shape.IsAsync)} class-upcaster registration '{upcasterType.Name}' from unknown JSON source schema to '{shape.Target.Name}'; EventTypeName overrides and effective aliases were not inspected"
        : $"Marten has an authored {ShapeLabel(shape.IsAsync)} class-upcaster registration '{upcasterType.Name}' for '{shape.Source!.Name} -> {shape.Target.Name}'; EventTypeName overrides and effective aliases were not inspected";

    static bool IsDirectUpcast(IMethodSymbol method) =>
        IsEventStoreOptionsMethod(method.OriginalDefinition, "Upcast");

    static bool IsExtensionUpcast(IMethodSymbol method)
    {
        var candidate = (method.ReducedFrom ?? method).OriginalDefinition;
        return string.Equals(candidate.Name, "Upcast", StringComparison.Ordinal) && IsEventStoreOptionsExtension(candidate);
    }

    static bool IsGenericUpcasterRegistration(IMethodSymbol method)
    {
        var candidate = method.OriginalDefinition;
        return candidate.Parameters.Length == 0 &&
               candidate.TypeParameters is [{ HasConstructorConstraint: true } upcaster] &&
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
            if (!IsUpcasterBase(current))
            {
                continue;
            }

            if (!HasAuthoredOrMetadataBaseEdge(derived, current, project))
            {
                return null;
            }

            if (ShapeOfUpcasterBase(current) is { } shape)
            {
                return shape;
            }
        }

        return null;
    }

    static bool IsUpcasterBase(INamedTypeSymbol type)
    {
        var metadataName = DotNetSubjectIds.MetadataName(type.OriginalDefinition);
        return _clrUpcasterBases.Contains(metadataName) || _rawUpcasterBases.Contains(metadataName);
    }

    static UpcasterShape? ShapeOfUpcasterBase(INamedTypeSymbol upcasterBase)
    {
        var metadataName = DotNetSubjectIds.MetadataName(upcasterBase.OriginalDefinition);
        var isAsync = _asyncUpcasterBases.Contains(metadataName);
        var clrShape = _clrUpcasterBases.Contains(metadataName) ? ClrShape(upcasterBase, isAsync) : null;
        return clrShape ?? (_rawUpcasterBases.Contains(metadataName) ? RawShape(upcasterBase, isAsync) : null);
    }

    static UpcasterShape? ClrShape(INamedTypeSymbol upcasterBase, bool isAsync) =>
        upcasterBase.TypeArguments is [INamedTypeSymbol oldEvent, INamedTypeSymbol newEvent]
            ? new(oldEvent, newEvent, isAsync, false)
            : null;

    static UpcasterShape? RawShape(INamedTypeSymbol upcasterBase, bool isAsync) =>
        upcasterBase.TypeArguments is [INamedTypeSymbol target]
            ? new(null, target, isAsync, true)
            : null;

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
        var hasAlias = TryOptionalBaseAlias(invocation, method, out var alias);
        if (oldEvent is null || newEvent is null || !hasAlias)
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

        var version = ConstantUnsigned(ArgumentForParameter(invocation, method, "schemaVersion")?.Expression, semanticModel);
        if (TrySchemaVersionRawJsonUpcast(method, out var targetFromGeneric))
        {
            DiscoverSchemaVersionRawJsonUpcast(invocation, method, targetFromGeneric, version);
            return;
        }

        if (TryTypedUpcast(method, out var isAsync))
        {
            DiscoverSchemaVersionTypedUpcast(invocation, method, isAsync, version);
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

    void DiscoverSchemaVersionRawJsonUpcast(InvocationExpressionSyntax invocation, IMethodSymbol method, bool targetFromGeneric, uint? version)
    {
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

    void DiscoverSchemaVersionTypedUpcast(InvocationExpressionSyntax invocation, IMethodSymbol method, bool isAsync, uint? version)
    {
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

    GenerationDiagnostic UpcastConfiguration(
        INamedTypeSymbol? target,
        string message,
        Location location,
        GenerationDiagnosticOutcome outcome = GenerationDiagnosticOutcome.Unsupported) =>
        Configuration(
            MartenDiagnosticCodes.EventUpcastConfigurationOmitted,
            new(target, message + UpcastConfigurationNote, location, outcome));

    GenerationDiagnostic UnresolvedUpcastConfiguration(Location location, INamedTypeSymbol? target = null) => UpcastConfiguration(
        target,
        "Marten has an authored upcast declaration with a computed, indirect, mixed inline collection, or otherwise unresolved type, alias, or schema version; no source type, target type, alias, version, shape, ordering, or effective value was guessed",
        location,
        GenerationDiagnosticOutcome.Unknown);

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
