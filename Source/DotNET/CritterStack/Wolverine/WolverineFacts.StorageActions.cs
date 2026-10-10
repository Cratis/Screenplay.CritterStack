// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Cratis.Screenplay.Generation;
using Cratis.Screenplay.Generation.DotNet;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Cratis.CritterStack.Screenplay.Wolverine;

internal static partial class WolverineFacts
{
    static void AddStorageActionConsequences(
        WolverineFactScope scope,
        SubjectId sourceSubject,
        IMethodSymbol method,
        IReadOnlyList<WolverineReturnConsequence> consequences,
        Evidence evidence)
    {
        var artifactIds = new HashSet<string>(StringComparer.Ordinal);
        var factoryMethodsBySlot = StorageFactoryMethodsBySlot(method, scope.Project);
        foreach (var consequence in consequences)
        {
            if (StoredEntityType(consequence) is not { } entityType ||
                StorageActionRelationshipKind(consequence.Slot, entityType, factoryMethodsBySlot) is not { } relationshipKind)
            {
                continue;
            }

            var entitySubject = scope.Subjects.SubjectForType(scope.Project, entityType);
            AddStoredEntityArtifact(scope, artifactIds, entitySubject, entityType);
            scope.Facts.Add(Relationship(
                $"wolverine:stores:{sourceSubject.Value}:{entitySubject.Value}:{consequence.Slot}",
                sourceSubject,
                relationshipKind,
                entitySubject,
                evidence with
                {
                    Strength = EvidenceStrength.Exact,
                    Explanation = "Wolverine applies this returned storage action to the persistence layer"
                },
                discriminator: $"storage-action:{consequence.Slot}"));
        }
    }

    static INamedTypeSymbol? StoredEntityType(WolverineReturnConsequence consequence) =>
        consequence.Kind == WolverineReturnConsequenceKind.StorageAction
            ? consequence.EntityType as INamedTypeSymbol
            : null;

    static void AddStoredEntityArtifact(
        WolverineFactScope scope,
        HashSet<string> artifactIds,
        SubjectId entitySubject,
        INamedTypeSymbol entityType)
    {
        var artifactId = $"wolverine:read-model:{entitySubject.Value}";
        if (!artifactIds.Add(artifactId))
        {
            return;
        }

        scope.Facts.Add(Artifact(
            artifactId,
            new ArtifactKey { Subject = entitySubject, Kind = ArtifactKind.ReadModel },
            entityType.Name,
            SourceFileOf(entityType, scope.Project),
            DotNetTypeShapes.PropertiesOf(entityType),
            CritterStackSource.EvidenceFor(entityType, scope.Adapter, scope.Project, EvidenceStrength.Exact, "Wolverine persists this model through a returned storage action")));
    }

    static RelationshipKind? StorageActionRelationshipKind(
        int slot,
        ITypeSymbol entityType,
        Dictionary<int, List<IMethodSymbol?>> factoryMethodsBySlot)
    {
        if (!factoryMethodsBySlot.TryGetValue(slot, out var slotMethods))
        {
            return RelationshipKind.Stores;
        }

        var factoryMethods = slotMethods
            .OfType<IMethodSymbol>()
            .Where(_ => _.TypeArguments.Any(typeArgument => SymbolEqualityComparer.Default.Equals(typeArgument, entityType)))
            .ToArray();
        if (slotMethods.Exists(_ => _ is null) || factoryMethods.Length != slotMethods.Count)
        {
            return RelationshipKind.Stores;
        }

        var kinds = factoryMethods
            .Select(FactoryRelationshipKind)
            .Where(_ => _ is not null)
            .Distinct()
            .ToArray();
        return kinds.Length switch
        {
            0 => null,
            1 => kinds[0],
            _ => RelationshipKind.Stores
        };
    }

    static RelationshipKind? FactoryRelationshipKind(IMethodSymbol factoryMethod) => factoryMethod.Name switch
    {
        "Delete" => RelationshipKind.Deletes,
        "Update" => RelationshipKind.Updates,
        "Nothing" or "StartStream" => null,
        _ => RelationshipKind.Stores
    };

    static Dictionary<int, List<IMethodSymbol?>> StorageFactoryMethodsBySlot(
        IMethodSymbol method,
        DotNetProjectCompilation project)
    {
        var methodsBySlot = new Dictionary<int, List<IMethodSymbol?>>();
        foreach (var (declaration, semanticModel) in WolverineMethodSyntax.Declarations(method, project))
        {
            foreach (var returned in ReturnedExpressions(declaration))
            {
                AddSlotFactoryMethods(methodsBySlot, UnwrapReturnedExpression(returned, semanticModel), semanticModel);
            }
        }

        return methodsBySlot;
    }

