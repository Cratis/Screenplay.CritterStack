// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Globalization;

using Cratis.Screenplay.Generation;
using Cratis.Screenplay.Generation.DotNet;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Operations;

namespace Cratis.CritterStack.Screenplay.Wolverine;

sealed record WolverineFetchedStreamBinding
{
    public required ILocalSymbol Local { get; init; }

    public required string Site { get; init; }

    public required int? Index { get; init; }

    public required bool IsCollection { get; init; }

    public required bool IsEmpty { get; init; }

    public required bool HasRepeatedIdentity { get; init; }

    public required bool HasUncomparableIdentity { get; init; }

    public required string IdentityExpression { get; init; }

    public bool IsProvenDistinct => !HasRepeatedIdentity && !HasUncomparableIdentity;
}

/// <summary>
/// The handler body being analyzed: its declaration, the semantic model that owns it and the project.
/// </summary>
/// <param name="Declaration">The handler method declaration.</param>
/// <param name="Model">The semantic model for the declaration's syntax tree.</param>
/// <param name="Project">The project compilation.</param>
sealed record WolverineMethodScope(
    MethodDeclarationSyntax Declaration,
    SemanticModel Model,
    DotNetProjectCompilation Project);

static class WolverineFetchedEventStreams
{
    public static IReadOnlyList<WolverineStateBinding> Bindings(IMethodSymbol method, INamedTypeSymbol? requestType, DotNetProjectCompilation project)
    {
        var request = method.Parameters.FirstOrDefault(parameter => SymbolEqualityComparer.Default.Equals(parameter.Type, requestType));
        var origin = new FetchOrigin(method, request, project);
        var bindings = new List<WolverineStateBinding>();
        foreach (var (declaration, semanticModel) in WolverineMethodSyntax.Declarations(method, project))
        {
            var scope = new WolverineMethodScope(declaration, semanticModel, project);
            foreach (var variable in declaration.DescendantNodes().OfType<VariableDeclaratorSyntax>())
            {
                if (FetchSiteOf(variable, scope) is { } fetchSite)
                {
                    bindings.AddRange(BindingsFor(fetchSite, origin));
                }
            }
        }

        return bindings;
    }

    public static IReadOnlyList<WolverineStateBinding> Targets(
        IOperation receiver,
        InvocationExpressionSyntax append,
        WolverineMethodScope scope,
        IReadOnlyList<WolverineStateBinding> bindings)
    {
        var fetched = bindings.Where(binding => binding.Fetched is { IsEmpty: false, IsProvenDistinct: true }).ToArray();
        if (IndexedTarget(Unwrap(receiver), fetched, scope) is { } indexed)
        {
            return [indexed];
        }

        if (Unwrap(receiver) is not ILocalReferenceOperation local || !IsStable(local.Local, scope))
        {
            return [];
        }

        return TargetFromInitializer(local.Local, fetched, scope) is { } fromIndex
            ? [fromIndex]
            : TargetsFromLoop(append, local.Local, fetched, scope);
    }

    static WolverineStateBinding? TargetFromInitializer(
        ILocalSymbol local,
        IReadOnlyList<WolverineStateBinding> fetched,
        WolverineMethodScope scope)
    {
        var initializer = local.DeclaringSyntaxReferences.Select(reference => reference.GetSyntax())
            .OfType<VariableDeclaratorSyntax>().SingleOrDefault()?.Initializer?.Value;
        return initializer is null
            ? null
            : IndexedTarget(Unwrap(scope.Model.GetOperation(initializer)), fetched, scope);
    }

    static IReadOnlyList<WolverineStateBinding> TargetsFromLoop(
        InvocationExpressionSyntax append,
        ILocalSymbol local,
        IReadOnlyList<WolverineStateBinding> fetched,
        WolverineMethodScope scope)
    {
        foreach (var loop in append.Ancestors().TakeWhile(node => node != scope.Declaration).OfType<ForEachStatementSyntax>())
        {
            if (LoopCollection(loop, local, scope) is { } collection)
            {
                return [.. fetched.Where(binding => SymbolEqualityComparer.Default.Equals(binding.Fetched!.Local, collection))];
            }
        }

        return [];
    }

    // The stable collection a foreach loop over `local` iterates, or null when the loop does not bind it.
    static ILocalSymbol? LoopCollection(ForEachStatementSyntax loop, ILocalSymbol local, WolverineMethodScope scope)
    {
        if (!SymbolEqualityComparer.Default.Equals(scope.Model.GetDeclaredSymbol(loop), local))
        {
            return null;
        }

        return Unwrap(scope.Model.GetOperation(loop.Expression)) is ILocalReferenceOperation collection && IsStable(collection.Local, scope)
            ? collection.Local
            : null;
    }

