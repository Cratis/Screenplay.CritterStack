// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Cratis.Screenplay.Generation;
using Cratis.Screenplay.Generation.DotNet;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Cratis.CritterStack.Screenplay.Wolverine;

sealed record WolverineSagaDiscoveryResult(
    IReadOnlyList<GenerationFact> Facts,
    IReadOnlyList<GenerationDiagnostic> Diagnostics);

sealed record WolverineSagaRoleMethod(
    IMethodSymbol Method,
    INamedTypeSymbol MessageType,
    string Role,
    bool IsCreationCapable);

sealed record WolverineSagaRoleAdmission(
    IMethodSymbol Method,
    WolverineSagaRoleMethod? Role,
    GenerationDiagnostic? Diagnostic);

sealed record WolverineSagaCorrelation(
    string? TargetMember,
    Evidence? Evidence,
    string? UnresolvedReason = null);

sealed record WolverineSagaMemberMatch(
    ISymbol? Member,
    bool IsAmbiguous);

sealed record WolverineSagaFactScope(
    DotNetProjectCompilation Project,
    AdapterIdentity Adapter,
    CritterStackSubjectResolver Subjects,
    List<GenerationFact> Facts,
    List<GenerationDiagnostic> Diagnostics);

sealed record WolverineSagaRoleScope(
    INamedTypeSymbol SagaType,
    WolverineSagaRoleMethod Role,
    SubjectId HandlerSubject,
    Evidence RoleEvidence);

static class WolverineSagaFacts
{
    public static WolverineSagaDiscoveryResult Discover(
        DotNetProjectCompilation project,
        AdapterIdentity adapter,
        CritterStackSubjectResolver subjects,
        WolverineHandlerDiscoveryPolicy discovery)
    {
        var scope = new WolverineSagaFactScope(project, adapter, subjects, [], []);
        var catalog = new DotNetArtifactCatalog(project.Compilation);
        foreach (var sagaType in catalog.Types
                     .Where(type => IsAdmissibleType(type, project, discovery))
                     .OrderBy(DotNetSubjectIds.MetadataName, StringComparer.Ordinal))
        {
            var admitted = WolverineSagaRoles.Admit(sagaType, project, adapter);
            scope.Diagnostics.AddRange(admitted.Diagnostics);
            if (admitted.Roles.Count > 0)
            {
                AddSagaFacts(scope, sagaType, admitted.Roles);
            }
        }

        return new(scope.Facts, scope.Diagnostics);
    }

    public static bool IsSagaType(INamedTypeSymbol type, DotNetProjectCompilation project) =>
        WolverineSagaTypes.IsSagaState(type, project);

    internal static SubjectId HandlerSubject(DotNetProjectCompilation project, IMethodSymbol method) =>
        DotNetMethodIdentity.SubjectFor(project, method);

    internal static string HandlerName(IMethodSymbol method) =>
        DotNetMethodIdentity.DisplayName(method);

    internal static bool IsIgnored(ISymbol symbol, DotNetProjectCompilation project) => symbol.GetAttributes().Any(attribute =>
        attribute.AttributeClass is not null &&
        (DotNetSubjectIds.MetadataName(attribute.AttributeClass.OriginalDefinition) == WellKnownTypes.WolverineIgnoreAttribute ||
         DotNetSubjectIds.MetadataName(attribute.AttributeClass.OriginalDefinition) == WellKnownTypes.WolverineLegacyIgnoreAttribute) &&
        IsAuthoredOrMetadataAttribute(attribute, project));

    internal static bool IsPublicAuthoredMember(ISymbol member, DotNetProjectCompilation project) =>
        !member.IsStatic &&
        member.DeclaredAccessibility == Accessibility.Public &&
        member.Locations.Any(location => IsAuthoredSourceLocation(location, project));