    static void AddSlotFactoryMethods(
        Dictionary<int, List<IMethodSymbol?>> methodsBySlot,
        ExpressionSyntax expression,
        SemanticModel semanticModel)
    {
        var slotExpressions = expression is TupleExpressionSyntax tuple
            ? tuple.Arguments.Select(_ => _.Expression).ToArray()
            : [expression];
        for (var slot = 0; slot < slotExpressions.Length; slot++)
        {
            if (!methodsBySlot.TryGetValue(slot, out var methods))
            {
                methods = [];
                methodsBySlot.Add(slot, methods);
            }

            methods.Add(StorageFactoryMethod(slotExpressions[slot], semanticModel, []));
        }
    }

    static IEnumerable<ExpressionSyntax> ReturnedExpressions(MethodDeclarationSyntax declaration)
    {
        if (declaration.ExpressionBody?.Expression is { } expressionBody)
        {
            yield return expressionBody;
        }

        if (declaration.Body is null)
        {
            yield break;
        }

        foreach (var returnStatement in declaration.Body.DescendantNodes().OfType<ReturnStatementSyntax>())
        {
            var nestedExecutable = returnStatement.Ancestors()
                .TakeWhile(_ => !ReferenceEquals(_, declaration))
                .Any(_ => _ is LocalFunctionStatementSyntax or AnonymousFunctionExpressionSyntax);
            if (!nestedExecutable && returnStatement.Expression is { } expression)
            {
                yield return expression;
            }
        }
    }

    static ExpressionSyntax UnwrapReturnedExpression(ExpressionSyntax expression, SemanticModel semanticModel)
    {
        while (true)
        {
            expression = expression switch
            {
                ParenthesizedExpressionSyntax parenthesized => parenthesized.Expression,
                AwaitExpressionSyntax awaited => awaited.Expression,
                CastExpressionSyntax cast => cast.Expression,
                _ => expression
            };

            if (TaskFromResultArgument(expression, semanticModel) is not { } result)
            {
                return expression;
            }

            expression = result;
        }
    }

    static ExpressionSyntax? TaskFromResultArgument(ExpressionSyntax expression, SemanticModel semanticModel) =>
        expression is InvocationExpressionSyntax invocation &&
        semanticModel.GetSymbolInfo(invocation).Symbol is IMethodSymbol taskFactory &&
        IsTaskFromResult(taskFactory)
            ? invocation.ArgumentList.Arguments.FirstOrDefault()?.Expression
            : null;

    static bool IsTaskFromResult(IMethodSymbol method) =>
        string.Equals(method.Name, "FromResult", StringComparison.Ordinal) &&
        method.ContainingType is { } containingType &&
        DotNetSubjectIds.MetadataName(containingType.OriginalDefinition) is { } taskType &&
        (string.Equals(taskType, "System.Threading.Tasks.Task", StringComparison.Ordinal) ||
         string.Equals(taskType, "System.Threading.Tasks.ValueTask", StringComparison.Ordinal));

    static IMethodSymbol? StorageFactoryMethod(
        ExpressionSyntax expression,
        SemanticModel semanticModel,
        HashSet<ILocalSymbol> visitedLocals)
    {
        expression = UnwrapReturnedExpression(expression, semanticModel);
        if (StorageFactoryInvocation(expression, semanticModel) is { } invoked)
        {
            return invoked;
        }

        return LocalInitializer(expression, semanticModel, visitedLocals) is { } initializer
            ? StorageFactoryMethod(initializer, semanticModel, visitedLocals)
            : null;
    }

    static IMethodSymbol? StorageFactoryInvocation(ExpressionSyntax expression, SemanticModel semanticModel) =>
        expression is InvocationExpressionSyntax invocation &&
        semanticModel.GetSymbolInfo(invocation).Symbol is IMethodSymbol invoked &&
        invoked.IsStatic &&
        DotNetSubjectIds.MetadataName(invoked.ContainingType.OriginalDefinition) == WellKnownTypes.WolverineStorageFactory
            ? invoked
            : null;

    static ExpressionSyntax? LocalInitializer(
        ExpressionSyntax expression,
        SemanticModel semanticModel,
        HashSet<ILocalSymbol> visitedLocals)
    {
        if (semanticModel.GetSymbolInfo(expression).Symbol is not ILocalSymbol local || !visitedLocals.Add(local))
        {
            return null;
        }

        return local.DeclaringSyntaxReferences
            .Select(_ => _.GetSyntax())
            .OfType<VariableDeclaratorSyntax>()
            .Select(_ => _.Initializer?.Value)
            .OfType<ExpressionSyntax>()
            .FirstOrDefault();
    }
}
