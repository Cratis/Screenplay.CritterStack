// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Cratis.Screenplay.Generation;
using Cratis.Screenplay.Generation.DotNet;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Operations;

namespace Cratis.CritterStack.Screenplay.Wolverine;

enum WolverineStateBindingKind
{
    DirectEventStream,
    LoadedEventStream
}

sealed record WolverineStateBindingEvidence<T>(T Value, SourceRange? Source);

sealed record WolverineStateBinding(
    string HandlerKey,
    IParameterSymbol Parameter,
    INamedTypeSymbol ModelType,
    WolverineStateBindingKind Kind,
    WolverineStateBindingEvidence<string?> Identity,
    WolverineStateBindingEvidence<string?> Version,
    WolverineStateBindingEvidence<string> LoadStyle,
    WolverineStateBindingEvidence<bool> Consistency,
    ISymbol? IdentityMember,
    ISymbol? VersionMember,
    bool HasAmbiguousConventionalVersion,
    SourceRange? Source)
{
    public bool LoadsModel => Kind == WolverineStateBindingKind.LoadedEventStream;

    public string Discriminator => $"stream:{HandlerKey}:{Parameter.Ordinal}:{Parameter.Name}";
}

sealed record WolverineEventStreamAppend(
    WolverineStateBinding Binding,
    IReadOnlyList<INamedTypeSymbol> EventTypes,
    SourceRange? Source);

sealed record WolverineUnresolvedEventStreamAppend(string Reason, SourceRange? Source);

sealed record WolverineEventStreamAppendDiscovery(
    IReadOnlyList<WolverineEventStreamAppend> Appends,
    IReadOnlyList<WolverineUnresolvedEventStreamAppend> Unresolved,
    bool HasDirectWrite);

static class WolverineEventStreams
{
    static readonly HashSet<string> _eventStreamTypes =
    [
        WellKnownTypes.JasperFxEventStream,
        WellKnownTypes.MartenLegacyEventStream
    ];

    public static IReadOnlyList<WolverineStateBinding> Bindings(
        IMethodSymbol method,
        INamedTypeSymbol? requestType,
        DotNetProjectCompilation project)
    {
        var bindings = new List<WolverineStateBinding>();
        var loadedBindingCount = method.Parameters.Count(parameter =>
            EventStreamModels(parameter.Type, project).Count > 0 &&
            WriteModelAttribute(parameter, project) is not null);
        var handlerKey = $"{project.SubjectForType(method.ContainingType).Value}#{method.MetadataName}";
        foreach (var parameter in method.Parameters)
        {
            var modelTypes = EventStreamModels(parameter.Type, project);
            if (modelTypes.Count == 0)
            {
                continue;
            }

            var attribute = WriteModelAttribute(parameter, project);
            var source = SourceOf(attribute, parameter, project);
            var identityName = attribute?.ConstructorArguments.FirstOrDefault().Value as string;
            var versionName = NamedString(attribute, "VersionSource");
            var loadStyle = NamedEnum(attribute, "LoadStyle") ?? "Optimistic";
            var consistency = NamedBoolean(attribute, "AlwaysEnforceConsistency") ?? false;
            foreach (var modelType in modelTypes)
            {
                var identityMember = attribute is null
                    ? null
                    : IdentityMember(method, requestType, modelType, identityName, project);
                var versionMember = attribute is null
                    ? null
                    : VersionMember(method, requestType, versionName, loadedBindingCount, project);
                var hasAmbiguousConventionalVersion = attribute is not null &&
                    string.IsNullOrWhiteSpace(versionName) &&
                    loadedBindingCount > 1 &&
                    VersionMember(method, requestType, "Version", loadedBindingCount: 1, project: project) is not null;
                bindings.Add(new(
                    handlerKey,
                    parameter,
                    modelType,
                    attribute is null
                        ? WolverineStateBindingKind.DirectEventStream
                        : WolverineStateBindingKind.LoadedEventStream,
                    new(identityName ?? identityMember?.Name, EvidenceSource(identityMember, source, project)),
                    new(versionName ?? versionMember?.Name, EvidenceSource(versionMember, source, project)),
                    new(loadStyle, source),
                    new(consistency, source),
                    identityMember,
                    versionMember,
                    hasAmbiguousConventionalVersion,
                    source));
            }
        }

        return bindings;
    }