    internal static string MethodSignature(IMethodSymbol method) =>
        method.GetDocumentationCommentId() ?? method.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);

    internal static string SourcePath(ISymbol symbol) => symbol.Locations
        .Where(location => location.IsInSource)
        .Select(location => location.SourceTree?.FilePath ?? string.Empty)
        .Order(StringComparer.Ordinal)
        .FirstOrDefault() ?? string.Empty;

    internal static int SourceStart(ISymbol symbol) => symbol.Locations
        .Where(location => location.IsInSource)
        .Select(location => location.SourceSpan.Start)
        .DefaultIfEmpty(int.MaxValue)
        .Min();

    internal static string LowerFirst(string value) => value.Length == 0
        ? value
        : $"{char.ToLowerInvariant(value[0])}{value[1..]}";

    static void AddSagaFacts(
        WolverineSagaFactScope scope,
        INamedTypeSymbol sagaType,
        IReadOnlyList<WolverineSagaRoleMethod> roles)
    {
        var sagaSubject = scope.Subjects.SubjectForType(scope.Project, sagaType);
        var sagaEvidence = CritterStackSource.EvidenceFor(
            sagaType,
            scope.Adapter,
            scope.Project,
            EvidenceStrength.Exact,
            "Authored public concrete type derived from Wolverine.Saga");
        scope.Facts.Add(Artifact(
            $"wolverine:saga:{sagaSubject.Value}",
            sagaSubject,
            ArtifactKind.Saga,
            sagaType.Name,
            sagaEvidence.Source?.Path,
            SagaProperties(sagaType, scope.Project),
            sagaEvidence));

        foreach (var role in roles)
        {
            AddRoleFacts(scope, sagaType, role, roles);
        }

        var completion = CompletionInvocation(roles, scope.Project);
        scope.Diagnostics.Add(new GenerationDiagnostic
        {
            Code = WolverineDiagnosticCodes.SagaLifecycleRealization,
            Severity = GenerationDiagnosticSeverity.Information,
            Outcome = GenerationDiagnosticOutcome.Unsupported,
            Message = completion is null
                ? $"Saga '{sagaType.Name}' uses Wolverine-managed lifecycle. Authored source does not safely establish a portable domain workflow; neutral Saga/Handler/Handles facts are retained as realization/provenance, and Screenplay uses ordinary Event Modeling building blocks"
                : $"Saga '{sagaType.Name}' invokes Wolverine.Saga.MarkCompleted() within Wolverine-managed lifecycle. Authored source does not safely establish portable conditional completion or a portable domain workflow; neutral Saga/Handler/Handles facts are retained as realization/provenance, and Screenplay uses ordinary Event Modeling building blocks",
            Source = completion ?? sagaEvidence.Source,
            Subject = sagaSubject
        });
    }

    static void AddRoleFacts(
        WolverineSagaFactScope scope,
        INamedTypeSymbol sagaType,
        WolverineSagaRoleMethod role,
        IReadOnlyList<WolverineSagaRoleMethod> allRoles)
    {
        var project = scope.Project;
        var method = role.Method;
        var handlerSubject = HandlerSubject(project, method);
        var roleEvidence = CritterStackSource.EvidenceFor(
            method,
            scope.Adapter,
            project,
            EvidenceStrength.Conventional,
            $"Authored Wolverine saga {role.Role} role method");
        var correlation = WolverineSagaCorrelations.For(project, scope.Adapter, sagaType, role.MessageType, allRoles);
        AddHandlerFacts(scope, role, handlerSubject, roleEvidence, correlation);

        if (correlation.TargetMember is null)
        {
            scope.Diagnostics.Add(new GenerationDiagnostic
            {
                Code = WolverineDiagnosticCodes.SagaCorrelationRuntime,
                Severity = GenerationDiagnosticSeverity.Information,
                Outcome = GenerationDiagnosticOutcome.Unknown,
                Message = correlation.UnresolvedReason ?? $"Saga handler '{sagaType.Name}.{method.Name}' has no authored message correlation member for '{role.MessageType.Name}'; correlation remains runtime-resolved",
                Source = roleEvidence.Source,
                Subject = handlerSubject
            });
        }

        AddConsequenceFacts(scope, new(sagaType, role, handlerSubject, roleEvidence));
    }

    static void AddHandlerFacts(
        WolverineSagaFactScope scope,
        WolverineSagaRoleMethod role,
        SubjectId handlerSubject,
        Evidence roleEvidence,
        WolverineSagaCorrelation correlation)
    {
        var project = scope.Project;
        var method = role.Method;
        var facts = scope.Facts;
        var messageSubject = scope.Subjects.SubjectForType(project, role.MessageType);
        var relationshipEvidence = correlation.Evidence ?? roleEvidence;
        var discriminator = $"wolverine:saga:{role.Role}";

        facts.Add(Artifact(
            $"wolverine:saga-handler:{handlerSubject.Value}",
            handlerSubject,
            ArtifactKind.Handler,
            HandlerName(method),
            roleEvidence.Source?.Path,
            [],
            roleEvidence));
        facts.Add(Artifact(
            $"wolverine:message:{messageSubject.Value}",
            messageSubject,
            ArtifactKind.Message,
            role.MessageType.Name,
            SourceFileOf(role.MessageType, project),
            WolverineFacts.AuthoredMessageProperties(role.MessageType, project),
            relationshipEvidence));
        facts.Add(Relationship(
            $"wolverine:saga-handles:{handlerSubject.Value}:{messageSubject.Value}:{discriminator}",
            handlerSubject,
            RelationshipKind.Handles,
            messageSubject,
            relationshipEvidence,
            targetMember: correlation.TargetMember,
            discriminator: discriminator));
    }

    static void AddConsequenceFacts(WolverineSagaFactScope scope, WolverineSagaRoleScope roleScope)
    {
        var project = scope.Project;
        var subjects = scope.Subjects;
        var facts = scope.Facts;
        var diagnostics = scope.Diagnostics;
        var method = roleScope.Role.Method;
        var handlerSubject = roleScope.HandlerSubject;
        var roleEvidence = roleScope.RoleEvidence;
        var returnConsequences = WolverineReturnConsequences.Classify(
            method,
            project,
            isHttpEndpoint: false,
            aggregateWorkflow: false,
            hasEventStream: false);
        WolverineFacts.AddSagaReturnConsequences(project, subjects, handlerSubject, returnConsequences, roleEvidence, facts);
        WolverineFacts.AddSagaOutgoingMessages(
            project,
            subjects,
            handlerSubject,
            method,
            WolverineFacts.DiscoverSagaOutgoingMessages(method, project),
            roleEvidence,
            facts,
            diagnostics);
        WolverineFacts.AddSagaDirectBusConsequences(project, subjects, handlerSubject, method, roleEvidence, facts, diagnostics);
        AddTimeoutDiagnostics(diagnostics, roleScope, returnConsequences);
    }

    static void AddTimeoutDiagnostics(
        List<GenerationDiagnostic> diagnostics,
        WolverineSagaRoleScope roleScope,
        IReadOnlyList<WolverineReturnConsequence> returnConsequences)
    {
        var sagaType = roleScope.SagaType;
        var method = roleScope.Role.Method;
        var roleEvidence = roleScope.RoleEvidence;
        var handlerSubject = roleScope.HandlerSubject;
        foreach (var timeout in returnConsequences
                     .Where(consequence => consequence.Kind == WolverineReturnConsequenceKind.Cascade)
                     .Select(consequence => consequence.Type)
                     .OfType<INamedTypeSymbol>()
                     .Where(WolverineReturnConsequences.IsTimeoutMessage)
                     .Distinct(SymbolEqualityComparer.Default))
        {
            diagnostics.Add(new GenerationDiagnostic
            {
                Code = WolverineDiagnosticCodes.DelayedMessageOmitted,
                Severity = GenerationDiagnosticSeverity.Warning,
                Outcome = GenerationDiagnosticOutcome.Unsupported,
                Message = $"Saga handler '{sagaType.Name}.{method.Name}' returns timeout message '{timeout!.Name}', whose delayed delivery cannot be represented by the current Screenplay language",
                Source = roleEvidence.Source,
                Subject = handlerSubject
            });
        }
    }

    static IReadOnlyList<PropertyDefinition> SagaProperties(INamedTypeSymbol sagaType, DotNetProjectCompilation project) =>
    [
        .. sagaType.GetMembers()
            .OfType<IPropertySymbol>()
            .Where(property =>
                !property.IsStatic &&
                !property.IsIndexer &&
                property.DeclaredAccessibility == Accessibility.Public &&
                property.GetMethod?.DeclaredAccessibility == Accessibility.Public &&
                property.Locations.Any(location => IsAuthoredSourceLocation(location, project)))
            .OrderBy(SourceStart)
            .ThenBy(property => property.Name, StringComparer.Ordinal)
            .Select(property => new PropertyDefinition
            {
                Name = LowerFirst(property.Name),
                Type = DotNetTypeShapes.TypeReferenceFor(property.Type)
            })
    ];

    static SourceRange? CompletionInvocation(
        IReadOnlyList<WolverineSagaRoleMethod> roles,
        DotNetProjectCompilation project)
    {
        if (project.Compilation.GetTypeByMetadataName(WellKnownTypes.WolverineSaga) is not { } sagaType)
        {
            return null;
        }

        var invocation = roles
            .SelectMany(role => WolverineMethodSyntax.Declarations(role.Method, project))
            .Select(declaration => FirstMarkCompleted(declaration.Declaration, declaration.SemanticModel, sagaType))
            .FirstOrDefault(_ => _ is not null);
        return invocation is null ? null : CritterStackSource.RangeForProject(invocation.GetLocation(), project);
    }

    static InvocationExpressionSyntax? FirstMarkCompleted(MethodDeclarationSyntax declaration, SemanticModel semanticModel, INamedTypeSymbol sagaType) =>
        declaration
            .DescendantNodes()
            .OfType<InvocationExpressionSyntax>()
            .OrderBy(node => node.SpanStart)
            .FirstOrDefault(node => IsMarkCompleted(semanticModel, node, sagaType));

    static bool IsMarkCompleted(SemanticModel semanticModel, InvocationExpressionSyntax invocation, INamedTypeSymbol sagaType) =>
        semanticModel.GetSymbolInfo(invocation).Symbol is IMethodSymbol invoked &&
        invoked.Name == "MarkCompleted" &&
        SymbolEqualityComparer.Default.Equals(invoked.ContainingType.OriginalDefinition, sagaType.OriginalDefinition);

    static bool IsAdmissibleType(
        INamedTypeSymbol type,
        DotNetProjectCompilation project,
        WolverineHandlerDiscoveryPolicy discovery) =>
        IsEffectivelyPublic(type) &&
        !type.IsAbstract &&
        !type.IsStatic &&
        !type.IsGenericType &&
        type.Locations.Any(location => IsAuthoredSourceLocation(location, project)) &&
        !IsIgnored(type, project) &&
        IsSagaType(type, project) &&
        discovery.Includes(type);

    static bool IsEffectivelyPublic(INamedTypeSymbol type)
    {
        for (var current = type; current is not null; current = current.ContainingType)
        {
            if (current.DeclaredAccessibility != Accessibility.Public)
            {
                return false;
            }
        }

        return true;
    }

    static bool IsAuthoredOrMetadataAttribute(AttributeData attribute, DotNetProjectCompilation project)
    {
        if (attribute.ApplicationSyntaxReference is not { } syntaxReference)
        {
            return true;
        }

        return project.AuthoredSyntaxTrees.Contains(syntaxReference.SyntaxTree) &&
               !DotNetGeneratedSource.IsGenerated(syntaxReference.SyntaxTree);
    }

    static bool IsAuthoredSourceLocation(Location location, DotNetProjectCompilation project) =>
        location.IsInSource &&
        location.SourceTree is not null &&
        project.AuthoredSyntaxTrees.Contains(location.SourceTree) &&
        !DotNetGeneratedSource.IsGenerated(location.SourceTree);

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
        string? targetMember = null,
        string? discriminator = null) => new()
        {
            Id = new FactId { Value = id },
            Subject = source,
            Definition = new RelationshipDefinition
            {
                Key = new RelationshipKey
                {
                    Kind = kind,
                    Source = source,
                    Target = target,
                    Discriminator = discriminator
                },
                TargetMember = targetMember
            },
            Evidence = evidence
        };
}
