// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Cratis.Screenplay.Generation;
using Cratis.Screenplay.Generation.DotNet;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Cratis.CritterStack.Screenplay.Marten;

sealed class MartenBackendOwnershipResolver(
    MartenStoreIndex stores,
    Wolverine.WolverineStorageRouting routing)
{
    public BackendOwnership ForMethod(DotNetProjectCompilation project, IMethodSymbol method)
    {
        if (!stores.HasExplicitScopes)
        {
            return BackendOwnership.Legacy;
        }

        var configured = AttributeOwnership(project, method.GetAttributes()) ??
                         TypeOwnership(project, method.ContainingType);
        return configured ?? routing.ForHandler(method.ContainingType) ?? stores.Default();
    }

    public BackendOwnership ForSaga(DotNetProjectCompilation project, IMethodSymbol method) =>
        AttributeOwnership(project, method.GetAttributes()) ?? TypeOwnership(project, method.ContainingType) ??
        routing.ForHandler(method.ContainingType) ?? BackendOwnership.Legacy;

    public BackendOwnership ForInvocation(DotNetProjectCompilation project, InvocationExpressionSyntax invocation, IMethodSymbol? owner = null)
    {
        if (!stores.HasExplicitScopes)
        {
            return BackendOwnership.Legacy;
        }

        var model = project.Compilation.GetSemanticModel(invocation.SyntaxTree);
        var receiver = ReceiverType(invocation, model);
        if (receiver is not null && MarkerFor(receiver) is { } marker)
        {
            return stores.ForMarker(project, marker);
        }

        return OptionsOrEnclosingOwnership(project, invocation, model, owner);
    }

    static INamedTypeSymbol? MarkerFor(INamedTypeSymbol type)
    {
        var candidates = type.AllInterfaces.Prepend(type);
        return candidates.Where(IsAncillarySession)
            .Select(candidate => candidate.TypeArguments[0]).OfType<INamedTypeSymbol>().FirstOrDefault();
    }

    static bool IsAncillarySession(INamedTypeSymbol candidate) =>
        candidate.ContainingNamespace.ToDisplayString() == "Marten.Ancillary" &&
        candidate.TypeArguments.Length == 1 &&
        (string.Equals(candidate.Name, "IDocumentSession", StringComparison.Ordinal) ||
         string.Equals(candidate.Name, "IQuerySession", StringComparison.Ordinal));

    static bool IsStoreAttribute(AttributeData attribute) =>
        attribute.AttributeClass is { } type &&
        (string.Equals(DotNetSubjectIds.MetadataName(type), "Wolverine.Marten.MartenStoreAttribute", StringComparison.Ordinal) ||
         string.Equals(DotNetSubjectIds.MetadataName(type), "Wolverine.Persistence.StorageAttribute", StringComparison.Ordinal));

    static INamedTypeSymbol? ReceiverType(InvocationExpressionSyntax invocation, SemanticModel model)
    {
        if (invocation.Expression is not MemberAccessExpressionSyntax access)
        {
            return null;
        }

        var receiver = access.Expression;
        while (receiver is MemberAccessExpressionSyntax member)
        {
            if (model.GetTypeInfo(receiver).Type is INamedTypeSymbol type && MarkerFor(type) is not null)
            {
                return type;
            }

            receiver = member.Expression;
        }

        return model.GetTypeInfo(receiver).Type as INamedTypeSymbol;
    }

    BackendOwnership OptionsOrEnclosingOwnership(
        DotNetProjectCompilation project,
        InvocationExpressionSyntax invocation,
        SemanticModel model,
        IMethodSymbol? owner)
    {
        var options = stores.ForOptions(project, invocation);
        if (options.Kind != BackendOwnershipKind.Unknown || MartenStoreOptionsBinding.IsOptionsReceiver(invocation, model))
        {
            return options;
        }

        return (owner ?? model.GetEnclosingSymbol(invocation.SpanStart)) is IMethodSymbol method
            ? ForMethod(project, method)
            : BackendOwnership.Unknown;
    }

    BackendOwnership? TypeOwnership(DotNetProjectCompilation project, INamedTypeSymbol? type)
    {
        while (type is not null)
        {
            if (AttributeOwnership(project, type.GetAttributes()) is { } selection)
            {
                return selection;
            }

            type = type.BaseType;
        }

        return null;
    }

    BackendOwnership? AttributeOwnership(DotNetProjectCompilation project, IEnumerable<AttributeData> attributes)
    {
        var selected = attributes.Where(IsStoreAttribute).ToArray();
        if (selected.Length == 0)
        {
            return null;
        }

        var ownership = selected.Select(attribute => attribute.ConstructorArguments.FirstOrDefault().Value is INamedTypeSymbol marker
            ? stores.ForMarker(project, marker)
            : BackendOwnership.Unknown).ToArray();
        return ownership.All(candidate => candidate == ownership[0]) ? ownership[0] : BackendOwnership.Conflict;
    }
}