    public static WolverineEventStreamAppendDiscovery Appends(
        IMethodSymbol method,
        DotNetProjectCompilation project,
        IReadOnlyList<WolverineStateBinding> bindings)
    {
        var appends = new List<WolverineEventStreamAppend>();
        var unresolved = new List<WolverineUnresolvedEventStreamAppend>();
        var hasDirectWrite = false;

        foreach (var (declaration, semanticModel) in WolverineMethodSyntax.Declarations(method, project))
        {
            foreach (var invocationSyntax in declaration.DescendantNodes().OfType<InvocationExpressionSyntax>())
            {
                if (!IsInDirectHandlerBody(invocationSyntax, declaration) ||
                    semanticModel.GetOperation(invocationSyntax) is not IInvocationOperation invocation ||
                    !TryGetAppendModel(invocation, project, out var invokedModel))
                {
                    continue;
                }

                hasDirectWrite = true;
                var source = DotNetSource.Range(invocationSyntax.GetLocation(), project.SourceRoot);
                if (ReceiverParameter(invocation.Instance) is not { } receiver ||
                    bindings.FirstOrDefault(binding =>
                        SymbolEqualityComparer.Default.Equals(binding.Parameter, receiver) &&
                        SymbolEqualityComparer.Default.Equals(binding.ModelType, invokedModel)) is not { } binding)
                {
                    unresolved.Add(new("the receiver is not rooted directly in an IEventStream<T> handler parameter", source));
                    continue;
                }

                if (!TryGetPayloads(invocation, out var eventTypes))
                {
                    unresolved.Add(new("the appended payload is not a direct object creation, params value, array, collection expression, or direct collection initializer", source));
                    continue;
                }

                appends.Add(new(binding, eventTypes, source));
            }
        }

        return new(appends, unresolved, hasDirectWrite);
    }

    public static bool IsEventStream(ITypeSymbol type) => EventStreamInterfaces(type).Count > 0;

    public static bool IsExactAppend(IInvocationOperation invocation, DotNetProjectCompilation project) =>
        TryGetAppendModel(invocation, project, out _);

    static bool TryGetAppendModel(
        IInvocationOperation invocation,
        DotNetProjectCompilation project,
        out INamedTypeSymbol modelType)
    {
        modelType = null!;
        var method = invocation.TargetMethod;
        var streamMetadataName = DotNetSubjectIds.MetadataName(method.ContainingType.OriginalDefinition);
        if (method.ReducedFrom is not null ||
            !_eventStreamTypes.Contains(streamMetadataName) ||
            project.Compilation.GetTypeByMetadataName(streamMetadataName) is not { } streamDefinition ||
            !IsAuthoredOrMetadataSymbol(streamDefinition, project))
        {
            return false;
        }

        if (!SymbolEqualityComparer.Default.Equals(method.ContainingType.OriginalDefinition, streamDefinition) ||
            method.ContainingType.TypeArguments.SingleOrDefault() is not INamedTypeSymbol model ||
            !streamDefinition.GetMembers(method.Name).OfType<IMethodSymbol>().Any(candidate =>
                SymbolEqualityComparer.Default.Equals(candidate.OriginalDefinition, method.OriginalDefinition)) ||
            method.Parameters.Length != 1)
        {
            return false;
        }

        var isExactAppend = method.Name switch
        {
            "AppendOne" => method.Parameters[0].Type.SpecialType == SpecialType.System_Object,
            "AppendMany" => IsObjectArray(method.Parameters[0].Type) || IsObjectEnumerable(method.Parameters[0].Type),
            _ => false
        };
        if (isExactAppend)
        {
            modelType = model;
        }

        return isExactAppend;
    }

