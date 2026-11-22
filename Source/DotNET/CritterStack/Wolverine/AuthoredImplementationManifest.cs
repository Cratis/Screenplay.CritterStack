// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Cratis.Screenplay.Generation;
using Cratis.Screenplay.Generation.DotNet;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using static Cratis.CritterStack.Screenplay.Wolverine.AuthoredCalls;
using static Cratis.CritterStack.Screenplay.Wolverine.AuthoredSymbolKeys;
using static Cratis.CritterStack.Screenplay.Wolverine.AuthoredTypeBinding;

namespace Cratis.CritterStack.Screenplay.Wolverine;

/// <summary>
/// Indexes selected-project dispatch once using assembly-qualified metadata identities, not compilation-local symbols.
/// </summary>
sealed class AuthoredImplementationManifest
{
    const int MaximumDepth = 8;
    const int MaximumCalls = 128;
    const string LimitReason = "the authored traversal reached its depth or call-count limit";

    readonly IReadOnlyList<DotNetProjectCompilation> _projects;
    readonly Dictionary<string, List<AuthoredImplementation>> _methods = [with(StringComparer.Ordinal)];
    readonly Dictionary<string, List<AuthoredDispatch>> _dispatch = [with(StringComparer.Ordinal)];
    readonly HashSet<AssemblyIdentity> _incompleteAssemblies;

    public AuthoredImplementationManifest(DotNetAnalysisContext context)
    {
        _projects = context.Projects;
        _incompleteAssemblies = IncompleteAssembliesOf(_projects);
        foreach (var project in _projects)
        {
            foreach (var type in TypesIn(project.Compilation.Assembly.GlobalNamespace))
            {
                IndexType(project, type);
            }
        }
    }

    public IReadOnlyList<WolverineProduction> SessionAppends(
        IMethodSymbol method,
        IParameterSymbol? request,
        DotNetProjectCompilation project,
        List<GenerationDiagnostic> diagnostics)
    {
        var result = new List<WolverineProduction>();
        var arguments = InitialArguments(request);
        var budget = new AuthoredTraversalBudget();
        var failed = false;
        foreach (var call in CallsIn(method, project).Where(call => IsApplicationCall(call.Operation.TargetMethod)))
        {
            var traversal = new AuthoredTraversal(diagnostics, budget, SourceOf(call), [MethodKey(method)]);
            result.AddRange(FollowFromRoot(method, project, call, new(arguments, traversal, [], Conditional: false, Depth: 0)));
            failed |= traversal.Failed;
        }

        return failed ? [] : result;
    }

    static HashSet<AssemblyIdentity> IncompleteAssembliesOf(IReadOnlyList<DotNetProjectCompilation> projects)
    {
        var selectedAssemblies = projects.Select(project => project.Compilation.Assembly.Identity).ToHashSet();
        return [.. projects.SelectMany(project => project.Compilation.SourceModule.ReferencedAssemblySymbols)
            .Where(assembly => !selectedAssemblies.Contains(assembly.Identity))
            .SelectMany(assembly => assembly.Modules.SelectMany(module => module.ReferencedAssemblySymbols))
            .Select(assembly => assembly.Identity)
            .Where(selectedAssemblies.Contains)];
    }

    static Dictionary<string, AuthoredArgument> InitialArguments(IParameterSymbol? request)
    {
        var arguments = new Dictionary<string, AuthoredArgument>(StringComparer.Ordinal);
        if (request is not null)
        {
            arguments[ParameterKey(request)] = new(request.Type, null);
        }

        return arguments;
    }

    static string Signature(IEnumerable<WolverineProduction> productions) => string.Join('\n', productions
        .Select(production => $"{TypeKey(production.EventType)}:{(production.StreamIdentity is null ? string.Empty : MemberKey(production.StreamIdentity))}:{production.LossReason}:{production.LossOutcome}")
        .Distinct(StringComparer.Ordinal)
        .Order(StringComparer.Ordinal));

    static IEnumerable<INamedTypeSymbol> TypesIn(INamespaceOrTypeSymbol container) =>
        container.GetMembers().OfType<INamespaceOrTypeSymbol>()
            .SelectMany(member => member is INamedTypeSymbol type ? TypesIn(member).Prepend(type) : TypesIn(member));

    static List<T> ListFor<T>(Dictionary<string, List<T>> index, string key)
    {
        if (!index.TryGetValue(key, out var list))
        {
            list = [];
            index.Add(key, list);
        }

        return list;
    }