    static FetchSite? FetchSiteOf(VariableDeclaratorSyntax variable, WolverineMethodScope scope)
    {
        if (variable.Initializer is not { } initializer || !IsDirect(variable, scope.Declaration))
        {
            return null;
        }

        return scope.Model.GetDeclaredSymbol(variable) is ILocalSymbol local
            ? FetchSiteFor(local, AwaitedFetch(initializer, scope.Model), scope.Project)
            : null;
    }

    static FetchSite? FetchSiteFor(ILocalSymbol local, IInvocationOperation? fetch, DotNetProjectCompilation project) =>
        fetch is not null &&
        TryModel(fetch.TargetMethod, project, out var model) &&
        fetch.Arguments.FirstOrDefault(argument => argument.Parameter?.Ordinal == 0)?.Value is { } identities
            ? new FetchSite(local, fetch, model, identities)
            : null;

    static IInvocationOperation? AwaitedFetch(EqualsValueClauseSyntax initializer, SemanticModel semanticModel) =>
        Unwrap(semanticModel.GetOperation(initializer.Value)) is IAwaitOperation awaited
            ? FetchInvocation(awaited.Operation)
            : null;

    static IEnumerable<WolverineStateBinding> BindingsFor(FetchSite fetchSite, FetchOrigin origin)
    {
        var project = origin.Project;
        var fetch = fetchSite.Fetch;
        var source = CritterStackSource.RangeForProject(fetch.Syntax.GetLocation(), project);
        var site = $"{DotNetMethodIdentity.SubjectFor(project, origin.Method).Value}:{source?.Path}:{fetch.Syntax.SpanStart.ToString(CultureInfo.InvariantCulture)}";
        var input = Unwrap(fetchSite.Identities)!;
        var elements = Elements(input);
        var (isRepeated, isUncomparable) = DistinctnessOf(elements);
        var bindingSite = new BindingSite(fetchSite, origin, site, source, isRepeated, isUncomparable);

        if (elements is { Count: > 0 })
        {
            return [.. elements.Select((element, index) => Bind(bindingSite, new(element, index, IsCollection: false, IsEmpty: false)))];
        }

        return [Bind(bindingSite, new(input, null, IsCollection: elements is null, IsEmpty: elements is { Count: 0 }))];
    }

    // A fixed batch of two or more identities only proves distinct streams when every identity is keyed
    // semantically; an identity that is not (an invocation or another arbitrary expression) may repeat
    // another, and the framework rejects duplicate IDs at runtime.
    static IdentityDistinctness DistinctnessOf(IReadOnlyList<IOperation>? elements)
    {
        var keys = elements is { Count: > 1 } ? elements.Select(IdentityKey).ToArray() : [];
        var uncomparable = keys.Any(key => key is null);
        var repeated = !uncomparable && keys.Distinct(StringComparer.Ordinal).Count() != keys.Length;
        return new(repeated, uncomparable);
    }

    static WolverineStateBinding Bind(BindingSite site, StreamSlot slot)
    {
        var origin = site.Origin;
        var source = site.Source;
        var member = IdentityMember(slot.Identity, origin);
        return new WolverineStateBinding(
            DotNetMethodIdentity.SubjectFor(origin.Project, origin.Method).Value,
            null,
            site.Fetch.Model,
            WolverineStateBindingKind.FetchedEventStream,
            new(member?.Name, source),
            new(null, source),
            new("Optimistic", source),
            new(false, source),
            member,
            null,
            false,
            source)
        {
            Fetched = new WolverineFetchedStreamBinding
            {
                Local = site.Fetch.Local,
                Site = site.Site,
                Index = slot.Index,
                IsCollection = slot.IsCollection,
                IsEmpty = slot.IsEmpty,
                HasRepeatedIdentity = site.IsRepeated,
                HasUncomparableIdentity = site.IsUncomparable,
                IdentityExpression = slot.Identity.Syntax.ToString()
            }
        };
    }

    static WolverineStateBinding? IndexedTarget(
        IOperation? operation,
        IReadOnlyList<WolverineStateBinding> bindings,
        WolverineMethodScope scope)
    {
        if (ConstantIndexedLocal(operation) is not var (collection, index) ||
            !IsStable(collection, scope))
        {
            return null;
        }

        return bindings.FirstOrDefault(binding => binding.Fetched is { IsCollection: false } fetched &&
            fetched.Index == index && SymbolEqualityComparer.Default.Equals(fetched.Local, collection));
    }

