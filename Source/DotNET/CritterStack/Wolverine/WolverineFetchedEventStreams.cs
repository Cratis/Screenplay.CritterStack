// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Globalization;

using Cratis.Screenplay.Generation;
using Cratis.Screenplay.Generation.DotNet;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Operations;

namespace Cratis.CritterStack.Screenplay.Wolverine;

sealed record WolverineFetchedStreamBinding(
    ILocalSymbol Local,
    string Site,
    int? Index,
    bool IsCollection,
    bool IsEmpty,
    bool HasRepeatedIdentity,
    string IdentityExpression);

static class WolverineFetchedEventStreams
{
    public static IReadOnlyList<WolverineStateBinding> Bindings(IMethodSymbol method, INamedTypeSymbol? requestType, DotNetProjectCompilation project)
    {
        var request = method.Parameters.FirstOrDefault(parameter => SymbolEqualityComparer.Default.Equals(parameter.Type, requestType));
        var bindings = new List<WolverineStateBinding>();
        foreach (var (declaration, semanticModel) in WolverineMethodSyntax.Declarations(method, project))
        {
            foreach (var variable in declaration.DescendantNodes().OfType<VariableDeclaratorSyntax>())
            {
                if (!IsDirect(variable, declaration) ||
                    variable.Initializer is null ||
                    semanticModel.GetDeclaredSymbol(variable) is not ILocalSymbol local ||
                    Unwrap(semanticModel.GetOperation(variable.Initializer.Value)) is not IAwaitOperation awaited ||
                    FetchInvocation(awaited.Operation) is not { } fetch ||
                    !TryModel(fetch.TargetMethod, project, out var model) ||
                    fetch.Arguments.FirstOrDefault(argument => argument.Parameter?.Ordinal == 0)?.Value is not { } identities)
                {
                    continue;
                }

                var source = CritterStackSource.RangeForProject(fetch.Syntax.GetLocation(), project);
                var site = $"{DotNetMethodIdentity.SubjectFor(project, method).Value}:{source?.Path}:{fetch.Syntax.SpanStart.ToString(CultureInfo.InvariantCulture)}";
                var input = Unwrap(identities)!;
                var elements = Elements(input);
                var repeated = elements is not null && elements.Select(IdentityKey).Distinct(StringComparer.Ordinal).Count() != elements.Count;
                if (elements is { Count: > 0 })
                {
                    for (var index = 0; index < elements.Count; index++)
                    {
                        Add(elements[index], index, isCollection: false, isEmpty: false);
                    }
                }
                else
                {
                    Add(input, null, isCollection: elements is null, isEmpty: elements is { Count: 0 });
                }

                void Add(IOperation identity, int? index, bool isCollection, bool isEmpty)
                {
                    var member = IdentityMember(identity, method, request, project);
                    bindings.Add(new WolverineStateBinding(
                        DotNetMethodIdentity.SubjectFor(project, method).Value,
                        null,
                        model,
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
                        Fetched = new(local, site, index, isCollection, isEmpty, repeated, identity.Syntax.ToString())
                    });
                }
            }
        }

        return bindings;
    }

    public static IReadOnlyList<WolverineStateBinding> Targets(
        IOperation receiver,
        InvocationExpressionSyntax append,
        MethodDeclarationSyntax declaration,
        SemanticModel semanticModel,
        IReadOnlyList<WolverineStateBinding> bindings,
        DotNetProjectCompilation project)
    {
        var fetched = bindings.Where(binding => binding.Fetched is { IsEmpty: false, HasRepeatedIdentity: false }).ToArray();
        var indexed = IndexedTarget(Unwrap(receiver), fetched, declaration, semanticModel, project);
        if (indexed is not null)
        {
            return [indexed];
        }

        if (Unwrap(receiver) is not ILocalReferenceOperation local || !IsStable(local.Local, declaration, semanticModel, project))
        {
            return [];
        }

        var initializer = local.Local.DeclaringSyntaxReferences.Select(reference => reference.GetSyntax())
            .OfType<VariableDeclaratorSyntax>().SingleOrDefault()?.Initializer?.Value;
        if (initializer is not null && IndexedTarget(Unwrap(semanticModel.GetOperation(initializer)), fetched, declaration, semanticModel, project) is { } fromIndex)
        {
            return [fromIndex];
        }

        foreach (var loop in append.Ancestors().TakeWhile(node => node != declaration).OfType<ForEachStatementSyntax>())
        {
            if (!SymbolEqualityComparer.Default.Equals(semanticModel.GetDeclaredSymbol(loop), local.Local) ||
                Unwrap(semanticModel.GetOperation(loop.Expression)) is not ILocalReferenceOperation collection ||
                !IsStable(collection.Local, declaration, semanticModel, project))
            {
                continue;
            }

            return [.. fetched.Where(binding => SymbolEqualityComparer.Default.Equals(binding.Fetched!.Local, collection.Local))];
        }

        return [];
    }

