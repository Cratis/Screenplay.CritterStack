// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Cratis.Screenplay.Generation;
using Cratis.Screenplay.Generation.DotNet;
using Microsoft.CodeAnalysis;

namespace Cratis.CritterStack.Screenplay.Wolverine;

internal static partial class WolverinePolicyDiscovery
{
    static readonly HashSet<string> _endpointPolicies =
    [
        WellKnownTypes.WolverineDeduplicatedWithResponseAttribute,
        WellKnownTypes.WolverineAlwaysPublishResponseAttribute,
        WellKnownTypes.WolverineTopicAttribute
    ];

    public static IReadOnlyList<GenerationDiagnostic> Discover(
        DotNetProjectCompilation project,
        CritterStackSubjectResolver subjects,
        WolverineMiddlewareRegistrations registrations)
    {
        var diagnostics = new List<GenerationDiagnostic>();
        foreach (var type in new DotNetArtifactCatalog(project.Compilation).Types.Where(_ => IsAuthored(_, project)))
        {
            foreach (var owner in new ISymbol[] { type }.Concat(type.GetMembers().OfType<IMethodSymbol>()))
            {
                AddPolicyDiagnostics(project, subjects.SubjectForType(project, type), owner, diagnostics);
                AddMiddlewareAttributeDiagnostics(project, subjects.SubjectForType(project, type), owner, diagnostics);
            }
        }

        foreach (var registration in registrations.Unresolved)
        {
            diagnostics.Add(Unresolved(
                registration.Source,
                subjects.SubjectForType(project, registration.Owner),
                "AddMiddleware registration has a runtime filter, conditional execution, or a middleware type that cannot be resolved as authored source"));
        }

        return diagnostics;
    }

    public static SourceRange? AttributeSource(AttributeData attribute, DotNetProjectCompilation project) =>
        attribute.ApplicationSyntaxReference is { } syntax
            ? CritterStackSource.RangeForProject(syntax.GetSyntax().GetLocation(), project)
            : null;

    static void AddPolicyDiagnostics(
        DotNetProjectCompilation project,
        SubjectId subject,
        ISymbol owner,
        List<GenerationDiagnostic> diagnostics)
    {
        foreach (var attribute in owner.GetAttributes().Where(_ => _.AttributeClass is { } type &&
                     _endpointPolicies.Contains(DotNetSubjectIds.MetadataName(type))))
        {
            diagnostics.Add(new()
            {
                Code = WolverineDiagnosticCodes.EndpointIdempotencyOmitted,
                Severity = GenerationDiagnosticSeverity.Warning,
                Outcome = GenerationDiagnosticOutcome.Unsupported,
                Message = $"Wolverine policy '{attribute.AttributeClass!.Name}' controls response deduplication, publication, or routing; this behavior is not represented",
                Source = AttributeSource(attribute, project),
                Subject = subject
            });
        }
    }

    static void AddMiddlewareAttributeDiagnostics(
        DotNetProjectCompilation project,
        SubjectId subject,
        ISymbol owner,
        List<GenerationDiagnostic> diagnostics)
    {
        foreach (var attribute in MiddlewareAttributes(owner))
        {
            if (!HasResolvedMiddlewareTypes(attribute, project))
            {
                diagnostics.Add(Unresolved(
                    AttributeSource(attribute, project),
                    subject,
                    "Middleware attribute scope or type cannot be resolved as authored source"));
            }
        }
    }

    static GenerationDiagnostic Unresolved(SourceRange? source, SubjectId subject, string reason) => new()
    {
        Code = WolverineDiagnosticCodes.HandlerDiscoveryConfigurationUnresolved,
        Severity = GenerationDiagnosticSeverity.Warning,
        Outcome = GenerationDiagnosticOutcome.Unknown,
        Message = $"Wolverine middleware was not applied because {reason}",
        Source = source,
        Subject = subject
    };

    static IEnumerable<AttributeData> MiddlewareAttributes(ISymbol owner) => owner.GetAttributes().Where(_ =>
        _.AttributeClass is { } type && DotNetSubjectIds.MetadataName(type) == WellKnownTypes.WolverineMiddlewareAttribute);

    static IEnumerable<TypedConstant> MiddlewareValues(AttributeData attribute) =>
        attribute.ConstructorArguments.SelectMany(ValuesOf);

    static IEnumerable<TypedConstant> ValuesOf(TypedConstant value)
    {
        if (value.Kind != TypedConstantKind.Array)
        {
            return [value];
        }

        return value.IsNull ? [] : value.Values;
    }

    static IEnumerable<INamedTypeSymbol> MiddlewareTypes(AttributeData attribute) =>
        MiddlewareValues(attribute).Select(_ => _.Value).OfType<INamedTypeSymbol>();

    static bool HasResolvedMiddlewareTypes(AttributeData attribute, DotNetProjectCompilation project)
    {
        var values = MiddlewareValues(attribute).ToArray();
        var scoping = attribute.NamedArguments.FirstOrDefault(_ => _.Key == "Scoping").Value.Value;
        return scoping is null or 0 or 1 or 2 or 3 && values.Length > 0 &&
            values.All(_ => _.Value is INamedTypeSymbol type && IsMiddlewareType(type, project));
    }

    static bool IsMiddlewareType(INamedTypeSymbol type, DotNetProjectCompilation project) =>
        !type.IsUnboundGenericType && IsAuthored(type, project);

    static bool IsAuthored(INamedTypeSymbol type, DotNetProjectCompilation project) =>
        type.DeclaringSyntaxReferences.Any(_ => project.AuthoredSyntaxTrees.Contains(_.SyntaxTree) && !DotNetGeneratedSource.IsGenerated(_.SyntaxTree));
}

sealed record WolverineMiddlewareApplication(INamedTypeSymbol Type, SourceRange? Source);