    static (ILocalSymbol Local, int Index)? ConstantIndexedLocal(IOperation? operation) =>
        operation is IPropertyReferenceOperation { Property.IsIndexer: true, Arguments.Length: 1 } indexer &&
        Unwrap(indexer.Instance) is ILocalReferenceOperation collection &&
        indexer.Arguments[0].Value.ConstantValue is { HasValue: true, Value: int index }
            ? (collection.Local, index)
            : null;

    static IInvocationOperation? FetchInvocation(IOperation operation)
    {
        var invocation = Unwrap(operation) as IInvocationOperation;
        if (invocation?.TargetMethod is { } target && IsConfigureAwait(target))
        {
            invocation = Unwrap(invocation.Instance) as IInvocationOperation;
        }

        return invocation;
    }

    static bool IsConfigureAwait(IMethodSymbol method) =>
        method is { IsStatic: false, Parameters.Length: 1 } &&
        string.Equals(method.Name, "ConfigureAwait", StringComparison.Ordinal) &&
        DotNetSubjectIds.MetadataName(method.ContainingType.OriginalDefinition) == "System.Threading.Tasks.Task`1" &&
        method.Parameters[0].Type.SpecialType == SpecialType.System_Boolean;

    static bool TryModel(IMethodSymbol method, DotNetProjectCompilation project, out INamedTypeSymbol model)
    {
        model = null!;
        if (ModelTypeOf(method) is not { } modelType || !IsFetchManyContract(method, modelType, project))
        {
            return false;
        }

        model = modelType;
        return true;
    }

    static INamedTypeSymbol? ModelTypeOf(IMethodSymbol method) =>
        IsFetchManyShape(method) && method.TypeArguments[0] is INamedTypeSymbol { TypeKind: not TypeKind.Error } modelType
            ? modelType
            : null;

    static bool IsFetchManyContract(IMethodSymbol method, INamedTypeSymbol modelType, DotNetProjectCompilation project) =>
        IsDeclaredOnEventStoreOperations(method, project) &&
        IsIdentityListFetch(method) &&
        ReturnsEventStreamsOf(method.ReturnType, modelType, project);

    static bool IsFetchManyShape(IMethodSymbol method) =>
        method.Name == "FetchManyForWriting" && !method.IsStatic && method.ReducedFrom is null && method.Arity == 1;

    static bool IsDeclaredOnEventStoreOperations(IMethodSymbol method, DotNetProjectCompilation project) =>
        project.Compilation.GetTypeByMetadataName(WellKnownTypes.JasperFxEventStoreOperations) is { } contract &&
        WolverineSymbolAuthority.IsAuthoredOrMetadataSymbol(contract, project) &&
        WolverineSymbolAuthority.IsAuthoredOrMetadataSymbol(method.OriginalDefinition, project) &&
        SymbolEqualityComparer.Default.Equals(method.ContainingType, contract);

    static bool IsIdentityListFetch(IMethodSymbol method) =>
        method.Parameters is [{ Type: INamedTypeSymbol ids }, { Type: INamedTypeSymbol cancellation }] &&
        DotNetSubjectIds.MetadataName(ids.OriginalDefinition) == "System.Collections.Generic.IReadOnlyList`1" &&
        ids.TypeArguments[0] is INamedTypeSymbol id &&
        (id.SpecialType == SpecialType.System_String || DotNetSubjectIds.MetadataName(id) == "System.Guid") &&
        DotNetSubjectIds.MetadataName(cancellation) == "System.Threading.CancellationToken";

    static bool ReturnsEventStreamsOf(ITypeSymbol returnType, INamedTypeSymbol modelType, DotNetProjectCompilation project) =>
        returnType is INamedTypeSymbol task &&
        DotNetSubjectIds.MetadataName(task.OriginalDefinition) == "System.Threading.Tasks.Task`1" &&
        task.TypeArguments[0] is INamedTypeSymbol list &&
        DotNetSubjectIds.MetadataName(list.OriginalDefinition) == "System.Collections.Generic.IReadOnlyList`1" &&
        list.TypeArguments[0] is INamedTypeSymbol stream &&
        DotNetSubjectIds.MetadataName(stream.OriginalDefinition) == WellKnownTypes.JasperFxEventStream &&
        WolverineSymbolAuthority.IsAuthoredOrMetadataSymbol(stream.OriginalDefinition, project) &&
        SymbolEqualityComparer.Default.Equals(stream.TypeArguments[0], modelType);