    static WolverineStateBinding? IndexedTarget(
        IOperation? operation,
        IReadOnlyList<WolverineStateBinding> bindings,
        MethodDeclarationSyntax declaration,
        SemanticModel semanticModel,
        DotNetProjectCompilation project)
    {
        if (operation is not IPropertyReferenceOperation { Property.IsIndexer: true, Arguments.Length: 1 } indexer ||
            Unwrap(indexer.Instance) is not ILocalReferenceOperation collection ||
            indexer.Arguments[0].Value.ConstantValue is not { HasValue: true, Value: int index } ||
            !IsStable(collection.Local, declaration, semanticModel, project))
        {
            return null;
        }

        return bindings.FirstOrDefault(binding => binding.Fetched is { IsCollection: false } fetched &&
            fetched.Index == index && SymbolEqualityComparer.Default.Equals(fetched.Local, collection.Local));
    }

    static IInvocationOperation? FetchInvocation(IOperation operation)
    {
        var invocation = Unwrap(operation) as IInvocationOperation;
        if (invocation?.TargetMethod is { IsStatic: false, Parameters.Length: 1 } configure &&
            string.Equals(configure.Name, "ConfigureAwait", StringComparison.Ordinal) &&
            DotNetSubjectIds.MetadataName(configure.ContainingType.OriginalDefinition) == "System.Threading.Tasks.Task`1" &&
            configure.Parameters[0].Type.SpecialType == SpecialType.System_Boolean)
        {
            invocation = Unwrap(invocation.Instance) as IInvocationOperation;
        }

        return invocation;
    }

    static bool TryModel(IMethodSymbol method, DotNetProjectCompilation project, out INamedTypeSymbol model)
    {
        model = null!;
        if (method.Name != "FetchManyForWriting" || method.IsStatic || method.ReducedFrom is not null ||
            method.Arity != 1 || method.TypeArguments[0] is not INamedTypeSymbol modelType || modelType.TypeKind == TypeKind.Error ||
            project.Compilation.GetTypeByMetadataName(WellKnownTypes.JasperFxEventStoreOperations) is not { } contract ||
            !WolverineSymbolAuthority.IsAuthoredOrMetadataSymbol(contract, project) ||
            !WolverineSymbolAuthority.IsAuthoredOrMetadataSymbol(method.OriginalDefinition, project) ||
            !SymbolEqualityComparer.Default.Equals(method.ContainingType, contract) ||
            method.Parameters is not [{ Type: INamedTypeSymbol ids }, { Type: INamedTypeSymbol cancellation }] ||
            DotNetSubjectIds.MetadataName(ids.OriginalDefinition) != "System.Collections.Generic.IReadOnlyList`1" ||
            ids.TypeArguments[0] is not INamedTypeSymbol id ||
            (id.SpecialType != SpecialType.System_String && DotNetSubjectIds.MetadataName(id) != "System.Guid") ||
            DotNetSubjectIds.MetadataName(cancellation) != "System.Threading.CancellationToken" ||
            method.ReturnType is not INamedTypeSymbol task ||
            DotNetSubjectIds.MetadataName(task.OriginalDefinition) != "System.Threading.Tasks.Task`1" ||
            task.TypeArguments[0] is not INamedTypeSymbol list ||
            DotNetSubjectIds.MetadataName(list.OriginalDefinition) != "System.Collections.Generic.IReadOnlyList`1" ||
            list.TypeArguments[0] is not INamedTypeSymbol stream ||
            DotNetSubjectIds.MetadataName(stream.OriginalDefinition) != WellKnownTypes.JasperFxEventStream ||
            !WolverineSymbolAuthority.IsAuthoredOrMetadataSymbol(stream.OriginalDefinition, project) ||
            !SymbolEqualityComparer.Default.Equals(stream.TypeArguments[0], modelType))
        {
            return false;
        }

        model = modelType;
        return true;
    }

