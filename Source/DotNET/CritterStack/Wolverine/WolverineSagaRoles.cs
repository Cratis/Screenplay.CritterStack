// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Cratis.Screenplay.Generation;
using Cratis.Screenplay.Generation.DotNet;
using Microsoft.CodeAnalysis;

namespace Cratis.CritterStack.Screenplay.Wolverine;

sealed record WolverineSagaChainScope(
    INamedTypeSymbol SagaType,
    DotNetProjectCompilation Project,
    AdapterIdentity Adapter,
    List<GenerationDiagnostic> Diagnostics);

sealed record WolverineSagaRoleSet(
    IReadOnlyList<WolverineSagaRoleMethod> Roles,
    IReadOnlyList<GenerationDiagnostic> Diagnostics);

/// <summary>
/// Admits the lifecycle methods of a saga type: which methods are roles, why a method is rejected, and which messages form a
/// saga chain Wolverine can actually run.
/// </summary>
static class WolverineSagaRoles
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

    public static WolverineSagaRoleSet Admit(INamedTypeSymbol sagaType, DotNetProjectCompilation project, AdapterIdentity adapter)
    {
        WolverineSagaRoleAdmission[] admissions =
        [
            .. sagaType.GetMembers()
                .OfType<IMethodSymbol>()
                .Select(method => RoleMethod(method, project, adapter))
                .OrderBy(admission => WolverineSagaFacts.SourcePath(admission.Method), StringComparer.Ordinal)
                .ThenBy(admission => WolverineSagaFacts.SourceStart(admission.Method))
                .ThenBy(admission => WolverineSagaFacts.MethodSignature(admission.Method), StringComparer.Ordinal)
        ];
        List<GenerationDiagnostic> diagnostics = [.. admissions.Select(_ => _.Diagnostic).OfType<GenerationDiagnostic>()];

        WolverineSagaRoleMethod[] candidateRoles = [.. admissions.Select(_ => _.Role).OfType<WolverineSagaRoleMethod>()];
        List<WolverineSagaRoleMethod> roles = [];
        foreach (var messageGroup in candidateRoles
                     .GroupBy<WolverineSagaRoleMethod, INamedTypeSymbol>(_ => _.MessageType, SymbolEqualityComparer.Default)
                     .OrderBy(_ => DotNetSubjectIds.MetadataName(_.Key), StringComparer.Ordinal))
        {
            roles.AddRange(AdmitChain([.. messageGroup], new(sagaType, project, adapter, diagnostics)));
        }

        return new(roles, diagnostics);
    }

    static WolverineSagaRoleMethod[] AdmitChain(WolverineSagaRoleMethod[] groupedRoles, WolverineSagaChainScope scope)
    {
        if (!groupedRoles.Any(EstablishesSagaChain))
        {
            Reject(groupedRoles, "no other exact lifecycle method for the same message establishes a Wolverine SagaChain");
            return [];
        }

        if (RequiresPublicParameterlessConstructor(groupedRoles) &&
            !HasPublicParameterlessConstructor(scope.SagaType))
        {
            var isNotFoundOnlyChain = IsNotFoundOnlyChain(groupedRoles);
            var rejectedRoles = groupedRoles.Where(role => role.IsCreationCapable || isNotFoundOnlyChain).ToArray();
            Reject(rejectedRoles, "Wolverine must create saga state, but the saga has no accessible public parameterless constructor and no exact returned saga supplies creation");
            groupedRoles = [.. groupedRoles.Except(rejectedRoles)];
        }

        if (!groupedRoles.Any(EstablishesSagaChain))
        {
            Reject(groupedRoles, "the remaining methods for the same message do not establish a Wolverine SagaChain");
            return [];
        }

        return groupedRoles;

        void Reject(IEnumerable<WolverineSagaRoleMethod> rejected, string reason) =>
            scope.Diagnostics.AddRange(rejected.Select(role => RejectedRole(role.Method, scope.Project, scope.Adapter, reason).Diagnostic!));
    }

    static WolverineSagaRoleAdmission RoleMethod(
        IMethodSymbol method,
        DotNetProjectCompilation project,
        AdapterIdentity adapter)
    {
        var roleName = method.Name.EndsWith("Async", StringComparison.Ordinal)
            ? method.Name[..^"Async".Length]
            : method.Name;
        if (!_roles.TryGetValue(roleName, out var role) || !IsDiscoverable(method, project))
        {
            return new(method, null, null);
        }

        if (RejectionReason(method, role, project, out var messageType) is { } reason)
        {
            return RejectedRole(method, project, adapter, reason);
        }

        var isCreationCapable = string.Equals(role, "start", StringComparison.Ordinal) || string.Equals(role, "start-or-handle", StringComparison.Ordinal);
        return new(method, new(method, messageType!, role, isCreationCapable), null);
    }

    static bool IsDiscoverable(IMethodSymbol method, DotNetProjectCompilation project) =>
        !WolverineSagaFacts.IsIgnored(method, project) &&
        WolverineMethodSyntax.Declarations(method, project).Any();

    static string? RejectionReason(
        IMethodSymbol method,
        string role,
        DotNetProjectCompilation project,
        out INamedTypeSymbol? messageType)
    {
        messageType = null;
        if (!IsOrdinaryMessageMethod(method))
        {
            return "the method must be a public, ordinary, non-generic method with a message parameter";
        }

        if (IsPrimitiveReturn(method.ReturnType))
        {
            return "Wolverine handler discovery rejects methods that directly return a primitive type";
        }

        if (method.Parameters[0].Type is not INamedTypeSymbol firstParameter || !IsMessagePayload(firstParameter, project))
        {
            return "the first parameter must be an authored non-saga message payload";
        }

        messageType = firstParameter;
        return ShapeRejection(method, role);
    }

    static bool IsOrdinaryMessageMethod(IMethodSymbol method) =>
        method.DeclaredAccessibility == Accessibility.Public &&
        method.MethodKind == MethodKind.Ordinary &&
        !method.IsGenericMethod &&
        method.Parameters.Length > 0;

    static bool IsMessagePayload(INamedTypeSymbol type, DotNetProjectCompilation project) =>
        WolverineFacts.IsSagaMessagePayloadType(type) && !WolverineSagaFacts.IsSagaType(type, project);

    static string? ShapeRejection(IMethodSymbol method, string role) => role switch
    {
        "start" => null,
        "not-found" => method.IsStatic ? null : "a not-found role must be a static method",
        "start-or-handle" => method.IsStatic ? "a start-or-handle role must be an instance method" : null,
        _ => method.IsStatic ? "an existing-state role must be an instance method" : null
    };

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

        if (creationRoles.Any(_ => !_.Method.IsStatic) || roles.Any(IsStatefulRole))
        {
            return true;
        }

        return !creationRoles.Any(ReturnsExactSaga);
    }

    static bool IsStatefulRole(WolverineSagaRoleMethod role) =>
        string.Equals(role.Role, "start-or-handle", StringComparison.Ordinal) ||
        string.Equals(role.Role, "orchestrate", StringComparison.Ordinal);

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
}