    static IReadOnlyList<IOperation>? Elements(IOperation input) => input switch
    {
        IArrayCreationOperation { Initializer: { } initializer } => initializer.ElementValues,
        IArrayCreationOperation { DimensionSizes: [{ ConstantValue: { HasValue: true, Value: 0 } }] } => [],
        IInvocationOperation { TargetMethod: { Name: "Empty", IsStatic: true, Arity: 1, Parameters.Length: 0 } empty } when
            empty.ContainingType.SpecialType == SpecialType.System_Array => [],
        ICollectionExpressionOperation collection when !collection.Elements.Any(element => element is ISpreadOperation) => collection.Elements,
        _ => null
    };

    static ISymbol? IdentityMember(IOperation operation, FetchOrigin origin) => Unwrap(operation) switch
    {
        IPropertyReferenceOperation property when IsRequestProperty(property, origin) => property.Property,
        IParameterReferenceOperation parameter when origin.Request is null &&
            SymbolEqualityComparer.Default.Equals(parameter.Parameter.ContainingSymbol, origin.Method) => parameter.Parameter,
        _ => null
    };

    static bool IsRequestProperty(IPropertyReferenceOperation property, FetchOrigin origin) =>
        Unwrap(property.Instance) is IParameterReferenceOperation parameter &&
        SymbolEqualityComparer.Default.Equals(parameter.Parameter, origin.Request) &&
        WolverineSymbolAuthority.IsAuthoredOrMetadataSymbol(property.Property, origin.Project);

    // Returns null when the identity is not keyed semantically, so it cannot be proven distinct from another identity.
    static string? IdentityKey(IOperation operation)
    {
        operation = Unwrap(operation)!;
        return operation.ConstantValue.HasValue
            ? $"constant:{operation.Type}:{operation.ConstantValue.Value}"
            : MemberIdentityKey(operation);
    }

    static string? MemberIdentityKey(IOperation operation)
    {
        return operation switch
        {
            // A member read is the same identity wherever and however it is written, so key it by the member's
            // containing type and name plus a normalized receiver rather than by source text or position; otherwise
            // `[Guid.Empty, Guid.Empty]` or `[command.Id, (command).Id]` looks distinct, and `SourceIds.Id` and
            // `DestinationIds.Id` look the same.
            IFieldReferenceOperation field =>
                Join($"field:{MemberKey(field.Field)}", ReceiverKey(field.Instance, field.Field.IsStatic)),
            IPropertyReferenceOperation property => PropertyKey(property),
            IArrayElementReferenceOperation element =>
                Join("element:", ReceiverKey(element.ArrayReference, isStatic: false), ArgumentsKey(element.Indices)),
            IParameterReferenceOperation parameter =>
                $"parameter:{SymbolKey(parameter.Parameter.ContainingSymbol)}:{parameter.Parameter.Ordinal.ToString(CultureInfo.InvariantCulture)}",
            ILocalReferenceOperation local =>
                $"local:{local.Local.Name}:{LocationKey(local.Local)}",
            _ => null
        };
    }

    static string? PropertyKey(IPropertyReferenceOperation property) =>
        Join(
            $"property:{MemberKey(property.Property)}",
            ReceiverKey(property.Instance, property.Property.IsStatic),
            ArgumentsKey(property.Arguments.Select(argument => argument.Value)));

    static string? Join(params string?[] parts) => parts.Any(part => part is null) ? null : string.Concat(parts);

    static string MemberKey(ISymbol member) => $"{SymbolKey(member.ContainingType)}.{member.MetadataName}";

