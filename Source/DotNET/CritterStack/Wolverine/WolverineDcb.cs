// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Cratis.Screenplay.Generation;
using Cratis.Screenplay.Generation.DotNet;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Cratis.CritterStack.Screenplay.Wolverine;

sealed record WolverineDcbCondition(
    int Ordinal,
    INamedTypeSymbol TagType,
    INamedTypeSymbol? EventType,
    string? SourceMember,
    SourceRange? Source);

sealed record WolverineDcbDiscovery(
    IParameterSymbol Parameter,
    INamedTypeSymbol ModelType,
    bool IsBoundaryParameter,
    IMethodSymbol Companion,
    IReadOnlyList<WolverineDcbCondition> Conditions,
    bool QueryResolved,
    IReadOnlyList<INamedTypeSymbol> EventTypes,
    IReadOnlyList<INamedTypeSymbol> ImperativeEventTypes,
    IReadOnlyList<INamedTypeSymbol> QueryEventTypes,
    string? SourceMember,
    SourceRange? Source,
    SourceRange? QuerySource)
{
    public string Discriminator => $"wolverine:dcb:{Parameter.Ordinal}:{Parameter.Name}";
}

static partial class WolverineDcb
{
    static readonly string[] _companionNames = ["Before", "BeforeAsync", "Load", "LoadAsync"];

    static readonly HashSet<string> _persistenceWrappers =
    [
        WellKnownTypes.WolverineEvents,
        WellKnownTypes.WolverineEventsToAppend
    ];

    public static WolverineDcbDiscovery? Discover(
        IMethodSymbol method,
        IParameterSymbol? request,
        DotNetProjectCompilation project,
        bool isHttpEndpoint)
    {
        var attributed = method.Parameters
            .Select(parameter => (Parameter: parameter, Attribute: DcbAttribute(parameter, project)))
            .Where(_ => _.Attribute is not null)
            .ToArray();
        if (attributed.Length != 1)
        {
            return null;
        }

        var (parameter, attribute) = attributed[0];
        if (!TryGetModel(parameter.Type, project, out var modelType, out var isBoundaryParameter))
        {
            return null;
        }

        var companion = Companions(method, request, project).FirstOrDefault();
        if (companion is null)
        {
            return null;
        }

        var query = ParseQuery(companion, request, method, project);
        var (eventTypes, imperativeEventTypes) = DiscoverEvents(method, isBoundaryParameter, project, isHttpEndpoint);
        var queryEventTypes = query.Conditions
            .Select(_ => _.EventType)
            .Where(type => type is not null && IsAuthoredEventPayload(type, project))
            .Distinct(SymbolEqualityComparer.Default)
            .OfType<INamedTypeSymbol>()
            .ToArray();
        return new(
            parameter,
            modelType,
            isBoundaryParameter,
            companion,
            query.Conditions,
            query.Resolved,
            eventTypes,
            imperativeEventTypes,
            queryEventTypes,
            query.SourceMember,
            SourceOf(attribute, parameter, project),
            query.Source ?? SourceOf(companion, project));
    }

    public static bool HasAttributedParameter(IParameterSymbol parameter, DotNetProjectCompilation project) =>
        DcbAttribute(parameter, project) is not null;

    static AttributeData? DcbAttribute(IParameterSymbol parameter, DotNetProjectCompilation project) =>
        parameter.GetAttributes().FirstOrDefault(attribute =>
            attribute.AttributeClass is not null &&
            IsAdmittedAttribute(attribute.AttributeClass, project) &&
            attribute.ApplicationSyntaxReference?.SyntaxTree is { } tree &&
            project.AuthoredSyntaxTrees.Contains(tree) &&
            !DotNetGeneratedSource.IsGenerated(tree));

    static bool IsAdmittedAttribute(INamedTypeSymbol attributeType, DotNetProjectCompilation project) =>
        IsCurrentDcbModelAttribute(attributeType, project) || IsLegacyBoundaryModelAttribute(attributeType, project);