    static bool HasSupportedBody(AuthoredCall call, AuthoredImplementation implementation, (MethodDeclarationSyntax Declaration, SemanticModel SemanticModel)[] declarations) =>
        declarations.Length == 1 && (declarations[0].Declaration.Body is not null || declarations[0].Declaration.ExpressionBody is not null) &&
        !implementation.Method.IsGenericMethod &&
        implementation.Method.ContainingType.TypeKind != TypeKind.Interface &&
        HasPlainArgumentBinding(call);

    static bool HasPlainArgumentBinding(AuthoredCall call)
    {
        var target = call.Operation.TargetMethod;
        return target.ReducedFrom is null &&
            target.Parameters.All(parameter => parameter.RefKind == RefKind.None) &&
            call.Operation.Arguments.GroupBy(argument => argument.Parameter?.Ordinal).All(group => group.Key is not null && group.Count() == 1);
    }

    static Dictionary<string, AuthoredArgument> BindArguments(AuthoredCall call, AuthoredImplementation implementation, IReadOnlyDictionary<string, AuthoredArgument> arguments) =>
        call.Operation.Arguments.ToDictionary(
            argument => ParameterKey(implementation.Method.Parameters[argument.Parameter!.Ordinal]),
            argument => Bind(argument.Value, arguments),
            StringComparer.Ordinal);

    static SourceRange[]? EvidenceSources(AuthoredCall call, SourceRange? implementationSource, IReadOnlyList<SourceRange> trail)
    {
        var callSource = SourceOf(call);
        return callSource is null || implementationSource is null ? null : [.. trail.Concat([callSource, implementationSource]).Distinct()];
    }

    static bool AppendsUnestablishedPayload(List<WolverineProduction> productions, AuthoredCall[] nestedCalls) =>
        productions.Exists(production => HasOpenType(production.EventType)) ||
        (productions.Count == 0 && nestedCalls.Any(nested => WolverineProductionLinks.IsSessionAppend(nested.Operation, nested.Project)));

    static string? FirstSourcePath(AuthoredImplementation implementation) =>
        implementation.Method.Locations.Where(location => location.IsInSource)
            .Select(location => CritterStackSource.RangeForProject(location, implementation.Project)?.Path)
            .Order(StringComparer.Ordinal).FirstOrDefault();

    static List<AuthoredImplementation> InDeterministicOrder(List<AuthoredImplementation> implementations) =>
    [
        .. implementations.OrderBy(implementation => DotNetSubjectIds.MetadataName(implementation.Method.ContainingType.OriginalDefinition), StringComparer.Ordinal)
            .ThenBy(FirstSourcePath, StringComparer.Ordinal)
            .ThenBy(implementation => implementation.Type, StringComparer.Ordinal)
    ];

    static IEnumerable<IMethodSymbol> DispatchContracts(INamedTypeSymbol type) =>
        type.AllInterfaces.SelectMany(contract => contract.GetMembers().OfType<IMethodSymbol>())
            .Concat(Hierarchy(type).SelectMany(parent => parent.GetMembers().OfType<IMethodSymbol>())
                .Where(method => method.IsVirtual || method.IsAbstract || method.IsOverride));

    static bool IsIndexed(AuthoredDispatch candidate, DotNetProjectCompilation project, INamedTypeSymbol type, IMethodSymbol contract) =>
        candidate.Project == project && TypeKey(candidate.Type) == TypeKey(type) && TypeKey(candidate.Contract.ContainingType) == TypeKey(contract.ContainingType);

    static bool HasVariantTypeParameters(IMethodSymbol target) =>
        target.ContainingType.OriginalDefinition.TypeParameters.Any(parameter => parameter.Variance != VarianceKind.None);

    static bool IsSingleAuthoredDefinition(List<AuthoredImplementation> found) =>
        found.Count == 1 && WolverineMethodSyntax.Declarations(found[0].Method, found[0].Project).Any();

    static bool IsStaticallyBound(IMethodSymbol target) =>
        target.ContainingType.TypeKind != TypeKind.Interface && !target.IsVirtual && !target.IsAbstract && !target.IsOverride;