    static IReadOnlyList<IOperation>? Elements(IOperation input) => input switch
    {
        IArrayCreationOperation { Initializer: { } initializer } => initializer.ElementValues,
        IArrayCreationOperation { DimensionSizes: [{ ConstantValue: { HasValue: true, Value: 0 } }] } => [],
        IInvocationOperation { TargetMethod: { Name: "Empty", IsStatic: true, Arity: 1, Parameters.Length: 0 } empty } when
            empty.ContainingType.SpecialType == SpecialType.System_Array => [],
        ICollectionExpressionOperation collection when !collection.Elements.Any(element => element is ISpreadOperation) => collection.Elements,
        _ => null
    };

    static ISymbol? IdentityMember(IOperation operation, IMethodSymbol method, IParameterSymbol? request, DotNetProjectCompilation project) => Unwrap(operation) switch
    {
        IPropertyReferenceOperation property when
            Unwrap(property.Instance) is IParameterReferenceOperation parameter &&
            SymbolEqualityComparer.Default.Equals(parameter.Parameter, request) &&
            WolverineSymbolAuthority.IsAuthoredOrMetadataSymbol(property.Property, project) => property.Property,
        IParameterReferenceOperation parameter when request is null && SymbolEqualityComparer.Default.Equals(parameter.Parameter.ContainingSymbol, method) => parameter.Parameter,
        _ => null
    };

    static string IdentityKey(IOperation operation)
    {
        operation = Unwrap(operation)!;
        if (operation.ConstantValue.HasValue)
        {
            return $"constant:{operation.Type}:{operation.ConstantValue.Value}";
        }

        return operation switch
        {
            // A field read is the same identity wherever it appears, so key it by symbol (and receiver for
            // instance fields) rather than by position; otherwise `[Guid.Empty, Guid.Empty]` looks distinct.
            IFieldReferenceOperation { Field.IsStatic: true } field =>
                $"field:{field.Field.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat)}",
            IFieldReferenceOperation field =>
                $"field:{FieldReceiverKey(field.Instance)}.{field.Field.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat)}",
            IPropertyReferenceOperation or IParameterReferenceOperation or ILocalReferenceOperation =>
                operation.Syntax.WithoutTrivia().ToString(),
            _ => $"expression:{operation.Syntax.SpanStart.ToString(CultureInfo.InvariantCulture)}"
        };
    }

    static string FieldReceiverKey(IOperation? receiver) => Unwrap(receiver) switch
    {
        IInstanceReferenceOperation => "this",
        { } instance => $"({IdentityKey(instance)})",
        null => "?"
    };

    static bool IsStable(ILocalSymbol local, MethodDeclarationSyntax declaration, SemanticModel model, DotNetProjectCompilation project) =>
        declaration.DescendantNodes().OfType<IdentifierNameSyntax>()
            .Where(identifier => SymbolEqualityComparer.Default.Equals(model.GetSymbolInfo(identifier).Symbol, local))
            .All(identifier => IsDirect(identifier, declaration) &&
                model.GetOperation(identifier) is { } reference && IsSupportedUse(reference, declaration, model, project));

    static bool IsSupportedUse(IOperation operation, MethodDeclarationSyntax declaration, SemanticModel model, DotNetProjectCompilation project)
    {
        operation = Outermost(operation);
        if (IsDirectAppendReceiver(operation, project))
        {
            return true;
        }

        if (operation.Parent is IPropertyReferenceOperation { Property.IsIndexer: true, Arguments: [{ Value.ConstantValue: { HasValue: true, Value: int } }] } indexer &&
            indexer.Instance == operation)
        {
            var slot = Outermost(indexer);
            return IsDirectAppendReceiver(slot, project) ||
                (slot.Parent is IVariableInitializerOperation { Parent: IVariableDeclaratorOperation variable } &&
                 variable.Symbol.RefKind == RefKind.None && IsStable(variable.Symbol, declaration, model, project));
        }

        if (operation.Parent is IForEachLoopOperation loop && loop.Collection == operation &&
            loop.Syntax is ForEachStatementSyntax syntax && model.GetDeclaredSymbol(syntax) is ILocalSymbol element)
        {
            return IsStable(element, declaration, model, project);
        }

        // Unknown reads can alias the stream just as writes can. In particular, extension
        // receivers, arguments, captures and non-append member access are not safe uses.
        return false;
    }

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
        IConversionOperation conversion when !conversion.Conversion.IsUserDefined &&
            conversion.Type?.SpecialType != SpecialType.System_Object &&
            conversion.Operand.Type?.SpecialType != SpecialType.System_Object &&
            (conversion.Conversion.IsIdentity || conversion.Conversion.IsReference || conversion.Operand is ICollectionExpressionOperation) => Unwrap(conversion.Operand),
        IParenthesizedOperation parenthesized => Unwrap(parenthesized.Operand),
        _ => operation
    };
}