    static bool IsCurrentDcbModelAttribute(INamedTypeSymbol attributeType, DotNetProjectCompilation project) =>
        project.Compilation.GetTypeByMetadataName(WellKnownTypes.WolverineDcbModelAttribute) is { } current &&
        WolverineSymbolAuthority.IsAuthoredOrMetadataSymbol(current, project) &&
        DotNetSymbols.IsOrInheritsFrom(attributeType, WellKnownTypes.WolverineDcbModelAttribute) &&
        IsAuthoredOrMetadataInheritance(attributeType, WellKnownTypes.WolverineDcbModelAttribute, project);

    static bool IsLegacyBoundaryModelAttribute(INamedTypeSymbol attributeType, DotNetProjectCompilation project) =>
        project.Compilation.GetTypeByMetadataName(WellKnownTypes.WolverineLegacyBoundaryModelAttribute) is { } legacy &&
        WolverineSymbolAuthority.IsAuthoredOrMetadataSymbol(legacy, project) &&
        DotNetSubjectIds.MetadataName(attributeType.OriginalDefinition) == WellKnownTypes.WolverineLegacyBoundaryModelAttribute;

    static bool IsAuthoredOrMetadataInheritance(
        INamedTypeSymbol attributeType,
        string admittedMetadataName,
        DotNetProjectCompilation project) => IsAuthoredOrMetadataInheritance(
            attributeType,
            admittedMetadataName,
            project,
            [with(SymbolEqualityComparer.Default)]);

    static bool IsAuthoredOrMetadataInheritance(
        INamedTypeSymbol attributeType,
        string admittedMetadataName,
        DotNetProjectCompilation project,
        HashSet<INamedTypeSymbol> visited)
    {
        if (!visited.Add(attributeType))
        {
            return false;
        }

        if (DotNetSubjectIds.MetadataName(attributeType.OriginalDefinition) == admittedMetadataName)
        {
            return true;
        }

        if (attributeType.DeclaringSyntaxReferences.Length == 0)
        {
            return attributeType.BaseType is not null &&
                   IsAuthoredOrMetadataInheritance(attributeType.BaseType, admittedMetadataName, project, visited);
        }

        return attributeType.DeclaringSyntaxReferences.Any(reference =>
            AuthoredBaseList(reference, project) is { } baseList &&
            BaseTypesInherit(baseList, admittedMetadataName, project, visited));
    }

    static BaseListSyntax? AuthoredBaseList(SyntaxReference reference, DotNetProjectCompilation project) =>
        reference.GetSyntax() is TypeDeclarationSyntax { BaseList: { } baseList } declaration &&
        project.AuthoredSyntaxTrees.Contains(declaration.SyntaxTree) &&
        !DotNetGeneratedSource.IsGenerated(declaration.SyntaxTree)
            ? baseList
            : null;

    static bool BaseTypesInherit(
        BaseListSyntax baseList,
        string admittedMetadataName,
        DotNetProjectCompilation project,
        HashSet<INamedTypeSymbol> visited)
    {
        var semanticModel = project.Compilation.GetSemanticModel(baseList.SyntaxTree);
        return baseList.Types.Any(baseType =>
            semanticModel.GetTypeInfo(baseType.Type).Type is INamedTypeSymbol candidate &&
            IsAuthoredOrMetadataInheritance(candidate, admittedMetadataName, project, visited));
    }

    static bool TryGetModel(
        ITypeSymbol parameterType,
        DotNetProjectCompilation project,
        out INamedTypeSymbol modelType,
        out bool isBoundaryParameter)
    {
        modelType = null!;
        isBoundaryParameter = false;
        if (parameterType is INamedTypeSymbol { IsGenericType: true } boundary && IsEventBoundaryDefinition(boundary))
        {
            if (BoundaryModel(boundary, project) is not { } boundaryModel)
            {
                return false;
            }

            modelType = boundaryModel;
            isBoundaryParameter = true;
            return true;
        }

        if (ImplementsEventBoundary(parameterType) || PlainModel(parameterType, project) is not { } plainModel)
        {
            return false;
        }

        modelType = plainModel;
        return true;
    }

    static bool IsEventBoundaryDefinition(INamedTypeSymbol type) =>
        DotNetSubjectIds.MetadataName(type.OriginalDefinition) == WellKnownTypes.JasperFxEventBoundary;

    static bool ImplementsEventBoundary(ITypeSymbol type) =>
        type is INamedTypeSymbol named && named.AllInterfaces.Any(IsEventBoundaryDefinition);