    static string SymbolKey(ISymbol? symbol) => symbol?.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat
        .WithMemberOptions(SymbolDisplayMemberOptions.IncludeContainingType | SymbolDisplayMemberOptions.IncludeParameters)) ?? "?";

    static string LocationKey(ISymbol symbol) => symbol.Locations
        .Where(_ => _.IsInSource)
        .Select(_ => _.SourceSpan.Start.ToString(CultureInfo.InvariantCulture))
        .DefaultIfEmpty("?")
        .First();

    static string? ReceiverKey(IOperation? receiver, bool isStatic) => isStatic
        ? string.Empty
        : Unwrap(receiver) switch
        {
            IInstanceReferenceOperation => "@this",
            { } instance => IdentityKey(instance) is { } key ? $"@({key})" : null,
            null => "@?"
        };

    static string? ArgumentsKey(IEnumerable<IOperation> arguments)
    {
        var keys = arguments.Select(IdentityKey).ToArray();
        if (keys.Length == 0)
        {
            return string.Empty;
        }

        return keys.Any(key => key is null) ? null : $"[{string.Join(',', keys)}]";
    }

    static bool IsStable(ILocalSymbol local, WolverineMethodScope scope) =>
        scope.Declaration.DescendantNodes().OfType<IdentifierNameSyntax>()
            .Where(identifier => SymbolEqualityComparer.Default.Equals(scope.Model.GetSymbolInfo(identifier).Symbol, local))
            .All(identifier => IsDirect(identifier, scope.Declaration) &&
                scope.Model.GetOperation(identifier) is { } reference && IsSupportedUse(reference, scope));

    static bool IsSupportedUse(IOperation operation, WolverineMethodScope scope)
    {
        var model = scope.Model;
        var project = scope.Project;
        operation = Outermost(operation);
        if (IsDirectAppendReceiver(operation, project))
        {
            return true;
        }

        if (ConstantIndexer(operation) is { } indexer)
        {
            return IsSupportedIndexedSlotUse(Outermost(indexer), scope);
        }

        if (LoopElement(operation, model) is { } element)
        {
            return IsStable(element, scope);
        }

        // Unknown reads can alias the stream just as writes can. In particular, extension
        // receivers, arguments, captures and non-append member access are not safe uses.
        return false;
    }

    static IPropertyReferenceOperation? ConstantIndexer(IOperation operation) =>
        operation.Parent is IPropertyReferenceOperation { Property.IsIndexer: true, Arguments: [{ Value.ConstantValue: { HasValue: true, Value: int } }] } indexer &&
        indexer.Instance == operation
            ? indexer
            : null;

    static ILocalSymbol? LoopElement(IOperation operation, SemanticModel model) =>
        operation.Parent is IForEachLoopOperation loop && loop.Collection == operation && loop.Syntax is ForEachStatementSyntax syntax
            ? model.GetDeclaredSymbol(syntax) as ILocalSymbol
            : null;

    static bool IsSupportedIndexedSlotUse(IOperation slot, WolverineMethodScope scope) =>
        IsDirectAppendReceiver(slot, scope.Project) ||
        (slot.Parent is IVariableInitializerOperation { Parent: IVariableDeclaratorOperation variable } &&
         variable.Symbol.RefKind == RefKind.None && IsStable(variable.Symbol, scope));

    static bool IsDirectAppendReceiver(IOperation operation, DotNetProjectCompilation project) =>
        operation.Parent is IInvocationOperation invocation && invocation.Instance == operation &&
        WolverineEventStreams.IsExactAppend(invocation, project);

    static IOperation Outermost(IOperation operation)
    {
        while (operation.Parent is IConversionOperation or IParenthesizedOperation &&
               Unwrap(operation.Parent) == Unwrap(operation))
        {
            operation = operation.Parent;
        }

        return operation;
    }

    static bool IsDirect(SyntaxNode node, MethodDeclarationSyntax declaration) =>
        !node.Ancestors().TakeWhile(ancestor => ancestor != declaration).Any(ancestor => ancestor is LocalFunctionStatementSyntax or AnonymousFunctionExpressionSyntax);

    static IOperation? Unwrap(IOperation? operation) => operation switch
    {
        IConversionOperation conversion when IsTransparentConversion(conversion) => Unwrap(conversion.Operand),
        IParenthesizedOperation parenthesized => Unwrap(parenthesized.Operand),
        _ => operation
    };

    static bool IsTransparentConversion(IConversionOperation conversion) =>
        !conversion.Conversion.IsUserDefined &&
        !InvolvesObject(conversion) &&
        IsRepresentationPreserving(conversion);

    static bool InvolvesObject(IConversionOperation conversion) =>
        conversion.Type?.SpecialType == SpecialType.System_Object ||
        conversion.Operand.Type?.SpecialType == SpecialType.System_Object;

    static bool IsRepresentationPreserving(IConversionOperation conversion) =>
        conversion.Conversion.IsIdentity || conversion.Conversion.IsReference || conversion.Operand is ICollectionExpressionOperation;

    sealed record FetchSite(ILocalSymbol Local, IInvocationOperation Fetch, INamedTypeSymbol Model, IOperation Identities);

    sealed record FetchOrigin(IMethodSymbol Method, IParameterSymbol? Request, DotNetProjectCompilation Project);

    sealed record IdentityDistinctness(bool IsRepeated, bool IsUncomparable);

    sealed record StreamSlot(IOperation Identity, int? Index, bool IsCollection, bool IsEmpty);

    sealed record BindingSite(
        FetchSite Fetch,
        FetchOrigin Origin,
        string Site,
        SourceRange? Source,
        bool IsRepeated,
        bool IsUncomparable);
}