    static (AuthoredFollowState? State, string? Failure) NestedStateFor(AuthoredCall call, AuthoredImplementation implementation, MethodDeclarationSyntax declaration, AuthoredFollowState state)
    {
        var boundArguments = BindArguments(call, implementation, state.Arguments);
        if (HasReboundArguments(implementation.Method, implementation.Project, boundArguments))
        {
            return (null, "a forwarded argument is reassigned in an implementation");
        }

        var implementationSource = CritterStackSource.RangeForProject(declaration.GetLocation(), implementation.Project);
        var sources = EvidenceSources(call, implementationSource, state.Trail);
        if (sources is null)
        {
            return (null, "the source locations needed for call-site and implementation evidence are unavailable");
        }

        return (new AuthoredFollowState(boundArguments, state.Traversal, sources, state.Conditional || IsConditionallyReached(call), state.Depth + 1), null);
    }

    IReadOnlyList<WolverineProduction> FollowFromRoot(
        IMethodSymbol method,
        DotNetProjectCompilation project,
        AuthoredCall call,
        AuthoredFollowState root)
    {
        if (HasReboundArguments(method, project, root.Arguments))
        {
            return root.Traversal.Fail(call, "a forwarded argument is reassigned in the caller");
        }

        return Follow(call, root);
    }

    void IndexType(DotNetProjectCompilation project, INamedTypeSymbol type)
    {
        IndexMethods(project, type);
        if (type.IsAbstract || type.TypeKind is not (TypeKind.Class or TypeKind.Struct))
        {
            return;
        }

        foreach (var contract in DispatchContracts(type).Where(IsApplicationCall))
        {
            var dispatches = ListFor(_dispatch, MethodKey(contract));
            if (!dispatches.Exists(candidate => IsIndexed(candidate, project, type, contract)))
            {
                dispatches.Add(new(project, type, contract));
            }
        }
    }

    void IndexMethods(DotNetProjectCompilation project, INamedTypeSymbol type)
    {
        foreach (var method in type.GetMembers().OfType<IMethodSymbol>())
        {
            var methods = ListFor(_methods, MethodKey(method));
            if (!methods.Exists(candidate => candidate.Project == project && candidate.Type == TypeKey(type)))
            {
                methods.Add(new(project, method.PartialImplementationPart ?? method, TypeKey(type)));
            }
        }
    }

    IReadOnlyList<WolverineProduction> Follow(AuthoredCall call, AuthoredFollowState state)
    {
        var traversal = state.Traversal;
        if (!IsApplicationCall(call.Operation.TargetMethod))
        {
            return [];
        }

        if (state.Depth >= MaximumDepth)
        {
            return traversal.Fail(call, LimitReason);
        }

        var candidates = Candidates(call.Operation.TargetMethod);
        if (candidates.Count == 0)
        {
            return traversal.Fail(call, "the selected source scope does not establish a complete authored implementation set");
        }

        var results = new List<IReadOnlyList<WolverineProduction>>();
        foreach (var candidate in candidates)
        {
            results.Add(FollowImplementation(call, candidate, state));
            if (traversal.Failed)
            {
                return [];
            }
        }

        if (results.Skip(1).Any(result => Signature(result) != Signature(results[0])))
        {
            return traversal.Fail(call, "the authored implementations do not establish identical persistence consequences");
        }

        return [.. results.SelectMany(result => result)];
    }

    IReadOnlyList<WolverineProduction> FollowImplementation(AuthoredCall call, AuthoredImplementation implementation, AuthoredFollowState state)
    {
        var traversal = state.Traversal;
        if (++traversal.Budget.Calls > MaximumCalls)
        {
            return traversal.Fail(call, LimitReason);
        }

        var declarations = WolverineMethodSyntax.Declarations(implementation.Method, implementation.Project).ToArray();
        if (!HasSupportedBody(call, implementation, declarations))
        {
            return traversal.Fail(call, "an implementation has no supported authored body or argument binding");
        }

        var methodKey = MethodKey(implementation.Method);
        if (!traversal.Path.Add(methodKey))
        {
            return traversal.Fail(call, "the authored traversal encountered a cycle");
        }

        var (nestedState, failure) = NestedStateFor(call, implementation, declarations[0].Declaration, state);
        if (nestedState is null)
        {
            return traversal.Fail(call, failure!);
        }

        var productions = AuthoredProductions(implementation, nestedState);
        var nestedCalls = CallsIn(implementation.Method, implementation.Project).ToArray();
        if (AppendsUnestablishedPayload(productions, nestedCalls))
        {
            return traversal.Fail(call, "an implementation appends a payload whose event type is not established by authored source");
        }

        if (!FollowNested(productions, nestedCalls, nestedState))
        {
            return [];
        }

        traversal.Path.Remove(methodKey);
        return productions;
    }

