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
    LoadedEventStream,
    FetchedEventStream
}

sealed record WolverineStateBindingEvidence<T>(T Value, SourceRange? Source);

sealed record WolverineStateBinding(
    string HandlerKey,
    IParameterSymbol? Parameter,
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
    public WolverineFetchedStreamBinding? Fetched { get; init; }

    public bool LoadsModel => Kind is WolverineStateBindingKind.LoadedEventStream or WolverineStateBindingKind.FetchedEventStream;

    public string Discriminator => Fetched is { } fetched
        ? $"stream:fetch:{fetched.Site}:{(fetched.Index is { } index ? index.ToString(System.Globalization.CultureInfo.InvariantCulture) : "family")}"
        : $"stream:{HandlerKey}:{Parameter!.Ordinal}:{Parameter.Name}";
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

    public static IReadOnlyList<WolverineStateBinding> ParameterBindings(
        IMethodSymbol method,
        INamedTypeSymbol? requestType,
        DotNetProjectCompilation project)
    {
        var bindings = new List<WolverineStateBinding>();
        var loadedBindingCount = method.Parameters.Count(parameter =>
            EventStreamModels(parameter.Type, project).Count > 0 &&
            WriteModelAttribute(parameter, project) is not null);
        var context = new BindingContext(method, requestType, project, DotNetMethodIdentity.SubjectFor(project, method).Value, loadedBindingCount);
        foreach (var parameter in method.Parameters)
        {
            var modelTypes = EventStreamModels(parameter.Type, project);
            if (modelTypes.Count > 0)
            {
                bindings.AddRange(BindingsForParameter(context, parameter, modelTypes));
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
            var scope = new AppendScope(new(declaration, semanticModel, project), bindings);
            foreach (var invocationSyntax in DotNetSource.AuthoredInvocationsIn(declaration, project))
            {
                if (DirectAppendOf(invocationSyntax, scope) is not { } direct)
                {
                    continue;
                }

                hasDirectWrite = true;
                var outcome = ResolveAppend(direct, scope);
                appends.AddRange(outcome.Appends);
                if (outcome.Unresolved is { } reason)
                {
                    unresolved.Add(reason);
                }
            }
        }

        return new(appends, unresolved, hasDirectWrite);
    }

    // The exact IEventStream<T> append call a direct handler-body invocation represents, or null when it is anything else.
    static DirectAppend? DirectAppendOf(InvocationExpressionSyntax invocationSyntax, AppendScope scope)
    {
        var method = scope.Method;
        if (!IsInDirectHandlerBody(invocationSyntax, method.Declaration) ||
            DotNetInvocations.MethodFor(invocationSyntax, method.Model) is not { } invocationMethod ||
            method.Model.GetOperation(invocationSyntax) is not IInvocationOperation invocation ||
            !TryGetAppendModel(invocation, method.Project, out var invokedModel))
        {
            return null;
        }

        return new(invocationSyntax, invocationMethod, invocation, invokedModel);
    }

    static AppendOutcome ResolveAppend(DirectAppend direct, AppendScope scope)
    {
        var method = scope.Method;
        var source = CritterStackSource.RangeForProject(direct.Syntax.GetLocation(), method.Project);
        if (DotNetInvocations.ReceiverFor(direct.Syntax, direct.Method, method.Model) is not { } receiverExpression ||
            (method.Model.GetOperation(receiverExpression) ?? direct.Invocation.Instance) is not { } receiverOperation)
        {
            return AppendOutcome.Failed("the receiver could not be resolved", source);
        }

        var targets = AppendTargets(receiverOperation, direct.Syntax, direct.Model, scope);
        if (targets.Count == 0)
        {
            return AppendOutcome.Failed("the receiver is not rooted directly in an IEventStream<T> handler parameter or a supported, unmodified fetched-stream slot or foreach binding", source);
        }

        if (!TryGetDirectPayloads(direct.Invocation, out var eventTypes))
        {
            return AppendOutcome.Failed("the appended payload is not a direct object creation, params value, array, collection expression, or direct collection initializer", source);
        }

        var admittedEventTypes = eventTypes
            .Where(eventType => !WolverineSagaTypes.IsSagaState(eventType, method.Project))
            .ToArray();
        return admittedEventTypes.Length > 0
            ? new AppendOutcome([.. targets.Select(binding => new WolverineEventStreamAppend(binding, admittedEventTypes, source))], null)
            : new AppendOutcome([], null);
    }

    public static bool TryGetDirectPayloads(
        IInvocationOperation invocation,
        out IReadOnlyList<INamedTypeSymbol> eventTypes) => TryGetPayloads(invocation, out eventTypes);

    public static bool TryGetDirectPayloads(
        IOperation operation,
        out IReadOnlyList<INamedTypeSymbol> eventTypes,
        bool allowEmpty = false)
    {
        var payloads = new List<INamedTypeSymbol>();
        if (!TryGetPayloads(operation, payloads, allowEmpty))
        {
            eventTypes = [];
            return false;
        }

        eventTypes = payloads;
        return true;
    }

    public static bool IsEventStream(ITypeSymbol type) => EventStreamInterfaces(type).Count > 0;

    public static bool IsExactAppend(IInvocationOperation invocation, DotNetProjectCompilation project) =>
        TryGetAppendModel(invocation, project, out _);

    static IEnumerable<WolverineStateBinding> BindingsForParameter(
        BindingContext context,
        IParameterSymbol parameter,
        IReadOnlyList<INamedTypeSymbol> modelTypes)
    {
        var declaration = DeclarationOf(WriteModelAttribute(parameter, context.Project), parameter, context.Project);
        foreach (var modelType in modelTypes)
        {
            yield return BindingFor(context, parameter, modelType, declaration);
        }
    }

    static ParameterDeclaration DeclarationOf(AttributeData? attribute, IParameterSymbol parameter, DotNetProjectCompilation project) => new(
        attribute,
        attribute?.ConstructorArguments.FirstOrDefault().Value as string,
        NamedString(attribute, "VersionSource"),
        NamedEnum(attribute, "LoadStyle") ?? "Optimistic",
        NamedBoolean(attribute, "AlwaysEnforceConsistency") ?? HasConsistentAggregateDefault(parameter, project),
        SourceOf(attribute, parameter, project));

    static bool HasConsistentAggregateDefault(IParameterSymbol parameter, DotNetProjectCompilation project) =>
        WolverineReadModelParameters.ConsistentAttribute(parameter, project) is not null;

    static WolverineStateBinding BindingFor(
        BindingContext context,
        IParameterSymbol parameter,
        INamedTypeSymbol modelType,
        ParameterDeclaration declaration)
    {
        var lookup = context.Lookup;
        var project = context.Project;
        var isLoaded = declaration.Attribute is not null;
        var identityMember = isLoaded ? IdentityMember(lookup, modelType, declaration.IdentityName) : null;
        var versionMember = isLoaded ? VersionMember(lookup, declaration.VersionName, context.LoadedBindingCount) : null;
        var hasAmbiguousConventionalVersion = isLoaded &&
            string.IsNullOrWhiteSpace(declaration.VersionName) &&
            HasConventionalVersionMember(context);
        var source = declaration.Source;
        return new(
            context.HandlerKey,
            parameter,
            modelType,
            isLoaded
                ? WolverineStateBindingKind.LoadedEventStream
                : WolverineStateBindingKind.DirectEventStream,
            new(declaration.IdentityName ?? identityMember?.Name, EvidenceSource(identityMember, source, project)),
            new(declaration.VersionName ?? versionMember?.Name, EvidenceSource(versionMember, source, project)),
            new(declaration.LoadStyle, source),
            new(declaration.Consistency, source),
            identityMember,
            versionMember,
            hasAmbiguousConventionalVersion,
            source);
    }

    static bool HasConventionalVersionMember(BindingContext context) =>
        context.LoadedBindingCount > 1 &&
        VersionMember(context.Lookup, "Version", loadedBindingCount: 1) is not null;

    static IReadOnlyList<WolverineStateBinding> AppendTargets(
        IOperation receiverOperation,
        InvocationExpressionSyntax invocationSyntax,
        INamedTypeSymbol invokedModel,
        AppendScope scope)
    {
        var receiver = ReceiverParameter(receiverOperation);
        var bindings = scope.Bindings;
        var targets = receiver is null
            ? WolverineFetchedEventStreams.Targets(receiverOperation, invocationSyntax, scope.Method, bindings)
            : [.. bindings.Where(binding => SymbolEqualityComparer.Default.Equals(binding.Parameter, receiver))];
        return [.. targets.Where(binding => SymbolEqualityComparer.Default.Equals(binding.ModelType, invokedModel))];
    }

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
            !WolverineSymbolAuthority.IsAuthoredOrMetadataSymbol(streamDefinition, project))
        {
            return false;
        }

        if (!SymbolEqualityComparer.Default.Equals(method.ContainingType.OriginalDefinition, streamDefinition) ||
            method.ContainingType.TypeArguments.SingleOrDefault() is not INamedTypeSymbol model)
        {
            return false;
        }

        var isExactAppend = ExpectedAppendSignatures(streamDefinition)
            .Any(signature => DotNetMethodSignatures.Matches(method, signature));
        if (isExactAppend)
        {
            modelType = model;
        }

        return isExactAppend;
    }

    static IEnumerable<DotNetMethodSignature> ExpectedAppendSignatures(INamedTypeSymbol streamDefinition) =>
        streamDefinition.GetMembers("AppendOne")
            .OfType<IMethodSymbol>()
            .Where(candidate => candidate.Parameters is [{ Type.SpecialType: SpecialType.System_Object }])
            .Concat(streamDefinition.GetMembers("AppendMany")
                .OfType<IMethodSymbol>()
                .Where(candidate => candidate.Parameters is [var parameter] &&
                    (IsObjectArray(parameter.Type) || IsObjectEnumerable(parameter.Type))))
            .Select(DotNetMethodSignatures.From);

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
        if (!WolverineSymbolAuthority.IsAuthoredOrMetadataSymbol(streamType.OriginalDefinition, project) ||
            parameterType is not INamedTypeSymbol named)
        {
            return false;
        }

        if (SymbolEqualityComparer.Default.Equals(named.OriginalDefinition, streamType.OriginalDefinition) ||
            named.DeclaringSyntaxReferences.Length == 0)
        {
            return true;
        }

        return AuthoredBaseTypes(named, project).Any(candidate =>
            EventStreamInterfaces(candidate).Any(@interface => SymbolEqualityComparer.Default.Equals(@interface, streamType)));
    }

    static IEnumerable<INamedTypeSymbol> AuthoredBaseTypes(INamedTypeSymbol type, DotNetProjectCompilation project) =>
        type.DeclaringSyntaxReferences
            .Select(reference => reference.GetSyntax())
            .OfType<TypeDeclarationSyntax>()
            .Where(declaration =>
                declaration.BaseList is not null &&
                project.AuthoredSyntaxTrees.Contains(declaration.SyntaxTree) &&
                !DotNetGeneratedSource.IsGenerated(declaration.SyntaxTree))
            .SelectMany(declaration => BaseListTypes(declaration, project));

    static IEnumerable<INamedTypeSymbol> BaseListTypes(TypeDeclarationSyntax declaration, DotNetProjectCompilation project)
    {
        var semanticModel = project.Compilation.GetSemanticModel(declaration.SyntaxTree);
        return declaration.BaseList!.Types
            .Select(baseType => semanticModel.GetTypeInfo(baseType.Type).Type)
            .OfType<INamedTypeSymbol>();
    }

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
                WolverineSymbolAuthority.IsAuthoredOrMetadataSymbol(admitted, project) &&
                DotNetSymbols.IsOrInheritsFrom(attributeType, metadataName)))
        {
            return false;
        }

        if (admittedBases.Contains(DotNetSubjectIds.MetadataName(attributeType.OriginalDefinition), StringComparer.Ordinal) ||
            attributeType.DeclaringSyntaxReferences.Length == 0)
        {
            return true;
        }

        return AuthoredBaseTypes(attributeType, project).Any(candidate =>
            admittedBases.Any(metadataName => DotNetSymbols.IsOrInheritsFrom(candidate, metadataName)));
    }

    static ISymbol? IdentityMember(
        MemberLookup lookup,
        INamedTypeSymbol modelType,
        string? explicitName)
    {
        var members = SourceMembers(lookup);
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
        MemberLookup lookup,
        string? explicitName,
        int loadedBindingCount)
    {
        if (loadedBindingCount > 1 && string.IsNullOrWhiteSpace(explicitName))
        {
            return null;
        }

        var name = string.IsNullOrWhiteSpace(explicitName) ? "Version" : explicitName;
        return SourceMembers(lookup).FirstOrDefault(member =>
            string.Equals(member.Name, name, StringComparison.OrdinalIgnoreCase) &&
            TypeOf(member)?.SpecialType is SpecialType.System_Int32 or SpecialType.System_Int64 or SpecialType.System_UInt32);
    }

    static IReadOnlyList<ISymbol> SourceMembers(MemberLookup lookup) => lookup.RequestType is null
        ? [.. lookup.Method.Parameters.Where(parameter =>
            !IsEventStream(parameter.Type) &&
            WolverineSymbolAuthority.IsAuthoredOrMetadataSymbol(parameter, lookup.Project))]
        : [.. lookup.RequestType.GetMembers().OfType<IPropertySymbol>().Where(property => WolverineSymbolAuthority.IsAuthoredOrMetadataSymbol(property, lookup.Project))];

    static ITypeSymbol? TypeOf(ISymbol symbol) => symbol switch
    {
        IPropertySymbol property => property.Type,
        IParameterSymbol parameter => parameter.Type,
        _ => null
    };

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
        return location is null ? fallback : CritterStackSource.RangeForProject(location, project);
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
            return CritterStackSource.RangeForProject(syntax.GetLocation(), project);
        }

        var location = parameter.Locations.FirstOrDefault(candidate =>
            candidate.IsInSource &&
            candidate.SourceTree is not null &&
            project.AuthoredSyntaxTrees.Contains(candidate.SourceTree) &&
            !DotNetGeneratedSource.IsGenerated(candidate.SourceTree));
        return location is null ? null : CritterStackSource.RangeForProject(location, project);
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
        if (TryGetSinglePayload(operation, eventTypes))
        {
            return true;
        }

        return allowContainer && TryGetContainerPayloads(operation, eventTypes, allowEmpty);
    }

    static bool TryGetSinglePayload(IOperation operation, List<INamedTypeSymbol> eventTypes)
    {
        if (operation is not IObjectCreationOperation objectCreation ||
            IsObjectCollection(objectCreation.Type) ||
            !IsEventPayloadType(objectCreation.Type))
        {
            return false;
        }

        eventTypes.Add((INamedTypeSymbol)objectCreation.Type!);
        return true;
    }

    static bool TryGetContainerPayloads(IOperation operation, List<INamedTypeSymbol> eventTypes, bool allowEmpty) => operation switch
    {
        IArrayCreationOperation { Initializer: not null } arrayCreation =>
            TryGetAllPayloads(arrayCreation.Initializer.ElementValues, eventTypes, allowEmpty),
        IArrayInitializerOperation arrayInitializer =>
            TryGetAllPayloads(arrayInitializer.ElementValues, eventTypes, allowEmpty),
        ICollectionExpressionOperation collectionExpression =>
            !collectionExpression.Elements.Any(_ => _ is ISpreadOperation) &&
            TryGetAllPayloads(collectionExpression.Elements, eventTypes, allowEmpty),
        IObjectCreationOperation { Initializer: not null } collectionCreation when IsSupportedCollectionInitializer(collectionCreation) =>
            TryGetCollectionInitializerPayloads(collectionCreation.Initializer, eventTypes, allowEmpty),
        _ => false
    };

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
                !IsCollectionAdd(invocation) ||
                !invocation.Arguments.All(argument => TryGetPayloads(argument.Value, eventTypes, allowContainer: false)))
            {
                return false;
            }
        }

        return true;
    }

    static bool IsCollectionAdd(IInvocationOperation invocation) =>
        invocation.TargetMethod.Name == "Add" &&
        invocation.TargetMethod.ContainingNamespace.ToDisplayString().StartsWith("System.Collections", StringComparison.Ordinal) &&
        invocation.Arguments.Length != 0;

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
                case IConversionOperation conversion when IsTransparentConversion(conversion):
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

    static bool IsTransparentConversion(IConversionOperation conversion) =>
        !conversion.Conversion.IsUserDefined &&
        (conversion.Conversion.IsIdentity ||
         conversion.Conversion.IsReference ||
         conversion.Operand is ICollectionExpressionOperation);

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

    sealed record MemberLookup(
        IMethodSymbol Method,
        INamedTypeSymbol? RequestType,
        DotNetProjectCompilation Project);

    sealed record BindingContext(
        IMethodSymbol Method,
        INamedTypeSymbol? RequestType,
        DotNetProjectCompilation Project,
        string HandlerKey,
        int LoadedBindingCount)
    {
        public MemberLookup Lookup => new(Method, RequestType, Project);
    }

    sealed record ParameterDeclaration(
        AttributeData? Attribute,
        string? IdentityName,
        string? VersionName,
        string LoadStyle,
        bool Consistency,
        SourceRange? Source);

    sealed record AppendScope(
        WolverineMethodScope Method,
        IReadOnlyList<WolverineStateBinding> Bindings);

    sealed record DirectAppend(
        InvocationExpressionSyntax Syntax,
        IMethodSymbol Method,
        IInvocationOperation Invocation,
        INamedTypeSymbol Model);

    sealed record AppendOutcome(
        IReadOnlyList<WolverineEventStreamAppend> Appends,
        WolverineUnresolvedEventStreamAppend? Unresolved)
    {
        public static AppendOutcome Failed(string reason, SourceRange? source) => new([], new(reason, source));
    }
}
