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
    string Role);

sealed record WolverineSagaCorrelation(
    string? TargetMember,
    Evidence? Evidence);

static class WolverineSagaFacts
{
    static readonly Dictionary<string, string> _roles = new(StringComparer.Ordinal)
    {
        ["Start"] = "start",
        ["Starts"] = "start",
        ["StartOrHandle"] = "start-or-handle",
        ["StartsOrHandles"] = "start-or-handle",
        ["Orchestrate"] = "orchestrate",
        ["Orchestrates"] = "orchestrate",
        ["Handle"] = "orchestrate",
        ["Handles"] = "orchestrate",
        ["Consume"] = "orchestrate",
        ["Consumes"] = "orchestrate",
        ["NotFound"] = "not-found"
    };

    public static WolverineSagaDiscoveryResult Discover(
        DotNetProjectCompilation project,
        AdapterIdentity adapter,
        WolverineHandlerDiscoveryPolicy discovery)
    {
        var facts = new List<GenerationFact>();
        var diagnostics = new List<GenerationDiagnostic>();
        var catalog = new DotNetArtifactCatalog(project.Compilation);
        foreach (var sagaType in catalog.Types
                     .Where(type => IsAdmissibleType(type, project, discovery))
                     .OrderBy(DotNetSubjectIds.MetadataName, StringComparer.Ordinal))
        {
            var roles = sagaType.GetMembers()
                .OfType<IMethodSymbol>()
                .Select(method => RoleMethod(method, project))
                .OfType<WolverineSagaRoleMethod>()
                .OrderBy(role => SourcePath(role.Method), StringComparer.Ordinal)
                .ThenBy(role => SourceStart(role.Method))
                .ThenBy(role => MethodSignature(role.Method), StringComparer.Ordinal)
                .ToArray();
            if (roles.Length == 0)
            {
                continue;
            }

            AddSagaFacts(project, adapter, sagaType, roles, facts, diagnostics);
        }

        return new(facts, diagnostics);
    }

    public static bool IsSagaType(INamedTypeSymbol type, DotNetProjectCompilation project)
    {
        if (project.Compilation.GetTypeByMetadataName(WellKnownTypes.WolverineSaga) is not { } sagaType)
        {
            return false;
        }

        return IsAuthoredOrMetadataAssignableTo(
            type,
            sagaType,
            project,
            new HashSet<INamedTypeSymbol>(SymbolEqualityComparer.Default));
    }

    internal static SubjectId HandlerSubject(DotNetProjectCompilation project, IMethodSymbol method) => new()
    {
        Value = $"{project.SubjectForType(method.ContainingType).Value}#saga-handler:{Uri.EscapeDataString(MethodSignature(method))}"
    };

    internal static string HandlerName(IMethodSymbol method) =>
        $"{method.ContainingType.Name}.{method.Name}({method.Parameters.FirstOrDefault()?.Type.Name ?? "unknown"})";

    static void AddSagaFacts(
        DotNetProjectCompilation project,
        AdapterIdentity adapter,
        INamedTypeSymbol sagaType,
        IReadOnlyList<WolverineSagaRoleMethod> roles,
        List<GenerationFact> facts,
        List<GenerationDiagnostic> diagnostics)
    {
        var sagaSubject = project.SubjectForType(sagaType);
        var sagaEvidence = CritterStackSource.EvidenceFor(
            sagaType,
            adapter,
            project,
            EvidenceStrength.Exact,
            "Authored public concrete type derived from Wolverine.Saga");
        facts.Add(Artifact(
            $"wolverine:saga:{sagaSubject.Value}",
            sagaSubject,
            ArtifactKind.Saga,
            sagaType.Name,
            sagaEvidence.Source?.Path,
            SagaProperties(sagaType, project),
            sagaEvidence));

        foreach (var role in roles)
        {
            AddRoleFacts(project, adapter, sagaType, role, roles, facts, diagnostics);
        }

        var completion = CompletionInvocation(roles, project);
        diagnostics.Add(new GenerationDiagnostic
        {
            Code = WolverineDiagnosticCodes.SagaWorkflowOmitted,
            Severity = GenerationDiagnosticSeverity.Warning,
            Message = completion is null
                ? $"Saga '{sagaType.Name}' has authored workflow roles, but lifecycle persistence cannot be represented by the current Screenplay language"
                : $"Saga '{sagaType.Name}' invokes Wolverine.Saga.MarkCompleted(), but conditional completion and lifecycle persistence cannot be represented by the current Screenplay language",
            Source = completion ?? sagaEvidence.Source,
            Subject = sagaSubject
        });
    }