    bool FollowNested(List<WolverineProduction> productions, AuthoredCall[] nestedCalls, AuthoredFollowState nestedState)
    {
        foreach (var nested in nestedCalls)
        {
            productions.AddRange(Follow(nested, nestedState));
            if (nestedState.Traversal.Failed)
            {
                return false;
            }
        }

        return true;
    }

    List<WolverineProduction> AuthoredProductions(AuthoredImplementation implementation, AuthoredFollowState state) =>
        [.. WolverineProductionLinks.AuthoredSessionAppends(implementation.Method, implementation.Project, operation => Bind(operation, state.Arguments).Identity)
            .Select(production => production with
            {
                EventType = CanonicalType(production.EventType),
                StreamIdentity = state.Conditional ? null : production.StreamIdentity,
                LossReason = state.Conditional ? "the authored abstraction is called only on some paths through the handler" : production.LossReason,
                LossOutcome = state.Conditional ? GenerationDiagnosticOutcome.Unsupported : production.LossOutcome,
                Source = state.Traversal.Root,
                AuthoredSources = production.Source is { } appendSource ? [.. state.Trail, appendSource] : [.. state.Trail]
            })];

    List<AuthoredImplementation> Candidates(IMethodSymbol target)
    {
        if (!TryFindDefinitions(target, out var definitions))
        {
            return [];
        }

        if (IsStaticallyBound(target))
        {
            return definitions;
        }

        return _dispatch.TryGetValue(MethodKey(target), out var dispatches) ? ResolveDispatches(target, dispatches) : [];
    }

    bool TryFindDefinitions(IMethodSymbol target, out List<AuthoredImplementation> definitions)
    {
        definitions = [];
        if (HasVariantTypeParameters(target) || _incompleteAssemblies.Contains(target.ContainingAssembly.Identity))
        {
            return false;
        }

        if (!_methods.TryGetValue(MethodKey(target), out var found) || !IsSingleAuthoredDefinition(found))
        {
            return false;
        }

        definitions = found;
        return true;
    }

    List<AuthoredImplementation> ResolveDispatches(IMethodSymbol target, List<AuthoredDispatch> dispatches)
    {
        var implementations = new List<AuthoredImplementation>();
        foreach (var dispatch in dispatches)
        {
            var bindings = new Dictionary<ITypeParameterSymbol, ITypeSymbol>(SymbolEqualityComparer.Default);
            if (!BindTypes(dispatch.Contract.ContainingType, target.ContainingType, bindings))
            {
                continue;
            }

            var implementation = ResolveImplementation(target, dispatch, bindings);
            if (implementation is null)
            {
                return [];
            }

            implementations.Add(implementation);
        }

        return InDeterministicOrder(implementations);
    }

    AuthoredImplementation? ResolveImplementation(IMethodSymbol target, AuthoredDispatch dispatch, Dictionary<ITypeParameterSymbol, ITypeSymbol> bindings)
    {
        var runtimeType = Construct(dispatch, bindings);
        var contract = runtimeType?.AllInterfaces.Concat(Hierarchy(runtimeType))
            .SingleOrDefault(type => TypeKey(type) == TypeKey(target.ContainingType));
        var member = contract?.GetMembers().OfType<IMethodSymbol>().SingleOrDefault(method => MethodKey(method) == MethodKey(target));
        var resolvedMethod = runtimeType is null || member is null ? null : ImplementationOf(runtimeType, member);
        if (resolvedMethod is null)
        {
            return null;
        }

        return _methods.TryGetValue(MethodKey(resolvedMethod), out var owners) && owners.Count == 1
            ? new(owners[0].Project, resolvedMethod.PartialImplementationPart ?? resolvedMethod, TypeKey(runtimeType!))
            : null;
    }

    INamedTypeSymbol CanonicalType(INamedTypeSymbol type)
    {
        var owner = _projects.FirstOrDefault(project => project.Compilation.Assembly.Identity.Equals(type.ContainingAssembly.Identity));
        return owner is null ? type : ResolveType(type, owner.Compilation) as INamedTypeSymbol ?? type;
    }
}