    static IReadOnlyList<INamedTypeSymbol> EventStreamInterfaces(ITypeSymbol type)
    {
        if (type is not INamedTypeSymbol named)
        {
            return [];
        }

        return
        [
            .. named.AllInterfaces
                .Concat([named])
                .Where(candidate =>
                    candidate.IsGenericType &&
                    _eventStreamTypes.Contains(DotNetSubjectIds.MetadataName(candidate.OriginalDefinition)))
                .GroupBy(_ => _.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat), StringComparer.Ordinal)
                .Select(_ => _.First())
        ];
    }

    static IReadOnlyList<INamedTypeSymbol> EventStreamModels(
        ITypeSymbol parameterType,
        DotNetProjectCompilation project) =>
    [
        .. EventStreamInterfaces(parameterType)
            .Where(stream => HasAuthoredOrMetadataImplementation(parameterType, stream, project))
            .Select(_ => _.TypeArguments.Single())
            .OfType<INamedTypeSymbol>()
    ];

    static bool HasAuthoredOrMetadataImplementation(
        ITypeSymbol parameterType,
        INamedTypeSymbol streamType,
        DotNetProjectCompilation project)
    {
        if (!CanonicalStreamIsAuthoredOrMetadata(streamType, project) || parameterType is not INamedTypeSymbol named)
        {
            return false;
        }

        if (SymbolEqualityComparer.Default.Equals(named.OriginalDefinition, streamType.OriginalDefinition) ||
            named.DeclaringSyntaxReferences.Length == 0)
        {
            return true;
        }

        foreach (var syntaxReference in named.DeclaringSyntaxReferences)
        {
            if (syntaxReference.GetSyntax() is not TypeDeclarationSyntax { BaseList: not null } declaration ||
                !project.AuthoredSyntaxTrees.Contains(declaration.SyntaxTree) ||
                DotNetGeneratedSource.IsGenerated(declaration.SyntaxTree))
            {
                continue;
            }

            var semanticModel = project.Compilation.GetSemanticModel(declaration.SyntaxTree);
            if (declaration.BaseList.Types.Any(baseType =>
                semanticModel.GetTypeInfo(baseType.Type).Type is INamedTypeSymbol candidate &&
                EventStreamInterfaces(candidate).Any(@interface => SymbolEqualityComparer.Default.Equals(@interface, streamType))))
            {
                return true;
            }
        }

        return false;
    }

    static bool CanonicalStreamIsAuthoredOrMetadata(
        INamedTypeSymbol streamType,
        DotNetProjectCompilation project) => streamType.OriginalDefinition.Locations.All(location =>
        !location.IsInSource ||
        (location.SourceTree is not null &&
         project.AuthoredSyntaxTrees.Contains(location.SourceTree) &&
         !DotNetGeneratedSource.IsGenerated(location.SourceTree)));

    static AttributeData? WriteModelAttribute(IParameterSymbol parameter, DotNetProjectCompilation project) =>
        parameter.GetAttributes().FirstOrDefault(attribute =>
            attribute.AttributeClass is not null &&
            IsAuthoredWriteModelAttribute(attribute.AttributeClass, project) &&
            attribute.ApplicationSyntaxReference?.SyntaxTree is { } tree &&
            project.AuthoredSyntaxTrees.Contains(tree) &&
            !DotNetGeneratedSource.IsGenerated(tree));

    static bool IsAuthoredWriteModelAttribute(
        INamedTypeSymbol attributeType,
        DotNetProjectCompilation project)
    {
        var admittedBases = new[]
        {
            WellKnownTypes.WolverineWriteModelAttribute,
            WellKnownTypes.WolverineLegacyWriteAggregateAttribute,
            WellKnownTypes.WolverineHttpAggregateAttribute
        };
        if (!admittedBases.Any(metadataName =>
                project.Compilation.GetTypeByMetadataName(metadataName) is { } admitted &&
                IsAuthoredOrMetadataSymbol(admitted, project) &&
                DotNetSymbols.IsOrInheritsFrom(attributeType, metadataName)))
        {
            return false;
        }

        if (admittedBases.Contains(DotNetSubjectIds.MetadataName(attributeType.OriginalDefinition), StringComparer.Ordinal) ||
            attributeType.DeclaringSyntaxReferences.Length == 0)
        {
            return true;
        }

        foreach (var syntaxReference in attributeType.DeclaringSyntaxReferences)
        {
            if (syntaxReference.GetSyntax() is not TypeDeclarationSyntax { BaseList: not null } declaration ||
                !project.AuthoredSyntaxTrees.Contains(declaration.SyntaxTree) ||
                DotNetGeneratedSource.IsGenerated(declaration.SyntaxTree))
            {
                continue;
            }

            var semanticModel = project.Compilation.GetSemanticModel(declaration.SyntaxTree);
            if (declaration.BaseList.Types.Any(baseType =>
                semanticModel.GetTypeInfo(baseType.Type).Type is INamedTypeSymbol candidate &&
                admittedBases.Any(metadataName => DotNetSymbols.IsOrInheritsFrom(candidate, metadataName))))
            {
                return true;
            }
        }

        return false;
    }

    static ISymbol? IdentityMember(
        IMethodSymbol method,
        INamedTypeSymbol? requestType,
        INamedTypeSymbol modelType,
        string? explicitName,
        DotNetProjectCompilation project)
    {
        var members = SourceMembers(method, requestType, project);
        if (!string.IsNullOrWhiteSpace(explicitName))
        {
            return members.FirstOrDefault(_ => string.Equals(_.Name, explicitName, StringComparison.OrdinalIgnoreCase));
        }

        var attributed = members.FirstOrDefault(_ => _.GetAttributes().Any(attribute =>
            attribute.AttributeClass is { } attributeType &&
            DotNetSubjectIds.MetadataName(attributeType.OriginalDefinition) == WellKnownTypes.JasperFxIdentityAttribute));
        return attributed ??
               members.FirstOrDefault(_ => string.Equals(_.Name, $"{modelType.Name}Id", StringComparison.OrdinalIgnoreCase)) ??
               members.FirstOrDefault(_ => string.Equals(_.Name, "Id", StringComparison.OrdinalIgnoreCase));
    }

    static ISymbol? VersionMember(
        IMethodSymbol method,
        INamedTypeSymbol? requestType,
        string? explicitName,
        int loadedBindingCount,
        DotNetProjectCompilation project)
    {
        if (loadedBindingCount > 1 && string.IsNullOrWhiteSpace(explicitName))
        {
            return null;
        }

        var name = string.IsNullOrWhiteSpace(explicitName) ? "Version" : explicitName;
        return SourceMembers(method, requestType, project).FirstOrDefault(member =>
            string.Equals(member.Name, name, StringComparison.OrdinalIgnoreCase) &&
            TypeOf(member)?.SpecialType is SpecialType.System_Int32 or SpecialType.System_Int64 or SpecialType.System_UInt32);
    }

    static IReadOnlyList<ISymbol> SourceMembers(
        IMethodSymbol method,
        INamedTypeSymbol? requestType,
        DotNetProjectCompilation project) => requestType is null
        ? [.. method.Parameters.Where(parameter =>
            !IsEventStream(parameter.Type) &&
            IsAuthoredOrMetadataSymbol(parameter, project))]
        : [.. requestType.GetMembers().OfType<IPropertySymbol>().Where(property => IsAuthoredOrMetadataSymbol(property, project))];

    static ITypeSymbol? TypeOf(ISymbol symbol) => symbol switch
    {
        IPropertySymbol property => property.Type,
        IParameterSymbol parameter => parameter.Type,
        _ => null
    };

    static bool IsAuthoredOrMetadataSymbol(
        ISymbol symbol,
        DotNetProjectCompilation project) => symbol.Locations.All(location =>
        !location.IsInSource ||
        (location.SourceTree is not null &&
         project.AuthoredSyntaxTrees.Contains(location.SourceTree) &&
         !DotNetGeneratedSource.IsGenerated(location.SourceTree)));

    static SourceRange? EvidenceSource(
        ISymbol? symbol,
        SourceRange? fallback,
        DotNetProjectCompilation project)
    {
        var location = symbol?.Locations.FirstOrDefault(location =>
            location.IsInSource &&
            location.SourceTree is not null &&
            project.AuthoredSyntaxTrees.Contains(location.SourceTree) &&
            !DotNetGeneratedSource.IsGenerated(location.SourceTree));
        return location is null ? fallback : DotNetSource.Range(location, project.SourceRoot);
    }

    static SourceRange? SourceOf(
        AttributeData? attribute,
        IParameterSymbol parameter,
        DotNetProjectCompilation project)
    {
        if (attribute?.ApplicationSyntaxReference?.GetSyntax() is { } syntax &&
            project.AuthoredSyntaxTrees.Contains(syntax.SyntaxTree) &&
            !DotNetGeneratedSource.IsGenerated(syntax.SyntaxTree))
        {
            return DotNetSource.Range(syntax.GetLocation(), project.SourceRoot);
        }

        var location = parameter.Locations.FirstOrDefault(candidate =>
            candidate.IsInSource &&
            candidate.SourceTree is not null &&
            project.AuthoredSyntaxTrees.Contains(candidate.SourceTree) &&
            !DotNetGeneratedSource.IsGenerated(candidate.SourceTree));
        return location is null ? null : DotNetSource.Range(location, project.SourceRoot);
    }

    static string? NamedString(AttributeData? attribute, string name) =>
        attribute?.NamedArguments.FirstOrDefault(_ => _.Key == name).Value.Value as string;

    static bool? NamedBoolean(AttributeData? attribute, string name)
    {
        var value = attribute?.NamedArguments.FirstOrDefault(_ => _.Key == name).Value;
        return value?.Value as bool?;
    }

    static string? NamedEnum(AttributeData? attribute, string name)
    {
        var constant = attribute?.NamedArguments.FirstOrDefault(_ => _.Key == name).Value;
        if (constant is not { Kind: TypedConstantKind.Enum, Type: INamedTypeSymbol enumType, Value: not null })
        {
            return null;
        }

        return enumType.GetMembers().OfType<IFieldSymbol>()
            .FirstOrDefault(_ => _.HasConstantValue && Equals(_.ConstantValue, constant.Value))?.Name;
    }

    static IParameterSymbol? ReceiverParameter(IOperation? receiver)
    {
        var current = receiver;
        while (current is not null)
        {
            switch (current)
            {
                case IConversionOperation conversion when IsSafeReceiverConversion(conversion):
                    current = conversion.Operand;
                    continue;
                case IParenthesizedOperation parenthesized:
                    current = parenthesized.Operand;
                    continue;
                case IParameterReferenceOperation parameter:
                    return parameter.Parameter;
                default:
                    return null;
            }
        }

        return null;
    }

    static bool TryGetPayloads(
        IInvocationOperation invocation,
        out IReadOnlyList<INamedTypeSymbol> eventTypes)
    {
        var payloads = new List<INamedTypeSymbol>();
        var appendMany = invocation.TargetMethod.Name == "AppendMany";
        var arguments = invocation.Arguments.Where(_ => _.Parameter?.Ordinal == 0).ToArray();
        if (arguments.Length == 0)
        {
            eventTypes = [];
            return appendMany;
        }

        foreach (var argument in arguments)
        {
            if (!TryGetPayloads(argument.Value, payloads, allowEmpty: appendMany))
            {
                eventTypes = [];
                return false;
            }
        }

        eventTypes = payloads;
        return appendMany || payloads.Count > 0;
    }

    static bool TryGetPayloads(
        IOperation operation,
        List<INamedTypeSymbol> eventTypes,
        bool allowEmpty = false,
        bool allowContainer = true)
    {
        operation = Unwrap(operation);
        if (operation is IObjectCreationOperation objectCreation &&
            !IsObjectCollection(objectCreation.Type) &&
            IsEventPayloadType(objectCreation.Type))
        {
            eventTypes.Add((INamedTypeSymbol)objectCreation.Type!);
            return true;
        }

        switch (operation)
        {
            case IArrayCreationOperation { Initializer: not null } arrayCreation when allowContainer:
                return TryGetAllPayloads(arrayCreation.Initializer.ElementValues, eventTypes, allowEmpty);
            case IArrayInitializerOperation arrayInitializer when allowContainer:
                return TryGetAllPayloads(arrayInitializer.ElementValues, eventTypes, allowEmpty);
            case ICollectionExpressionOperation collectionExpression when allowContainer:
                if (collectionExpression.Elements.Any(_ => _ is ISpreadOperation))
                {
                    return false;
                }

                return TryGetAllPayloads(collectionExpression.Elements, eventTypes, allowEmpty);
            case IObjectCreationOperation { Initializer: not null } collectionCreation when
                allowContainer && IsSupportedCollectionInitializer(collectionCreation):
                return TryGetCollectionInitializerPayloads(collectionCreation.Initializer, eventTypes, allowEmpty);
            default:
                return false;
        }
    }

    static bool TryGetCollectionInitializerPayloads(
        IObjectOrCollectionInitializerOperation initializer,
        List<INamedTypeSymbol> eventTypes,
        bool allowEmpty)
    {
        if (initializer.Initializers.Length == 0)
        {
            return allowEmpty;
        }

        foreach (var operation in initializer.Initializers)
        {
            if (operation is not IInvocationOperation invocation ||
                invocation.TargetMethod.Name != "Add" ||
                !invocation.TargetMethod.ContainingNamespace.ToDisplayString().StartsWith("System.Collections", StringComparison.Ordinal) ||
                invocation.Arguments.Length == 0)
            {
                return false;
            }

            foreach (var argument in invocation.Arguments)
            {
                if (!TryGetPayloads(argument.Value, eventTypes, allowContainer: false))
                {
                    return false;
                }
            }
        }

        return true;
    }

    static bool TryGetAllPayloads(
        IEnumerable<IOperation> operations,
        List<INamedTypeSymbol> eventTypes,
        bool allowEmpty)
    {
        var found = false;
        foreach (var operation in operations)
        {
            if (!TryGetPayloads(operation, eventTypes, allowContainer: false))
            {
                return false;
            }

            found = true;
        }

        return found || allowEmpty;
    }

    static IOperation Unwrap(IOperation operation)
    {
        var current = operation;
        while (true)
        {
            switch (current)
            {
                case IConversionOperation conversion when
                    !conversion.Conversion.IsUserDefined &&
                    (conversion.Conversion.IsIdentity ||
                     conversion.Conversion.IsReference ||
                     conversion.Operand is ICollectionExpressionOperation):
                    current = conversion.Operand;
                    continue;
                case IParenthesizedOperation parenthesized:
                    current = parenthesized.Operand;
                    continue;
                default:
                    return current;
            }
        }
    }

    static bool IsSafeReceiverConversion(IConversionOperation conversion) =>
        !conversion.Conversion.IsUserDefined &&
        (conversion.Conversion.IsIdentity || conversion.Conversion.IsReference) &&
        conversion.Type?.SpecialType != SpecialType.System_Object &&
        conversion.Operand.Type?.SpecialType != SpecialType.System_Object &&
        conversion.Operand.Type?.TypeKind != TypeKind.Dynamic;

    static bool IsObjectArray(ITypeSymbol type) =>
        type is IArrayTypeSymbol { ElementType.SpecialType: SpecialType.System_Object };

    static bool IsObjectEnumerable(ITypeSymbol type) =>
        type is INamedTypeSymbol named &&
        named.IsGenericType &&
        DotNetSubjectIds.MetadataName(named.OriginalDefinition) == "System.Collections.Generic.IEnumerable`1" &&
        named.TypeArguments[0].SpecialType == SpecialType.System_Object;

    static bool IsObjectCollection(ITypeSymbol? type) =>
        type is INamedTypeSymbol named &&
        named.AllInterfaces.Concat([named]).Any(IsObjectEnumerable);

    static bool IsSupportedCollectionInitializer(IObjectCreationOperation operation) =>
        operation.Type is INamedTypeSymbol type &&
        type.ContainingNamespace.ToDisplayString().StartsWith("System.Collections", StringComparison.Ordinal) &&
        IsObjectCollection(type);

    static bool IsEventPayloadType(ITypeSymbol? type) =>
        type is INamedTypeSymbol named &&
        named.SpecialType == SpecialType.None &&
        !WolverineReturnTypes.IsSpecialReturn(named) &&
        !DotNetSubjectIds.MetadataName(named.OriginalDefinition).StartsWith("System.", StringComparison.Ordinal);

    static bool IsInDirectHandlerBody(
        InvocationExpressionSyntax invocation,
        MethodDeclarationSyntax handlerDeclaration) =>
        !invocation.Ancestors()
            .TakeWhile(_ => _ != handlerDeclaration)
            .Any(_ => _ is LocalFunctionStatementSyntax or AnonymousFunctionExpressionSyntax);
}