    static void AddRoleFacts(
        DotNetProjectCompilation project,
        AdapterIdentity adapter,
        INamedTypeSymbol sagaType,
        WolverineSagaRoleMethod role,
        IReadOnlyList<WolverineSagaRoleMethod> allRoles,
        List<GenerationFact> facts,
        List<GenerationDiagnostic> diagnostics)
    {
        var method = role.Method;
        var handlerSubject = HandlerSubject(project, method);
        var messageSubject = project.SubjectForType(role.MessageType);
        var roleEvidence = CritterStackSource.EvidenceFor(
            method,
            adapter,
            project,
            EvidenceStrength.Conventional,
            $"Authored Wolverine saga {role.Role} role method");
        var correlation = CorrelationFor(project, adapter, sagaType, role.MessageType, allRoles);
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
            DotNetTypeShapes.PropertiesOf(role.MessageType),
            relationshipEvidence));
        facts.Add(Relationship(
            $"wolverine:saga-handles:{handlerSubject.Value}:{messageSubject.Value}:{discriminator}",
            handlerSubject,
            RelationshipKind.Handles,
            messageSubject,
            relationshipEvidence,
            targetMember: correlation.TargetMember,
            discriminator: discriminator));

        if (correlation.TargetMember is null)
        {
            diagnostics.Add(new GenerationDiagnostic
            {
                Code = WolverineDiagnosticCodes.SagaCorrelationRuntime,
                Severity = GenerationDiagnosticSeverity.Information,
                Message = $"Saga handler '{sagaType.Name}.{method.Name}' has no authored message correlation member for '{role.MessageType.Name}'; Wolverine must use runtime envelope correlation",
                Source = roleEvidence.Source,
                Subject = handlerSubject
            });
        }

        var returnConsequences = WolverineReturnConsequences.Classify(
            method,
            project,
            isHttpEndpoint: false,
            aggregateWorkflow: false,
            hasEventStream: false);
        WolverineFacts.AddSagaReturnConsequences(project, handlerSubject, returnConsequences, roleEvidence, facts);
        WolverineFacts.AddSagaOutgoingMessages(
            project,
            handlerSubject,
            method,
            WolverineFacts.DiscoverSagaOutgoingMessages(method, project),
            roleEvidence,
            facts,
            diagnostics);
        WolverineFacts.AddSagaDirectBusConsequences(project, handlerSubject, method, roleEvidence, facts, diagnostics);

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
                Message = $"Saga handler '{sagaType.Name}.{method.Name}' returns timeout message '{timeout!.Name}', whose delayed delivery cannot be represented by the current Screenplay language",
                Source = roleEvidence.Source,
                Subject = handlerSubject
            });
        }
    }

    static WolverineSagaRoleMethod? RoleMethod(IMethodSymbol method, DotNetProjectCompilation project)
    {
        if (method.DeclaredAccessibility != Accessibility.Public ||
            method.MethodKind != MethodKind.Ordinary ||
            method.IsGenericMethod ||
            method.Parameters.Length == 0 ||
            IsIgnored(method) ||
            !WolverineMethodSyntax.Declarations(method, project).Any())
        {
            return null;
        }

        var roleName = method.Name.EndsWith("Async", StringComparison.Ordinal)
            ? method.Name[..^"Async".Length]
            : method.Name;
        if (!_roles.TryGetValue(roleName, out var role) ||
            (method.IsStatic && role is not "start" and not "not-found") ||
            method.Parameters[0].Type is not INamedTypeSymbol messageType ||
            !WolverineFacts.IsSagaMessagePayloadType(messageType) ||
            IsSagaType(messageType, project))
        {
            return null;
        }

        return new(method, messageType, role);
    }

    static WolverineSagaCorrelation CorrelationFor(
        DotNetProjectCompilation project,
        AdapterIdentity adapter,
        INamedTypeSymbol sagaType,
        INamedTypeSymbol messageType,
        IReadOnlyList<WolverineSagaRoleMethod> allRoles)
    {
        var members = MessageMembers(messageType, project).ToArray();
        if (members.FirstOrDefault(member => DotNetSymbols.HasAttribute(member, WellKnownTypes.WolverineSagaIdentityAttribute)) is { } attributed)
        {
            return CorrelationFromMember(project, adapter, attributed, EvidenceStrength.Exact, "Exact Wolverine [SagaIdentity] message member");
        }

        var specified = allRoles
            .Where(role => SymbolEqualityComparer.Default.Equals(role.MessageType, messageType))
            .Select(role => SagaIdentityFrom(role.Method.Parameters[0]))
            .FirstOrDefault(candidate => candidate is not null);
        if (specified is not null && members.FirstOrDefault(member => string.Equals(member.Name, specified.Value.MemberName, StringComparison.Ordinal)) is { } specifiedMember)
        {
            var evidence = CritterStackSource.EvidenceFor(
                specified.Value.Parameter,
                adapter,
                project,
                EvidenceStrength.Exact,
                "Exact Wolverine [SagaIdentityFrom] handler parameter");
            return new(LowerFirst(specifiedMember.Name), evidence);
        }

        var expectedNames = new[]
        {
            $"{sagaType.Name}Id",
            $"{sagaType.Name.Replace("Saga", string.Empty, StringComparison.InvariantCultureIgnoreCase)}Id",
            "SagaId"
        };
        foreach (var expectedName in expectedNames.Distinct(StringComparer.Ordinal))
        {
            if (members.FirstOrDefault(member => string.Equals(member.Name, expectedName, StringComparison.Ordinal)) is { } conventional)
            {
                return CorrelationFromMember(
                    project,
                    adapter,
                    conventional,
                    EvidenceStrength.Conventional,
                    $"Wolverine saga correlation member convention '{expectedName}'");
            }
        }

        if (members.FirstOrDefault(member => string.Equals(member.Name, "Id", StringComparison.OrdinalIgnoreCase)) is { } id)
        {
            return CorrelationFromMember(
                project,
                adapter,
                id,
                EvidenceStrength.Conventional,
                "Wolverine case-insensitive Id saga correlation convention");
        }

        return new(null, null);
    }

    static (string MemberName, IParameterSymbol Parameter)? SagaIdentityFrom(IParameterSymbol parameter)
    {
        var attribute = parameter.GetAttributes().FirstOrDefault(candidate =>
            candidate.AttributeClass is not null &&
            DotNetSubjectIds.MetadataName(candidate.AttributeClass.OriginalDefinition) == WellKnownTypes.WolverineSagaIdentityFromAttribute);
        return attribute?.ConstructorArguments.FirstOrDefault().Value is string memberName
            ? (memberName, parameter)
            : null;
    }

    static WolverineSagaCorrelation CorrelationFromMember(
        DotNetProjectCompilation project,
        AdapterIdentity adapter,
        ISymbol member,
        EvidenceStrength strength,
        string explanation) => new(
            LowerFirst(member.Name),
            CritterStackSource.EvidenceFor(member, adapter, project, strength, explanation));

    static IEnumerable<ISymbol> MessageMembers(INamedTypeSymbol messageType, DotNetProjectCompilation project) =>
        messageType.GetMembers()
            .OfType<IFieldSymbol>()
            .Where(member => IsPublicAuthoredMember(member, project))
            .Cast<ISymbol>()
            .Concat(messageType.GetMembers()
                .OfType<IPropertySymbol>()
                .Where(member => !member.IsIndexer && IsPublicAuthoredMember(member, project)));

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

        foreach (var role in roles)
        {
            foreach (var (declaration, semanticModel) in WolverineMethodSyntax.Declarations(role.Method, project))
            {
                foreach (var invocation in declaration.DescendantNodes().OfType<InvocationExpressionSyntax>().OrderBy(node => node.SpanStart))
                {
                    if (semanticModel.GetSymbolInfo(invocation).Symbol is IMethodSymbol invoked &&
                        invoked.Name == "MarkCompleted" &&
                        SymbolEqualityComparer.Default.Equals(invoked.ContainingType.OriginalDefinition, sagaType.OriginalDefinition))
                    {
                        return CritterStackSource.RangeForProject(invocation.GetLocation(), project);
                    }
                }
            }
        }

        return null;
    }

    static bool IsAdmissibleType(
        INamedTypeSymbol type,
        DotNetProjectCompilation project,
        WolverineHandlerDiscoveryPolicy discovery) =>
        IsEffectivelyPublic(type) &&
        !type.IsAbstract &&
        !type.IsStatic &&
        !type.IsGenericType &&
        type.Locations.Any(location => IsAuthoredSourceLocation(location, project)) &&
        !IsIgnored(type) &&
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

    static bool IsAuthoredOrMetadataAssignableTo(
        INamedTypeSymbol type,
        INamedTypeSymbol target,
        DotNetProjectCompilation project,
        HashSet<INamedTypeSymbol> visited)
    {
        if (!visited.Add(type))
        {
            return false;
        }

        if (SymbolEqualityComparer.Default.Equals(type.OriginalDefinition, target.OriginalDefinition))
        {
            return true;
        }

        if (type.DeclaringSyntaxReferences.Length == 0)
        {
            return type.BaseType is not null && IsAuthoredOrMetadataAssignableTo(type.BaseType, target, project, visited);
        }

        foreach (var syntaxReference in type.DeclaringSyntaxReferences)
        {
            if (syntaxReference.GetSyntax() is not TypeDeclarationSyntax { BaseList: not null } declaration ||
                !project.AuthoredSyntaxTrees.Contains(declaration.SyntaxTree) ||
                DotNetGeneratedSource.IsGenerated(declaration.SyntaxTree))
            {
                continue;
            }

            var semanticModel = project.Compilation.GetSemanticModel(declaration.SyntaxTree);
            foreach (var baseType in declaration.BaseList.Types)
            {
                if (semanticModel.GetTypeInfo(baseType.Type).Type is INamedTypeSymbol candidate &&
                    IsAuthoredOrMetadataAssignableTo(candidate, target, project, visited))
                {
                    return true;
                }
            }
        }

        return false;
    }

    static bool IsIgnored(ISymbol symbol) =>
        DotNetSymbols.HasAttribute(symbol, WellKnownTypes.WolverineIgnoreAttribute) ||
        DotNetSymbols.HasAttribute(symbol, WellKnownTypes.WolverineLegacyIgnoreAttribute);

    static bool IsPublicAuthoredMember(ISymbol member, DotNetProjectCompilation project) =>
        !member.IsStatic &&
        member.DeclaredAccessibility == Accessibility.Public &&
        member.Locations.Any(location => IsAuthoredSourceLocation(location, project));

    static bool IsAuthoredSourceLocation(Location location, DotNetProjectCompilation project) =>
        location.IsInSource &&
        location.SourceTree is not null &&
        project.AuthoredSyntaxTrees.Contains(location.SourceTree) &&
        !DotNetGeneratedSource.IsGenerated(location.SourceTree);

    static string MethodSignature(IMethodSymbol method) =>
        method.GetDocumentationCommentId() ?? method.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);

    static string? SourceFileOf(ISymbol symbol, DotNetProjectCompilation project) =>
        CritterStackSource.EvidenceFor(symbol, new AdapterIdentity { Id = "source", Version = "1" }, project, EvidenceStrength.Exact).Source?.Path;

    static string SourcePath(ISymbol symbol) => symbol.Locations
        .Where(location => location.IsInSource)
        .Select(location => location.SourceTree?.FilePath ?? string.Empty)
        .Order(StringComparer.Ordinal)
        .FirstOrDefault() ?? string.Empty;

    static int SourceStart(ISymbol symbol) => symbol.Locations
        .Where(location => location.IsInSource)
        .Select(location => location.SourceSpan.Start)
        .DefaultIfEmpty(int.MaxValue)
        .Min();

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

    static string LowerFirst(string value) => value.Length == 0
        ? value
        : $"{char.ToLowerInvariant(value[0])}{value[1..]}";
}
