// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Cratis.Screenplay.Generation;
using Cratis.Screenplay.Generation.DotNet;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Cratis.CritterStack.Screenplay.Marten;

sealed record MartenDocumentUsage(INamedTypeSymbol Type, Evidence Evidence);

sealed record MartenEventProjectionResult(
    IReadOnlyList<GenerationFact> Facts,
    IReadOnlyList<GenerationDiagnostic> Diagnostics,
    IReadOnlyList<MartenDocumentUsage> Documents);

static class MartenEventProjectionFacts
{
    static readonly HashSet<string> _operationMethodNames = ["Store", "Insert", "Update", "Delete", "DeleteWhere"];
    static readonly HashSet<string> _conventionalMethodNames = ["Project", "Create", "Transform"];
    static readonly HashSet<string> _asyncReturnTypes = ["System.Threading.Tasks.Task`1", "System.Threading.Tasks.ValueTask`1"];
    static readonly HashSet<string> _teardownTypes =
    [
        "JasperFx.Events.Projections.AsyncOptions",
        "Marten.Events.Daemon.AsyncOptions"
    ];
    static readonly HashSet<string> _infrastructureParameterTypes =
    [
        "System.Threading.CancellationToken",
        "JasperFx.Events.IEvent",
        "Marten.Events.IEvent",
        "Marten.IQuerySession",
        "Marten.IDocumentOperations",
        "Marten.IDocumentSession"
    ];
    static readonly string[] _eventWrapperTypes = ["JasperFx.Events.IEvent`1", "Marten.Events.IEvent`1"];