    static INamedTypeSymbol? BoundaryModel(INamedTypeSymbol boundary, DotNetProjectCompilation project) =>
        CanonicalEventBoundary(project) is { } canonical &&
        SymbolEqualityComparer.Default.Equals(boundary.OriginalDefinition, canonical) &&
        boundary.TypeArguments[0] is INamedTypeSymbol model &&
        IsAuthoredSourceType(model, project)
            ? model
            : null;

    static INamedTypeSymbol? CanonicalEventBoundary(DotNetProjectCompilation project) =>
        project.Compilation.GetTypeByMetadataName(WellKnownTypes.JasperFxEventBoundary) is { } canonical &&
        WolverineSymbolAuthority.IsAuthoredOrMetadataSymbol(canonical, project)
            ? canonical
            : null;

    static INamedTypeSymbol? PlainModel(ITypeSymbol parameterType, DotNetProjectCompilation project)
    {
        var candidate = parameterType as INamedTypeSymbol;
        if (candidate?.OriginalDefinition.SpecialType == SpecialType.System_Nullable_T)
        {
            candidate = candidate.TypeArguments[0] as INamedTypeSymbol;
        }

        return candidate is not null && IsAuthoredCustomType(candidate, project) ? candidate : null;
    }

    static bool IsAuthoredCustomType(INamedTypeSymbol type, DotNetProjectCompilation project) =>
        type.SpecialType == SpecialType.None &&
        !DotNetSubjectIds.MetadataName(type.OriginalDefinition).StartsWith("System.", StringComparison.Ordinal) &&
        IsAuthoredSourceType(type, project);

    static IEnumerable<IMethodSymbol> Companions(
        IMethodSymbol handler,
        IParameterSymbol? request,
        DotNetProjectCompilation project) =>
        handler.ContainingType.GetMembers()
            .OfType<IMethodSymbol>()
            .Where(method => IsCompanionCandidate(method, handler, request, project))
            .OrderBy(method => SourcePathOf(method, project), StringComparer.Ordinal)
            .ThenBy(method => SourcePositionOf(method, project));

    static bool IsCompanionCandidate(
        IMethodSymbol method,
        IMethodSymbol handler,
        IParameterSymbol? request,
        DotNetProjectCompilation project) =>
        method.DeclaredAccessibility == Accessibility.Public &&
        method.MethodKind == MethodKind.Ordinary &&
        _companionNames.Contains(method.Name, StringComparer.Ordinal) &&
        IsExactQueryReturn(method.ReturnType) &&
        ParametersCorrelate(method, handler, request) &&
        WolverineMethodSyntax.Declarations(method, project).Any();

    static bool ParametersCorrelate(
        IMethodSymbol companion,
        IMethodSymbol handler,
        IParameterSymbol? request)
    {
        if (request is null)
        {
            return false;
        }

        var companionRequests = RequestParameters(companion, request);
        if (companionRequests.Length != 1)
        {
            return false;
        }

        return RequestParameters(handler, request).Length == 1 ||
               string.Equals(companionRequests[0].Name, request.Name, StringComparison.Ordinal);
    }

    static IParameterSymbol[] RequestParameters(IMethodSymbol method, IParameterSymbol request) =>
        [.. method.Parameters.Where(parameter => SymbolEqualityComparer.Default.Equals(parameter.Type, request.Type))];

    static bool IsExactQueryReturn(ITypeSymbol returnType)
    {
        if (returnType is not INamedTypeSymbol named)
        {
            return false;
        }

        var metadataName = DotNetSubjectIds.MetadataName(named.OriginalDefinition);
        if (metadataName == WellKnownTypes.JasperFxEventTagQuery)
        {
            return true;
        }

        return (string.Equals(metadataName, "System.Threading.Tasks.Task`1", StringComparison.Ordinal) ||
                string.Equals(metadataName, "System.Threading.Tasks.ValueTask`1", StringComparison.Ordinal)) &&
               named.TypeArguments[0] is INamedTypeSymbol result &&
               DotNetSubjectIds.MetadataName(result.OriginalDefinition) == WellKnownTypes.JasperFxEventTagQuery;
    }
}
