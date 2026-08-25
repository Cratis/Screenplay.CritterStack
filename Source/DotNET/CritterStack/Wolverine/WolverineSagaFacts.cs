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
            WolverineSagaRoleAdmission[] admissions =
            [
                .. sagaType.GetMembers()
                    .OfType<IMethodSymbol>()
                    .Select(method => RoleMethod(method, project, adapter))
                    .OrderBy(admission => SourcePath(admission.Method), StringComparer.Ordinal)
                    .ThenBy(admission => SourceStart(admission.Method))
                    .ThenBy(admission => MethodSignature(admission.Method), StringComparer.Ordinal)
            ];
            diagnostics.AddRange(admissions.Select(_ => _.Diagnostic).OfType<GenerationDiagnostic>());

            WolverineSagaRoleMethod[] candidateRoles = [.. admissions.Select(_ => _.Role).OfType<WolverineSagaRoleMethod>()];
            var roles = new List<WolverineSagaRoleMethod>();
            foreach (var messageGroup in candidateRoles
                         .GroupBy<WolverineSagaRoleMethod, INamedTypeSymbol>(_ => _.MessageType, SymbolEqualityComparer.Default)
                         .OrderBy(_ => DotNetSubjectIds.MetadataName(_.Key), StringComparer.Ordinal))
            {
                var groupedRoles = messageGroup.ToArray();
                if (!groupedRoles.Any(EstablishesSagaChain))
                {
                    diagnostics.AddRange(groupedRoles.Select(role => RejectedRole(
                        role.Method,
                        project,
                        adapter,
                        "no other exact lifecycle method for the same message establishes a Wolverine SagaChain").Diagnostic!));
                    continue;
                }

                if (RequiresPublicParameterlessConstructor(groupedRoles) &&
                    !HasPublicParameterlessConstructor(sagaType))
                {
                    var isNotFoundOnlyChain = IsNotFoundOnlyChain(groupedRoles);
                    var rejectedRoles = groupedRoles.Where(role => role.IsCreationCapable || isNotFoundOnlyChain).ToArray();
                    diagnostics.AddRange(rejectedRoles.Select(role => RejectedRole(
                        role.Method,
                        project,
                        adapter,
                        "Wolverine must create saga state, but the saga has no accessible public parameterless constructor and no exact returned saga supplies creation").Diagnostic!));
                    groupedRoles = [.. groupedRoles.Except(rejectedRoles)];
                }

                if (!groupedRoles.Any(EstablishesSagaChain))
                {
                    diagnostics.AddRange(groupedRoles.Select(role => RejectedRole(
                        role.Method,
                        project,
                        adapter,
                        "the remaining methods for the same message do not establish a Wolverine SagaChain").Diagnostic!));
                    continue;
                }

                roles.AddRange(groupedRoles);
            }

            if (roles.Count > 0)
            {
                AddSagaFacts(project, adapter, sagaType, roles, facts, diagnostics);
            }
        }

        return new(facts, diagnostics);
    }

    public static bool IsSagaType(INamedTypeSymbol type, DotNetProjectCompilation project) =>
        WolverineSagaTypes.IsSagaState(type, project);

    internal static SubjectId HandlerSubject(DotNetProjectCompilation project, IMethodSymbol method) =>
        DotNetMethodIdentity.SubjectFor(project, method);

    internal static string HandlerName(IMethodSymbol method) =>
        DotNetMethodIdentity.DisplayName(method);

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

        if (correlation.TargetMember is null)
        {
            diagnostics.Add(new GenerationDiagnostic
            {
                Code = WolverineDiagnosticCodes.SagaCorrelationRuntime,
                Severity = GenerationDiagnosticSeverity.Information,
                Outcome = GenerationDiagnosticOutcome.Unknown,
                Message = correlation.UnresolvedReason ?? $"Saga handler '{sagaType.Name}.{method.Name}' has no authored message correlation member for '{role.MessageType.Name}'; correlation remains runtime-resolved",
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
                Outcome = GenerationDiagnosticOutcome.Unsupported,
                Message = $"Saga handler '{sagaType.Name}.{method.Name}' returns timeout message '{timeout!.Name}', whose delayed delivery cannot be represented by the current Screenplay language",
                Source = roleEvidence.Source,
                Subject = handlerSubject
            });
        }
    }

    static WolverineSagaRoleAdmission RoleMethod(
        IMethodSymbol method,
        DotNetProjectCompilation project,
        AdapterIdentity adapter)
    {
        var roleName = method.Name.EndsWith("Async", StringComparison.Ordinal)
            ? method.Name[..^"Async".Length]
            : method.Name;
        if (!_roles.TryGetValue(roleName, out var role) ||
            IsIgnored(method, project) ||
            !WolverineMethodSyntax.Declarations(method, project).Any())
        {
            return new(method, null, null);
        }

        if (method.DeclaredAccessibility != Accessibility.Public ||
            method.MethodKind != MethodKind.Ordinary ||
            method.IsGenericMethod ||
            method.Parameters.Length == 0)
        {
            return RejectedRole(method, project, adapter, "the method must be a public, ordinary, non-generic method with a message parameter");
        }

        if (IsPrimitiveReturn(method.ReturnType))
        {
            return RejectedRole(method, project, adapter, "Wolverine handler discovery rejects methods that directly return a primitive type");
        }

        if (method.Parameters[0].Type is not INamedTypeSymbol messageType ||
            !WolverineFacts.IsSagaMessagePayloadType(messageType) ||
            IsSagaType(messageType, project))
        {
            return RejectedRole(method, project, adapter, "the first parameter must be an authored non-saga message payload");
        }

        var isStart = string.Equals(role, "start", StringComparison.Ordinal);
        var isStartOrHandle = string.Equals(role, "start-or-handle", StringComparison.Ordinal);
        var isNotFound = string.Equals(role, "not-found", StringComparison.Ordinal);
        var isLegalShape = isStart || (isNotFound ? method.IsStatic : !method.IsStatic);
        if (!isLegalShape)
        {
            var reason = "an existing-state role must be an instance method";
            if (isStartOrHandle)
            {
                reason = "a start-or-handle role must be an instance method";
            }
            else if (isNotFound)
            {
                reason = "a not-found role must be a static method";
            }
            return RejectedRole(method, project, adapter, reason);
        }

        return new(method, new(method, messageType, role, isStart || isStartOrHandle), null);
    }

    static bool EstablishesSagaChain(WolverineSagaRoleMethod role) =>
        !role.Method.IsStatic ||
        string.Equals(role.Method.Name, "Start", StringComparison.Ordinal) ||
        string.Equals(role.Method.Name, "StartAsync", StringComparison.Ordinal) ||
        string.Equals(role.Method.Name, "NotFound", StringComparison.Ordinal);

    static bool RequiresPublicParameterlessConstructor(IReadOnlyList<WolverineSagaRoleMethod> roles)
    {
        if (IsNotFoundOnlyChain(roles))
        {
            return true;
        }

        var creationRoles = roles.Where(_ => _.IsCreationCapable).ToArray();
        if (creationRoles.Length == 0)
        {
            return false;
        }

        if (creationRoles.Any(_ => !_.Method.IsStatic) ||
            roles.Any(_ =>
                string.Equals(_.Role, "start-or-handle", StringComparison.Ordinal) ||
                string.Equals(_.Role, "orchestrate", StringComparison.Ordinal)))
        {
            return true;
        }

        return !creationRoles.Any(ReturnsExactSaga);
    }

    static bool IsNotFoundOnlyChain(IReadOnlyList<WolverineSagaRoleMethod> roles) =>
        roles.Count > 0 && roles.All(_ => string.Equals(_.Role, "not-found", StringComparison.Ordinal));

    static bool ReturnsExactSaga(WolverineSagaRoleMethod role) =>
        WolverineReturnTypes.CreatedValues(role.Method)
            .OfType<INamedTypeSymbol>()
            .Any(type => SymbolEqualityComparer.Default.Equals(type.OriginalDefinition, role.Method.ContainingType.OriginalDefinition));

    static bool HasPublicParameterlessConstructor(INamedTypeSymbol sagaType) =>
        sagaType.InstanceConstructors.Any(constructor =>
            constructor.Parameters.Length == 0 &&
            constructor.DeclaredAccessibility == Accessibility.Public);

    static bool IsPrimitiveReturn(ITypeSymbol returnType) => returnType.SpecialType is
        SpecialType.System_Boolean or
        SpecialType.System_Byte or
        SpecialType.System_SByte or
        SpecialType.System_Int16 or
        SpecialType.System_UInt16 or
        SpecialType.System_Int32 or
        SpecialType.System_UInt32 or
        SpecialType.System_Int64 or
        SpecialType.System_UInt64 or
        SpecialType.System_Char or
        SpecialType.System_Double or
        SpecialType.System_Single or
        SpecialType.System_IntPtr or
        SpecialType.System_UIntPtr;

    static WolverineSagaRoleAdmission RejectedRole(
        IMethodSymbol method,
        DotNetProjectCompilation project,
        AdapterIdentity adapter,
        string reason)
    {
        var evidence = CritterStackSource.EvidenceFor(
            method,
            adapter,
            project,
            EvidenceStrength.Exact,
            "Authored Wolverine saga lifecycle method outside the admitted role shape");
        return new(method, null, new GenerationDiagnostic
        {
            Code = WolverineDiagnosticCodes.SagaRoleUnresolved,
            Severity = GenerationDiagnosticSeverity.Warning,
            Outcome = GenerationDiagnosticOutcome.Unknown,
            Message = $"Wolverine saga role '{DotNetMethodIdentity.DisplayName(method)}' was not admitted because {reason}",
            Source = evidence.Source,
            Subject = DotNetMethodIdentity.SubjectFor(project, method)
        });
    }

    static WolverineSagaCorrelation CorrelationFor(
        DotNetProjectCompilation project,
        AdapterIdentity adapter,
        INamedTypeSymbol sagaType,
        INamedTypeSymbol messageType,
        IReadOnlyList<WolverineSagaRoleMethod> allRoles)
    {
        var members = MessageMembers(messageType, project).ToArray();
        var attributed = MatchMember(members, member => DotNetSymbols.HasAttribute(member, WellKnownTypes.WolverineSagaIdentityAttribute));
        if (attributed.IsAmbiguous)
        {
            return UnresolvedCorrelation(sagaType, messageType, "multiple public [SagaIdentity] members are visible");
        }
        if (attributed.Member is not null)
        {
            return CorrelationFromMember(project, adapter, attributed.Member, EvidenceStrength.Exact, "Exact Wolverine [SagaIdentity] message member");
        }

        var specified = allRoles
            .Where(role => SymbolEqualityComparer.Default.Equals(role.MessageType, messageType))
            .SelectMany(role => role.Method.Parameters.Select(SagaIdentityFrom))
            .OfType<(string MemberName, IParameterSymbol Parameter)>()
            .ToArray();
        var specifiedNames = specified.Select(_ => _.MemberName).Distinct(StringComparer.Ordinal).ToArray();
        if (specifiedNames.Length > 1)
        {
            var evidence = SagaIdentityFromEvidence(project, adapter, specified[0].Parameter);
            return UnresolvedCorrelation(
                sagaType,
                messageType,
                "conflicting [SagaIdentityFrom] member names are declared across parameters in the same message chain",
                evidence);
        }

        if (specifiedNames.Length == 1)
        {
            var specifiedMember = MatchMember(members, member => string.Equals(member.Name, specifiedNames[0], StringComparison.Ordinal));
            if (specifiedMember.IsAmbiguous)
            {
                return UnresolvedCorrelation(sagaType, messageType, $"multiple public members match explicit [SagaIdentityFrom] name '{specifiedNames[0]}'");
            }
            if (specifiedMember.Member is not null)
            {
                return new(
                    LowerFirst(specifiedMember.Member.Name),
                    SagaIdentityFromEvidence(project, adapter, specified[0].Parameter));
            }
        }
        else
        {
            var fullSagaName = $"{sagaType.Name}Id";
            var fullNameMember = MatchMember(members, member => string.Equals(member.Name, fullSagaName, StringComparison.Ordinal));
            if (fullNameMember.IsAmbiguous)
            {
                return UnresolvedCorrelation(sagaType, messageType, $"multiple public members match Wolverine convention '{fullSagaName}'");
            }
            if (fullNameMember.Member is not null)
            {
                return CorrelationFromMember(project, adapter, fullNameMember.Member, EvidenceStrength.Conventional, $"Wolverine saga correlation member convention '{fullSagaName}'");
            }
        }

        var suffixStrippedName = $"{sagaType.Name.Replace("Saga", string.Empty, StringComparison.InvariantCultureIgnoreCase)}Id";
        foreach (var expectedName in new[] { suffixStrippedName, "SagaId" }.Distinct(StringComparer.Ordinal))
        {
            var conventional = MatchMember(members, member => string.Equals(member.Name, expectedName, StringComparison.Ordinal));
            if (conventional.IsAmbiguous)
            {
                return UnresolvedCorrelation(sagaType, messageType, $"multiple public members match Wolverine convention '{expectedName}'");
            }
            if (conventional.Member is not null)
            {
                return CorrelationFromMember(
                    project,
                    adapter,
                    conventional.Member,
                    EvidenceStrength.Conventional,
                    $"Wolverine saga correlation member convention '{expectedName}'");
            }
        }

        var id = MatchMember(members, member => string.Equals(member.Name, "Id", StringComparison.OrdinalIgnoreCase));
        if (id.IsAmbiguous)
        {
            return UnresolvedCorrelation(sagaType, messageType, "multiple public members match Wolverine's case-insensitive Id convention");
        }
        if (id.Member is not null)
        {
            return CorrelationFromMember(
                project,
                adapter,
                id.Member,
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

    static Evidence SagaIdentityFromEvidence(
        DotNetProjectCompilation project,
        AdapterIdentity adapter,
        IParameterSymbol parameter) => CritterStackSource.EvidenceFor(
            parameter,
            adapter,
            project,
            EvidenceStrength.Exact,
            "Exact Wolverine [SagaIdentityFrom] handler parameter");

    static WolverineSagaCorrelation CorrelationFromMember(
        DotNetProjectCompilation project,
        AdapterIdentity adapter,
        ISymbol member,
        EvidenceStrength strength,
        string explanation) => new(
            LowerFirst(member.Name),
            CritterStackSource.EvidenceFor(member, adapter, project, strength, explanation));

    static WolverineSagaCorrelation UnresolvedCorrelation(
        INamedTypeSymbol sagaType,
        INamedTypeSymbol messageType,
        string reason,
        Evidence? evidence = null) => new(
            null,
            evidence,
            $"Saga handler for '{sagaType.Name}' cannot safely select an authored correlation member for '{messageType.Name}' because {reason}; correlation remains runtime-resolved");

    static WolverineSagaMemberMatch MatchMember(IEnumerable<ISymbol> members, Func<ISymbol, bool> predicate)
    {
        var matches = members.Where(predicate).Take(2).ToArray();
        return new(matches.Length == 1 ? matches[0] : null, matches.Length > 1);
    }

    static IEnumerable<ISymbol> MessageMembers(INamedTypeSymbol messageType, DotNetProjectCompilation project)
    {
        var hierarchy = TypeHierarchy(messageType).ToArray();
        return hierarchy
            .SelectMany(type => type.GetMembers().OfType<IFieldSymbol>())
            .Where(member => IsPublicAuthoredMember(member, project))
            .Cast<ISymbol>()
            .Concat(hierarchy
                .SelectMany(type => type.GetMembers().OfType<IPropertySymbol>())
                .Where(member => !member.IsIndexer && IsPublicAuthoredMember(member, project)));
    }

    static IEnumerable<INamedTypeSymbol> TypeHierarchy(INamedTypeSymbol type)
    {
        for (var current = type; current is not null; current = current.BaseType)
        {
            yield return current;
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

    static bool IsIgnored(ISymbol symbol, DotNetProjectCompilation project) => symbol.GetAttributes().Any(attribute =>
        attribute.AttributeClass is not null &&
        (DotNetSubjectIds.MetadataName(attribute.AttributeClass.OriginalDefinition) == WellKnownTypes.WolverineIgnoreAttribute ||
         DotNetSubjectIds.MetadataName(attribute.AttributeClass.OriginalDefinition) == WellKnownTypes.WolverineLegacyIgnoreAttribute) &&
        IsAuthoredOrMetadataAttribute(attribute, project));

    static bool IsAuthoredOrMetadataAttribute(AttributeData attribute, DotNetProjectCompilation project)
    {
        if (attribute.ApplicationSyntaxReference is not { } syntaxReference)
        {
            return true;
        }

        return project.AuthoredSyntaxTrees.Contains(syntaxReference.SyntaxTree) &&
               !DotNetGeneratedSource.IsGenerated(syntaxReference.SyntaxTree);
    }

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
