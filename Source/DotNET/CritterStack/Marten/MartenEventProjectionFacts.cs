// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Cratis.Screenplay.Generation;
using Cratis.Screenplay.Generation.DotNet;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Cratis.CritterStack.Screenplay.Marten;

sealed record MartenDocumentUsage(INamedTypeSymbol Type, Evidence Evidence)
{
    public BackendOwnership Ownership { get; init; } = BackendOwnership.Legacy;
}

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

    public static MartenEventProjectionResult Discover(MartenCollectedFacts collected, ProjectionRegistration registration)
    {
        var (project, adapter, subjects) = (collected.Project, collected.Adapter, collected.Subjects);
        var scan = SourceOperations(project, adapter, registration.Projection!);
        var exactOperations = scan.Operations
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
        return BuildResult(collected, registration, new(exactOperations, scan.TeardownTypes));
    }

    public static IReadOnlyList<MartenDocumentObservation> ObserveDocuments(
        DotNetProjectCompilation project,
        AdapterIdentity adapter,
        ProjectionRegistration registration) =>
    [
        .. SourceOperations(project, adapter, registration.Projection!).Operations
            .OrderBy(operation => DotNetSubjectIds.MetadataName(operation.EventType), StringComparer.Ordinal)
            .ThenBy(operation => DotNetSubjectIds.MetadataName(operation.DocumentType), StringComparer.Ordinal)
            .ThenBy(operation => operation.Kind)
            .ThenBy(operation => operation.Evidence.Source?.Path, StringComparer.Ordinal)
            .ThenBy(operation => operation.Evidence.Source?.StartLine)
            .Select((operation, order) => new MartenDocumentObservation
            {
                Type = operation.DocumentType,
                Evidence = operation.Evidence,
                Ownership = registration.Ownership,
                Site = operation.Site,
                Order = order,
                Preference = OmissionDiagnosticEvidencePreference.UseSite,
                IsProjected = true,
                Method = operation.Method
            })
    ];

    static OperationScan SourceOperations(DotNetProjectCompilation project, AdapterIdentity adapter, INamedTypeSymbol projection)
    {
        var scan = new OperationScan(project, adapter, projection);
        foreach (var tree in project.Compilation.SyntaxTrees.Where(_ => !DotNetGeneratedSource.IsGenerated(_)))
        {
            var semanticModel = project.Compilation.GetSemanticModel(tree);
            scan.CollectConventionalOperations(tree, semanticModel);
            scan.CollectInvocationOperations(tree, semanticModel);
        }

        return scan;
    }

    static MartenEventProjectionResult BuildResult(
        MartenCollectedFacts collected,
        ProjectionRegistration registration,
        ExactOperations operations)
    {
        var (project, subjects) = (collected.Project, collected.Subjects);
        var exactOperations = operations.Operations;
        var projection = registration.Projection!;
        var diagnostics = new List<GenerationDiagnostic>
        {
            EventProjectionDiagnostic(project, subjects, registration, exactOperations.Length > 0)
        };
        if (exactOperations.Length == 0)
        {
            return new([], diagnostics, []);
        }

        var projectionSubject = subjects.SubjectForType(project, projection);
        var local = collected.WithOwnership(registration.Ownership, []);
        var facts = local.Facts;
        new MartenSourceArtifact
        {
            Id = $"marten:event-projection:{projectionSubject.Value}",
            Subject = projectionSubject,
            Kind = ArtifactKind.Projection,
            Name = projection.Name,
            File = MartenSourceFiles.FileOf(projection, project),
            Evidence = registration.Evidence
        }.AddTo(local);

        var scope = new ProjectionFactScope(local, projectionSubject);
        AddEventFacts(scope, facts, exactOperations);
        var documents = AddDocumentFacts(scope, facts, operations);
        AddOperationFacts(scope, facts, exactOperations);
        return new(facts, diagnostics, documents);
    }

    static void AddEventFacts(
        ProjectionFactScope scope,
        List<GenerationFact> facts,
        EventDocumentOperation[] exactOperations)
    {
        var (project, subjects, projectionSubject) = (scope.Project, scope.Subjects, scope.ProjectionSubject);
        foreach (var eventGroup in exactOperations.GroupBy(_ => subjects.SubjectForType(project, _.EventType)))
        {
            var operation = eventGroup.First();
            var eventType = operation.EventType;
            var eventSubject = eventGroup.Key;
            new MartenSourceArtifact
            {
                Id = $"marten:event:{eventSubject.Value}",
                Subject = eventSubject,
                Kind = ArtifactKind.Event,
                Name = eventType.Name,
                File = MartenSourceFiles.FileOf(eventType, project),
                SourceType = eventType,
                Evidence = operation.Evidence
            }.AddTo(scope.Collected);
            facts.Add(new MartenRelationshipFact
            {
                Id = $"marten:event-projection:consumes:{projectionSubject.Value}:{eventSubject.Value}",
                Source = projectionSubject,
                Kind = RelationshipKind.Consumes,
                Target = eventSubject,
                Evidence = operation.Evidence
            }.ToFact(scope.Ownership.Scope, scope.Ownership.Scope));
        }
    }

    static List<MartenDocumentUsage> AddDocumentFacts(
        ProjectionFactScope scope,
        List<GenerationFact> facts,
        ExactOperations operations)
    {
        var (project, subjects, projectionSubject) = (scope.Project, scope.Subjects, scope.ProjectionSubject);
        var (exactOperations, teardownTypes) = (operations.Operations, operations.TeardownTypes);
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
            documents.Add(new(operation.DocumentType, evidence) { Ownership = scope.Ownership });
            facts.Add(new MartenRelationshipFact
            {
                Id = $"marten:event-projection:builds:{projectionSubject.Value}:{documentSubject.Value}",
                Source = projectionSubject,
                Kind = RelationshipKind.Builds,
                Target = documentSubject,
                Evidence = evidence
            }.ToFact(scope.Ownership.Scope, scope.Ownership.Scope));
        }

        return documents;
    }

    static void AddOperationFacts(
        ProjectionFactScope scope,
        List<GenerationFact> facts,
        EventDocumentOperation[] exactOperations)
    {
        foreach (var operation in exactOperations)
        {
            var documentSubject = scope.Subjects.SubjectForType(scope.Project, operation.DocumentType);
            var eventSubject = scope.Subjects.SubjectForType(scope.Project, operation.EventType);
            facts.Add(new MartenRelationshipFact
            {
                Id = $"marten:event-projection:{operation.Kind}:{scope.ProjectionSubject.Value}:{eventSubject.Value}:{documentSubject.Value}",
                Source = scope.ProjectionSubject,
                Kind = operation.Kind,
                Target = documentSubject,
                Evidence = operation.Evidence,
                Discriminator = eventSubject.Value
            }.ToFact(scope.Ownership.Scope, scope.Ownership.Scope));
        }
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

        return containingMethod.Name == "ApplyAsync" &&
               invocation.Ancestors().OfType<SwitchSectionSyntax>().FirstOrDefault() is { Parent: SwitchStatementSyntax switchStatement } section &&
               SwitchesOnRawEventData(switchStatement, containingMethod, semanticModel)
            ? EventTypeFromLabels(section, semanticModel)
            : null;
    }

    static INamedTypeSymbol? EventTypeFromLabels(SwitchSectionSyntax section, SemanticModel semanticModel)
    {
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
            !IsInfrastructureDataAccess(memberAccess, semanticModel))
        {
            return false;
        }

        return semanticModel.GetSymbolInfo(memberAccess.Expression).Symbol is IParameterSymbol parameter &&
               SymbolEqualityComparer.Default.Equals(parameter.ContainingSymbol, containingMethod);
    }

    static bool IsInfrastructureDataAccess(MemberAccessExpressionSyntax memberAccess, SemanticModel semanticModel) =>
        semanticModel.GetSymbolInfo(memberAccess).Symbol is IPropertySymbol dataProperty &&
        dataProperty.Name == "Data" &&
        _infrastructureParameterTypes.Contains(DotNetSubjectIds.MetadataName(dataProperty.ContainingType.OriginalDefinition));

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
        if (returnType is INamedTypeSymbol namedReturnType && IsAsyncReturnType(namedReturnType))
        {
            return namedReturnType.TypeArguments[0] as INamedTypeSymbol is { } asyncDocument && IsSourceType(asyncDocument)
                ? asyncDocument
                : null;
        }

        return returnType is INamedTypeSymbol documentType && IsSourceType(documentType)
            ? documentType
            : null;
    }

    static bool IsAsyncReturnType(INamedTypeSymbol type) =>
        type.IsGenericType && _asyncReturnTypes.Contains(DotNetSubjectIds.MetadataName(type.OriginalDefinition));

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
        if (IsTeardownMethod(candidate) &&
            method.TypeArguments.OfType<INamedTypeSymbol>().FirstOrDefault(IsSourceType) is { } documentType)
        {
            type = documentType;
            return true;
        }

        type = null!;
        return false;
    }

    static bool IsTeardownMethod(IMethodSymbol candidate) =>
        candidate.Name == "DeleteViewTypeOnTeardown" &&
        _teardownTypes.Contains(DotNetSubjectIds.MetadataName(candidate.ContainingType.OriginalDefinition));

    static RelationshipKind? RelationshipKindFor(string methodName) => methodName switch
    {
        "Store" or "Insert" => RelationshipKind.Stores,
        "Update" => RelationshipKind.Updates,
        "Delete" or "DeleteWhere" => RelationshipKind.Deletes,
        _ => null
    };

    static bool IsSourceType(INamedTypeSymbol type) => type.TypeKind != TypeKind.Error && type.Locations.Any(_ => _.IsInSource);

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
        Evidence Evidence)
    {
        public required IMethodSymbol Method { get; init; }
        public required string Site { get; init; }
    }

    sealed record ProjectionFactScope(MartenCollectedFacts Collected, SubjectId ProjectionSubject)
    {
        public DotNetProjectCompilation Project => Collected.Project;
        public CritterStackSubjectResolver Subjects => Collected.Subjects;
        public BackendOwnership Ownership => Collected.Ownership;
    }

    sealed record ExactOperations(
        EventDocumentOperation[] Operations,
        HashSet<INamedTypeSymbol> TeardownTypes);

    sealed record OperationKey(SubjectId Event, SubjectId Document, RelationshipKind Kind);

    sealed class OperationScan(DotNetProjectCompilation project, AdapterIdentity adapter, INamedTypeSymbol projection)
    {
        public List<EventDocumentOperation> Operations { get; } = [];

        public HashSet<INamedTypeSymbol> TeardownTypes { get; } = [with(SymbolEqualityComparer.Default)];

        public void CollectConventionalOperations(SyntaxTree tree, SemanticModel semanticModel)
        {
            foreach (var declaration in tree.GetRoot().DescendantNodes().OfType<MethodDeclarationSyntax>())
            {
                if (ConventionalOperationFor(declaration, semanticModel) is { } operation)
                {
                    Operations.Add(operation);
                }
            }
        }

        public void CollectInvocationOperations(SyntaxTree tree, SemanticModel semanticModel)
        {
            foreach (var invocation in tree.GetRoot().DescendantNodes().OfType<InvocationExpressionSyntax>())
            {
                if (semanticModel.GetSymbolInfo(invocation).Symbol is IMethodSymbol method)
                {
                    CollectInvocation(invocation, method, semanticModel);
                }
            }
        }

        EventDocumentOperation? ConventionalOperationFor(MethodDeclarationSyntax declaration, SemanticModel semanticModel)
        {
            if (semanticModel.GetDeclaredSymbol(declaration) is not IMethodSymbol method || !IsConventionalProjectionMethod(method))
            {
                return null;
            }

            var eventType = EventTypeFrom(method);
            var documentType = DocumentReturnTypeFrom(method);
            if (eventType is null || documentType is null)
            {
                return null;
            }

            return new(
                eventType,
                documentType,
                RelationshipKind.Stores,
                CritterStackSource.EvidenceFor(method, adapter, project, EvidenceStrength.Exact, $"Marten EventProjection {method.Name} returns the '{documentType.Name}' document for '{eventType.Name}'"))
            {
                Method = method,
                Site = MartenDocumentSource.SiteFor(declaration, project)
            };
        }

        void CollectInvocation(InvocationExpressionSyntax invocation, IMethodSymbol method, SemanticModel semanticModel)
        {
            var enclosingSymbol = semanticModel.GetEnclosingSymbol(invocation.SpanStart);
            if (enclosingSymbol is not IMethodSymbol containingMethod || !IsProjectionMethod(containingMethod))
            {
                return;
            }

            if (TryGetTeardownType(method, out var teardownType))
            {
                TeardownTypes.Add(teardownType);
            }
            else if (IsDocumentOperation(method))
            {
                CollectDocumentOperation(invocation, method, containingMethod, semanticModel);
            }
        }

        void CollectDocumentOperation(
            InvocationExpressionSyntax invocation,
            IMethodSymbol method,
            IMethodSymbol containingMethod,
            SemanticModel semanticModel)
        {
            var eventType = EventTypeFor(invocation, containingMethod, semanticModel);
            var documentType = DocumentTypeFrom(method);
            if (eventType is null || documentType is null || RelationshipKindFor(method.Name) is not { } relationshipKind)
            {
                return;
            }

            Operations.Add(new(
                eventType,
                documentType,
                relationshipKind,
                new Evidence
                {
                    Adapter = adapter,
                    Strength = EvidenceStrength.Exact,
                    Source = CritterStackSource.RangeForProject(invocation.GetLocation(), project),
                    Explanation = $"Marten EventProjection '{eventType.Name}' operation uses IDocumentOperations.{method.Name} for '{documentType.Name}'"
                })
            {
                Method = containingMethod,
                Site = MartenDocumentSource.SiteFor(invocation, project)
            });
        }

        bool IsConventionalProjectionMethod(IMethodSymbol method) =>
            IsProjectionMethod(method) && (method.Name == "Create" || method.Name == "Transform");

        bool IsProjectionMethod(IMethodSymbol method) => SymbolEqualityComparer.Default.Equals(method.ContainingType, projection);
    }
}
