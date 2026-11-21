// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Cratis.Screenplay.Generation;
using Cratis.Screenplay.Generation.DotNet;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Cratis.CritterStack.Screenplay.Marten;

internal sealed partial class MartenEventSchemaConfigurationDiscovery(
    DotNetProjectCompilation project,
    CritterStackSubjectResolver subjects,
    SemanticModel semanticModel,
    List<GenerationDiagnostic> diagnostics)
{
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

    static bool IsEventStoreOptionsMethod(IMethodSymbol candidate, string name) =>
        candidate.Name == name &&
        DotNetSubjectIds.MetadataName(candidate.ContainingType.OriginalDefinition) == WellKnownTypes.MartenEventStoreOptions;

    static bool IsEventStoreOptionsExtension(IMethodSymbol candidate) =>
        candidate.IsExtensionMethod &&
        DotNetSubjectIds.MetadataName(candidate.ContainingType.OriginalDefinition) == WellKnownTypes.MartenEventStoreOptionsExtensions &&
        HasEventStoreReceiver(candidate);

    static bool HasParameters(IMethodSymbol candidate, params Func<ITypeSymbol, bool>?[] expected) =>
        candidate.Parameters.Length == expected.Length &&
        expected.Select((check, index) => check is null || check(candidate.Parameters[index].Type)).All(_ => _);

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

    bool TryOptionalBaseAlias(InvocationExpressionSyntax invocation, IMethodSymbol method, out string? alias)
    {
        var argument = ArgumentForParameter(invocation, method, "eventTypeName");
        alias = argument is null ? null : ConstantString(argument.Expression, semanticModel);
        return argument is null || alias is not null;
    }

    GenerationDiagnostic Configuration(string code, ConfigurationSite site) => new()
    {
        Code = code,
        Severity = GenerationDiagnosticSeverity.Warning,
        Outcome = site.Outcome,
        Message = site.Message,
        Source = CritterStackSource.RangeForProject(site.Location, project),
        Subject = site.SubjectType is null ? ProjectSubject(project) : subjects.SubjectForType(project, site.SubjectType)
    };

    sealed record ConfigurationSite(
        INamedTypeSymbol? SubjectType,
        string Message,
        Location Location,
        GenerationDiagnosticOutcome Outcome);
}