    public static MartenEventProjectionResult Discover(
        DotNetProjectCompilation project,
        AdapterIdentity adapter,
        CritterStackSubjectResolver subjects,
        ProjectionRegistration registration)
    {
        var projection = registration.Projection!;
        var operations = new List<EventDocumentOperation>();
        var teardownTypes = new HashSet<INamedTypeSymbol>(SymbolEqualityComparer.Default);

        foreach (var tree in project.Compilation.SyntaxTrees.Where(_ => !DotNetGeneratedSource.IsGenerated(_)))
        {
            var semanticModel = project.Compilation.GetSemanticModel(tree);
            foreach (var declaration in tree.GetRoot().DescendantNodes().OfType<MethodDeclarationSyntax>())
            {
                if (semanticModel.GetDeclaredSymbol(declaration) is not IMethodSymbol method ||
                    !SymbolEqualityComparer.Default.Equals(method.ContainingType, projection) ||
                    method.Name is not ("Create" or "Transform"))
                {
                    continue;
                }

                var eventType = EventTypeFrom(method);
                var documentType = DocumentReturnTypeFrom(method);
                if (eventType is null || documentType is null)
                {
                    continue;
                }

                operations.Add(new(
                    eventType,
                    documentType,
                    RelationshipKind.Stores,
                    CritterStackSource.EvidenceFor(method, adapter, project, EvidenceStrength.Exact, $"Marten EventProjection {method.Name} returns the '{documentType.Name}' document for '{eventType.Name}'")));
            }

            foreach (var invocation in tree.GetRoot().DescendantNodes().OfType<InvocationExpressionSyntax>())
            {
                if (semanticModel.GetSymbolInfo(invocation).Symbol is not IMethodSymbol method)
                {
                    continue;
                }

                var enclosingSymbol = semanticModel.GetEnclosingSymbol(invocation.SpanStart);
                if (enclosingSymbol is IMethodSymbol teardownMethod &&
                    SymbolEqualityComparer.Default.Equals(teardownMethod.ContainingType, projection) &&
                    TryGetTeardownType(method, out var teardownType))
                {
                    teardownTypes.Add(teardownType);
                    continue;
                }

                if (!IsDocumentOperation(method) ||
                    enclosingSymbol is not IMethodSymbol containingMethod ||
                    !SymbolEqualityComparer.Default.Equals(containingMethod.ContainingType, projection))
                {
                    continue;
                }

                var eventType = EventTypeFor(invocation, containingMethod, semanticModel);
                var documentType = DocumentTypeFrom(method);
                var relationshipKind = RelationshipKindFor(method.Name);
                if (eventType is null || documentType is null || relationshipKind is null)
                {
                    continue;
                }

                operations.Add(new(
                    eventType,
                    documentType,
                    relationshipKind.Value,
                    new Evidence
                    {
                        Adapter = adapter,
                        Strength = EvidenceStrength.Exact,
                        Source = CritterStackSource.RangeForProject(invocation.GetLocation(), project),
                        Explanation = $"Marten EventProjection '{eventType.Name}' operation uses IDocumentOperations.{method.Name} for '{documentType.Name}'"
                    }));
            }
        }

        var exactOperations = operations
            .GroupBy(_ => new OperationKey(
                subjects.SubjectForType(project, _.EventType),
                subjects.SubjectForType(project, _.DocumentType),
                _.Kind))
            .Select(_ => _.OrderBy(operation => operation.Evidence.Source?.Path, StringComparer.Ordinal)
                .ThenBy(operation => operation.Evidence.Source?.StartLine)
                .First())
            .OrderBy(_ => DotNetSubjectIds.MetadataName(_.EventType), StringComparer.Ordinal)
            .ThenBy(_ => DotNetSubjectIds.MetadataName(_.DocumentType), StringComparer.Ordinal)
            .ThenBy(_ => _.Kind)
            .ToArray();

        var diagnostics = new List<GenerationDiagnostic>
        {
            EventProjectionDiagnostic(project, subjects, registration, exactOperations.Length > 0)
        };
        if (exactOperations.Length == 0)
        {
            return new([], diagnostics, []);
        }

        var projectionSubject = subjects.SubjectForType(project, projection);
        var facts = new List<GenerationFact>
        {
            Artifact(
                $"marten:event-projection:{projectionSubject.Value}",
                projectionSubject,
                ArtifactKind.Projection,
                projection.Name,
                SourceFileOf(projection, project),
                [],
                registration.Evidence)
        };

        foreach (var eventGroup in exactOperations.GroupBy(_ => subjects.SubjectForType(project, _.EventType)))
        {
            var operation = eventGroup.First();
            var eventType = operation.EventType;
            var eventSubject = eventGroup.Key;
            facts.Add(Artifact(
                $"marten:event:{eventSubject.Value}",
                eventSubject,
                ArtifactKind.Event,
                eventType.Name,
                SourceFileOf(eventType, project),
                DotNetTypeShapes.PropertiesOf(eventType),
                operation.Evidence));
            facts.Add(Relationship(
                $"marten:event-projection:consumes:{projectionSubject.Value}:{eventSubject.Value}",
                projectionSubject,
                RelationshipKind.Consumes,
                eventSubject,
                operation.Evidence));
        }

        var documents = new List<MartenDocumentUsage>();
        foreach (var documentGroup in exactOperations.GroupBy(_ => subjects.SubjectForType(project, _.DocumentType)))
        {
            var operation = documentGroup.First();
            var documentSubject = documentGroup.Key;
            var evidence = teardownTypes.Contains(operation.DocumentType)
                ? operation.Evidence with
                {
                    Explanation = $"{operation.Evidence.Explanation}; DeleteViewTypeOnTeardown corroborates the document target"
                }
                : operation.Evidence;
            documents.Add(new(operation.DocumentType, evidence));
            facts.Add(Relationship(
                $"marten:event-projection:builds:{projectionSubject.Value}:{documentSubject.Value}",
                projectionSubject,
                RelationshipKind.Builds,
                documentSubject,
                evidence));
        }

        foreach (var operation in exactOperations)
        {
            var documentSubject = subjects.SubjectForType(project, operation.DocumentType);
            var eventSubject = subjects.SubjectForType(project, operation.EventType);
            facts.Add(Relationship(
                $"marten:event-projection:{operation.Kind}:{projectionSubject.Value}:{eventSubject.Value}:{documentSubject.Value}",
                projectionSubject,
                operation.Kind,
                documentSubject,
                operation.Evidence,
                discriminator: eventSubject.Value));
        }

        return new(facts, diagnostics, documents);
    }

