// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Cratis.Screenplay.Generation;
using Cratis.Screenplay.Generation.DotNet;
using Microsoft.CodeAnalysis;

namespace Cratis.CritterStack.Screenplay.Marten;

static class MartenConventionAlterationDiscovery
{
    public static IReadOnlyList<GenerationDiagnostic> Discover(DotNetProjectCompilation project)
    {
        var catalog = new DotNetArtifactCatalog(project.Compilation);
        return
        [
            .. catalog.Types
                .Where(_ => IsAuthoredConcreteType(_, project) && AltersConventions(_))
                .Select(type =>
                {
                    var location = type.Locations.First(_ => IsAuthoredLocation(_, project));
                    return new GenerationDiagnostic
                    {
                        Code = MartenDiagnosticCodes.ConventionAlterationOmitted,
                        Severity = GenerationDiagnosticSeverity.Warning,
                        Outcome = GenerationDiagnosticOutcome.Unsupported,
                        Message = $"Authored Marten convention-alteration type '{type.Name}' may change store shape at runtime; direct source declarations remain modeled, but policy consequences are not interpreted",
                        Source = CritterStackSource.RangeForProject(location, project),
                        Subject = project.SubjectForType(type)
                    };
                })
                .OrderBy(_ => _.Source?.Path, StringComparer.Ordinal)
                .ThenBy(_ => _.Source?.StartLine)
                .ThenBy(_ => _.Source?.StartColumn)
                .ThenBy(_ => _.Subject?.Value, StringComparer.Ordinal)
        ];
    }

    static bool AltersConventions(INamedTypeSymbol type)
    {
        if (DotNetSymbols.Implements(type, WellKnownTypes.MartenConfigure) ||
            DotNetSymbols.Implements(type, WellKnownTypes.MartenAsyncConfigure) ||
            DotNetSymbols.Implements(type, WellKnownTypes.MartenDocumentPolicy))
        {
            return true;
        }

        return DotNetSubjectIds.MetadataName(type.OriginalDefinition) != WellKnownTypes.MartenProjectionDocumentPolicy &&
               DotNetSymbols.IsOrInheritsFrom(type, WellKnownTypes.MartenProjectionDocumentPolicy);
    }

    static bool IsAuthoredConcreteType(INamedTypeSymbol type, DotNetProjectCompilation project) =>
        type.TypeKind == TypeKind.Class &&
        !type.IsAbstract &&
        type.Locations.Any(_ => IsAuthoredLocation(_, project));

    static bool IsAuthoredLocation(Location location, DotNetProjectCompilation project) =>
        location.IsInSource &&
        location.SourceTree is not null &&
        project.AuthoredSyntaxTrees.Contains(location.SourceTree) &&
        !DotNetGeneratedSource.IsGenerated(location.SourceTree);
}
