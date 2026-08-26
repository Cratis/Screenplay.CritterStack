// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Cratis.Screenplay.Generation;
using Cratis.Screenplay.Generation.DotNet;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Cratis.CritterStack.Screenplay.Marten;

static class MartenSessionListenerDiscovery
{
    public static IReadOnlyList<GenerationDiagnostic> Discover(
        DotNetProjectCompilation project,
        CritterStackSubjectResolver subjects)
    {
        var registrations = ListenerRegistrations(project);
        var catalog = new DotNetArtifactCatalog(project.Compilation);
        return
        [
            .. catalog.Types
                .Where(_ => IsAuthoredConcreteListener(_, project))
                .Select(type =>
                {
                    var registration = registrations.Find(_ => SymbolEqualityComparer.Default.Equals(_.Type, type));
                    var location = registration?.Location ?? type.Locations.First(_ => IsAuthoredLocation(_, project));
                    return new GenerationDiagnostic
                    {
                        Code = MartenDiagnosticCodes.SessionListenerOmitted,
                        Severity = GenerationDiagnosticSeverity.Information,
                        Outcome = GenerationDiagnosticOutcome.Unsupported,
                        Message = $"Authored Marten session listener '{type.Name}' observes document commits; its consequences are not represented",
                        Source = CritterStackSource.RangeForProject(location, project),
                        Subject = subjects.SubjectForType(project, type)
                    };
                })
                .OrderBy(_ => _.Source?.Path, StringComparer.Ordinal)
                .ThenBy(_ => _.Source?.StartLine)
                .ThenBy(_ => _.Source?.StartColumn)
                .ThenBy(_ => _.Subject?.Value, StringComparer.Ordinal)
        ];
    }

    static List<ListenerRegistration> ListenerRegistrations(DotNetProjectCompilation project)
    {
        var registrations = new List<ListenerRegistration>();
        foreach (var tree in project.Compilation.SyntaxTrees
                     .Where(_ => project.AuthoredSyntaxTrees.Contains(_) && !DotNetGeneratedSource.IsGenerated(_))
                     .OrderBy(_ => _.FilePath, StringComparer.Ordinal))
        {
            var semanticModel = project.Compilation.GetSemanticModel(tree);
            foreach (var invocation in tree.GetRoot().DescendantNodes().OfType<InvocationExpressionSyntax>().OrderBy(_ => _.SpanStart))
            {
                if (invocation.Expression is not MemberAccessExpressionSyntax
                    {
                        Name.Identifier.ValueText: "Add",
                        Expression: MemberAccessExpressionSyntax listeners
                    } ||
                    semanticModel.GetSymbolInfo(listeners).Symbol is not IPropertySymbol { Name: "Listeners" } ||
                    invocation.ArgumentList.Arguments.FirstOrDefault()?.Expression is not { } argument)
                {
                    continue;
                }

                var creation = argument.DescendantNodesAndSelf()
                    .OfType<ObjectCreationExpressionSyntax>()
                    .FirstOrDefault();
                var listenerType = creation is null
                    ? semanticModel.GetTypeInfo(argument).Type as INamedTypeSymbol
                    : semanticModel.GetTypeInfo(creation).Type as INamedTypeSymbol;
                if (listenerType is not null && IsListener(listenerType))
                {
                    registrations.Add(new(listenerType, invocation.GetLocation()));
                }
            }
        }

        return registrations;
    }

    static bool IsAuthoredConcreteListener(INamedTypeSymbol type, DotNetProjectCompilation project) =>
        type.TypeKind == TypeKind.Class &&
        !type.IsAbstract &&
        IsListener(type) &&
        type.Locations.Any(_ => IsAuthoredLocation(_, project));

    static bool IsListener(INamedTypeSymbol type) =>
        DotNetSymbols.Implements(type, WellKnownTypes.MartenDocumentSessionListener) ||
        DotNetSymbols.Implements(type, WellKnownTypes.MartenChangeListener);

    static bool IsAuthoredLocation(Location location, DotNetProjectCompilation project) =>
        location.IsInSource &&
        location.SourceTree is not null &&
        project.AuthoredSyntaxTrees.Contains(location.SourceTree) &&
        !DotNetGeneratedSource.IsGenerated(location.SourceTree);

    sealed record ListenerRegistration(INamedTypeSymbol Type, Location Location);
}