    static INamedTypeSymbol? EventTypeFor(
        InvocationExpressionSyntax invocation,
        IMethodSymbol containingMethod,
        SemanticModel semanticModel)
    {
        if (_conventionalMethodNames.Contains(containingMethod.Name))
        {
            return EventTypeFrom(containingMethod);
        }

        if (containingMethod.Name != "ApplyAsync" ||
            invocation.Ancestors().OfType<SwitchSectionSyntax>().FirstOrDefault() is not { } section ||
            section.Parent is not SwitchStatementSyntax switchStatement ||
            !SwitchesOnRawEventData(switchStatement, containingMethod, semanticModel))
        {
            return null;
        }

        foreach (var label in section.Labels.OfType<CasePatternSwitchLabelSyntax>())
        {
            var type = label.Pattern switch
            {
                DeclarationPatternSyntax declaration => semanticModel.GetTypeInfo(declaration.Type).Type,
                TypePatternSyntax typePattern => semanticModel.GetTypeInfo(typePattern.Type).Type,
                _ => null
            };
            if (type is INamedTypeSymbol namedType)
            {
                return EventTypeFrom(namedType);
            }
        }

        return null;
    }

    static bool SwitchesOnRawEventData(
        SwitchStatementSyntax switchStatement,
        IMethodSymbol containingMethod,
        SemanticModel semanticModel)
    {
        if (switchStatement.Expression is not MemberAccessExpressionSyntax memberAccess ||
            semanticModel.GetSymbolInfo(memberAccess).Symbol is not IPropertySymbol { Name: "Data" } dataProperty ||
            !_infrastructureParameterTypes.Contains(DotNetSubjectIds.MetadataName(dataProperty.ContainingType.OriginalDefinition)) ||
            semanticModel.GetSymbolInfo(memberAccess.Expression).Symbol is not IParameterSymbol parameter)
        {
            return false;
        }

        return SymbolEqualityComparer.Default.Equals(parameter.ContainingSymbol, containingMethod);
    }

    static INamedTypeSymbol? EventTypeFrom(IMethodSymbol method)
    {
        foreach (var parameter in method.Parameters)
        {
            if (parameter.Type is not INamedTypeSymbol type)
            {
                continue;
            }

            var eventType = EventTypeFrom(type);
            if (eventType is not null)
            {
                return eventType;
            }
        }

        return null;
    }

    static INamedTypeSymbol? EventTypeFrom(INamedTypeSymbol type)
    {
        if (type.IsGenericType && _eventWrapperTypes.Contains(DotNetSubjectIds.MetadataName(type.OriginalDefinition), StringComparer.Ordinal))
        {
            return type.TypeArguments[0] as INamedTypeSymbol;
        }

        return _infrastructureParameterTypes.Contains(DotNetSubjectIds.MetadataName(type.OriginalDefinition)) || !IsSourceType(type)
            ? null
            : type;
    }

    static INamedTypeSymbol? DocumentReturnTypeFrom(IMethodSymbol method)
    {
        var returnType = method.ReturnType;
        if (returnType is INamedTypeSymbol namedReturnType &&
            namedReturnType.IsGenericType &&
            _asyncReturnTypes.Contains(DotNetSubjectIds.MetadataName(namedReturnType.OriginalDefinition)))
        {
            return namedReturnType.TypeArguments[0] as INamedTypeSymbol is { } asyncDocument && IsSourceType(asyncDocument)
                ? asyncDocument
                : null;
        }

        return returnType is INamedTypeSymbol documentType && IsSourceType(documentType)
            ? documentType
            : null;
    }

    static INamedTypeSymbol? DocumentTypeFrom(IMethodSymbol method) => method.TypeArguments
        .OfType<INamedTypeSymbol>()
        .FirstOrDefault(IsSourceType);

    static bool IsDocumentOperation(IMethodSymbol method)
    {
        var candidate = method.ReducedFrom ?? method;
        return _operationMethodNames.Contains(candidate.Name) &&
               DotNetSubjectIds.MetadataName(candidate.ContainingType.OriginalDefinition) == "Marten.IDocumentOperations";
    }

    static bool TryGetTeardownType(IMethodSymbol method, out INamedTypeSymbol type)
    {
        var candidate = method.ReducedFrom ?? method;
        if (candidate.Name == "DeleteViewTypeOnTeardown" &&
            _teardownTypes.Contains(DotNetSubjectIds.MetadataName(candidate.ContainingType.OriginalDefinition)) &&
            method.TypeArguments.OfType<INamedTypeSymbol>().FirstOrDefault(IsSourceType) is { } documentType)
        {
            type = documentType;
            return true;
        }

        type = null!;
        return false;
    }

    static RelationshipKind? RelationshipKindFor(string methodName) => methodName switch
    {
        "Store" or "Insert" => RelationshipKind.Stores,
        "Update" => RelationshipKind.Updates,
        "Delete" or "DeleteWhere" => RelationshipKind.Deletes,
        _ => null
    };

    static bool IsSourceType(INamedTypeSymbol type) => type.TypeKind != TypeKind.Error && type.Locations.Any(_ => _.IsInSource);

    static string? SourceFileOf(ISymbol symbol, DotNetProjectCompilation project) =>
        CritterStackSource.EvidenceFor(symbol, new AdapterIdentity { Id = "source", Version = "1" }, project, EvidenceStrength.Exact).Source?.Path;

    static ArtifactFact Artifact(
        string id,
        SubjectId subject,
        ArtifactKind kind,
        string name,
        string? file,
        IReadOnlyList<PropertyDefinition> properties,
        Evidence evidence) => new()
        {
            Id = new FactId { Value = id },
            Subject = subject,
            Definition = new ArtifactDefinition
            {
                Key = new ArtifactKey { Subject = subject, Kind = kind },
                Name = name,
                File = file,
                Properties = properties
            },
            Evidence = evidence
        };

    static RelationshipFact Relationship(
        string id,
        SubjectId source,
        RelationshipKind kind,
        SubjectId target,
        Evidence evidence,
        string? discriminator = null) => new()
        {
            Id = new FactId { Value = id },
            Subject = source,
            Definition = new RelationshipDefinition
            {
                Key = new RelationshipKey { Kind = kind, Source = source, Target = target, Discriminator = discriminator }
            },
            Evidence = evidence
        };

    static GenerationDiagnostic EventProjectionDiagnostic(
        DotNetProjectCompilation project,
        CritterStackSubjectResolver subjects,
        ProjectionRegistration registration,
        bool hasExactOperations) => new()
        {
            Code = MartenDiagnosticCodes.EventProjectionOmitted,
            Severity = GenerationDiagnosticSeverity.Warning,
            Outcome = GenerationDiagnosticOutcome.Unsupported,
            Message = hasExactOperations
            ? $"Event projection '{registration.Projection!.Name}' has exact event and document operation relationships, but arbitrary document body, value, and predicate flow remains code-defined and was omitted"
            : $"Event projection '{registration.Projection!.Name}' has no authored Create return or event-bound IDocumentOperations Store, Insert, Update, Delete, or DeleteWhere operation that can be represented exactly",
            Source = registration.Evidence.Source,
            Subject = subjects.SubjectForType(project, registration.Projection)
        };

    sealed record EventDocumentOperation(
        INamedTypeSymbol EventType,
        INamedTypeSymbol DocumentType,
        RelationshipKind Kind,
        Evidence Evidence);

    sealed record OperationKey(SubjectId Event, SubjectId Document, RelationshipKind Kind);
}
