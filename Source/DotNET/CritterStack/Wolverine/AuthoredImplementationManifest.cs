// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Cratis.Screenplay.Generation;
using Cratis.Screenplay.Generation.DotNet;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Operations;

namespace Cratis.CritterStack.Screenplay.Wolverine;

/// <summary>
/// Indexes selected-project dispatch once using assembly-qualified metadata identities, not compilation-local symbols.
/// </summary>
sealed class AuthoredImplementationManifest
{
    const int MaximumDepth = 8;
    const int MaximumCalls = 128;

    readonly IReadOnlyList<DotNetProjectCompilation> _projects;
    readonly Dictionary<string, List<Implementation>> _methods = [with(StringComparer.Ordinal)];
    readonly Dictionary<string, List<Dispatch>> _dispatch = [with(StringComparer.Ordinal)];
    readonly HashSet<AssemblyIdentity> _incompleteAssemblies;
    internal static readonly string[] sourceArray = new[] { "System", "Microsoft", "Marten", "Wolverine", "JasperFx" };

    public AuthoredImplementationManifest(DotNetAnalysisContext context)
    {
        _projects = context.Projects;
        var selectedAssemblies = _projects.Select(project => project.Compilation.Assembly.Identity).ToHashSet();
        _incompleteAssemblies = [.. _projects.SelectMany(project => project.Compilation.SourceModule.ReferencedAssemblySymbols)
            .Where(assembly => !selectedAssemblies.Contains(assembly.Identity))
            .SelectMany(assembly => assembly.Modules.SelectMany(module => module.ReferencedAssemblySymbols))
            .Select(assembly => assembly.Identity)
            .Where(selectedAssemblies.Contains)];
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
        var arguments = request is null
            ? [with(StringComparer.Ordinal)]
            : new Dictionary<string, Argument>(StringComparer.Ordinal) { [ParameterKey(request)] = new(request.Type, null) };
        var budget = new TraversalBudget();
        var failed = false;
        foreach (var call in CallsIn(method, project).Where(call => IsApplicationCall(call.Operation.TargetMethod)))
        {
            var traversal = new Traversal(diagnostics, budget, SourceOf(call), [MethodKey(method)]);
            if (HasReboundArguments(method, project, arguments))
            {
                Fail(traversal, call, "a forwarded argument is reassigned in the caller");
            }
            else
            {
                result.AddRange(Follow(call, arguments, traversal, [], conditional: false, depth: 0));
            }

            failed |= traversal.Failed;
        }

        return failed ? [] : result;
    }

    void IndexType(DotNetProjectCompilation project, INamedTypeSymbol type)
    {
        foreach (var method in type.GetMembers().OfType<IMethodSymbol>())
        {
            var key = MethodKey(method);
            if (!_methods.TryGetValue(key, out var methods))
            {
                methods = [];
                _methods.Add(key, methods);
            }

            if (!methods.Exists(candidate => candidate.Project == project && candidate.Type == TypeKey(type)))
            {
                methods.Add(new(project, method.PartialImplementationPart ?? method, TypeKey(type)));
            }
        }

        if (type.IsAbstract || type.TypeKind is not (TypeKind.Class or TypeKind.Struct))
        {
            return;
        }

        var contracts = type.AllInterfaces.SelectMany(contract => contract.GetMembers().OfType<IMethodSymbol>())
            .Concat(Hierarchy(type).SelectMany(parent => parent.GetMembers().OfType<IMethodSymbol>())
                .Where(method => method.IsVirtual || method.IsAbstract || method.IsOverride));
        foreach (var contract in contracts.Where(IsApplicationCall))
        {
            var key = MethodKey(contract);
            if (!_dispatch.TryGetValue(key, out var dispatches))
            {
                dispatches = [];
                _dispatch.Add(key, dispatches);
            }

            if (!dispatches.Exists(candidate => candidate.Project == project && TypeKey(candidate.Type) == TypeKey(type) && TypeKey(candidate.Contract.ContainingType) == TypeKey(contract.ContainingType)))
            {
                dispatches.Add(new(project, type, contract));
            }
        }
    }

    IReadOnlyList<WolverineProduction> Follow(
        Call call,
        IReadOnlyDictionary<string, Argument> arguments,
        Traversal traversal,
        IReadOnlyList<SourceRange> trail,
        bool conditional,
        int depth)
    {
        if (!IsApplicationCall(call.Operation.TargetMethod))
        {
            return [];
        }

        if (depth >= MaximumDepth)
        {
            return Fail(traversal, call, "the authored traversal reached its depth or call-count limit");
        }

        var candidates = Candidates(call.Operation.TargetMethod);
        if (candidates.Count == 0)
        {
            return Fail(traversal, call, "the selected source scope does not establish a complete authored implementation set");
        }

        var results = new List<IReadOnlyList<WolverineProduction>>();
        foreach (var candidate in candidates)
        {
            results.Add(FollowImplementation(call, candidate, arguments, traversal, trail, conditional, depth));
            if (traversal.Failed)
            {
                return [];
            }
        }

        if (results.Skip(1).Any(result => Signature(result) != Signature(results[0])))
        {
            return Fail(traversal, call, "the authored implementations do not establish identical persistence consequences");
        }

        return [.. results.SelectMany(result => result)];
    }

    IReadOnlyList<WolverineProduction> FollowImplementation(
        Call call,
        Implementation implementation,
        IReadOnlyDictionary<string, Argument> arguments,
        Traversal traversal,
        IReadOnlyList<SourceRange> trail,
        bool conditional,
        int depth)
    {
        if (++traversal.Budget.Calls > MaximumCalls)
        {
            return Fail(traversal, call, "the authored traversal reached its depth or call-count limit");
        }

        var declarations = WolverineMethodSyntax.Declarations(implementation.Method, implementation.Project).ToArray();
        var target = call.Operation.TargetMethod;
        if (declarations.Length != 1 || (declarations[0].Declaration.Body is null && declarations[0].Declaration.ExpressionBody is null) ||
            implementation.Method.IsGenericMethod || target.ReducedFrom is not null ||
            implementation.Method.ContainingType.TypeKind == TypeKind.Interface ||
            target.Parameters.Any(parameter => parameter.RefKind != RefKind.None) ||
            call.Operation.Arguments.GroupBy(argument => argument.Parameter?.Ordinal).Any(group => group.Key is null || group.Count() != 1))
        {
            return Fail(traversal, call, "an implementation has no supported authored body or argument binding");
        }

        var methodKey = MethodKey(implementation.Method);
        if (!traversal.Path.Add(methodKey))
        {
            return Fail(traversal, call, "the authored traversal encountered a cycle");
        }

        var boundArguments = call.Operation.Arguments.ToDictionary(
            argument => ParameterKey(implementation.Method.Parameters[argument.Parameter!.Ordinal]),
            argument => Bind(argument.Value, arguments),
            StringComparer.Ordinal);
        if (HasReboundArguments(implementation.Method, implementation.Project, boundArguments))
        {
            return Fail(traversal, call, "a forwarded argument is reassigned in an implementation");
        }

        var declaration = declarations[0].Declaration;
        var callSource = SourceOf(call);
        var implementationSource = CritterStackSource.RangeForProject(declaration.GetLocation(), implementation.Project);
        if (callSource is null || implementationSource is null)
        {
            return Fail(traversal, call, "the source locations needed for call-site and implementation evidence are unavailable");
        }

        var sources = trail.Concat([callSource, implementationSource]).Distinct().ToArray();
        var isConditional = conditional || IsConditionallyReached(call);
        var productions = WolverineProductionLinks.AuthoredSessionAppends(implementation.Method, implementation.Project, operation => Bind(operation, boundArguments).Identity)
            .Select(production => production with
            {
                EventType = CanonicalType(production.EventType),
                StreamIdentity = isConditional ? null : production.StreamIdentity,
                LossReason = isConditional ? "the authored abstraction is called only on some paths through the handler" : production.LossReason,
                LossOutcome = isConditional ? GenerationDiagnosticOutcome.Unsupported : production.LossOutcome,
                Source = traversal.Root,
                AuthoredSources = production.Source is { } appendSource ? [.. sources, appendSource] : sources
            }).ToList();
        var nestedCalls = CallsIn(implementation.Method, implementation.Project).ToArray();
        if (productions.Exists(production => HasOpenType(production.EventType)) ||
            (productions.Count == 0 && nestedCalls.Any(nested => WolverineProductionLinks.IsSessionAppend(nested.Operation, nested.Project))))
        {
            return Fail(traversal, call, "an implementation appends a payload whose event type is not established by authored source");
        }

        foreach (var nested in nestedCalls)
        {
            productions.AddRange(Follow(nested, boundArguments, traversal, sources, isConditional, depth + 1));
            if (traversal.Failed)
            {
                return [];
            }
        }

        traversal.Path.Remove(methodKey);
        return productions;
    }

    IReadOnlyList<Implementation> Candidates(IMethodSymbol target)
    {
        if (target.ContainingType.OriginalDefinition.TypeParameters.Any(parameter => parameter.Variance != VarianceKind.None) ||
            !_methods.TryGetValue(MethodKey(target), out var definitions) || definitions.Count != 1 ||
            !WolverineMethodSyntax.Declarations(definitions[0].Method, definitions[0].Project).Any() ||
            _incompleteAssemblies.Contains(target.ContainingAssembly.Identity))
        {
            return [];
        }

        if (target.ContainingType.TypeKind != TypeKind.Interface && !target.IsVirtual && !target.IsAbstract && !target.IsOverride)
        {
            return definitions;
        }

        if (!_dispatch.TryGetValue(MethodKey(target), out var dispatches))
        {
            return [];
        }

        var implementations = new List<Implementation>();
        foreach (var dispatch in dispatches)
        {
            var bindings = new Dictionary<ITypeParameterSymbol, ITypeSymbol>(SymbolEqualityComparer.Default);
            if (!BindTypes(dispatch.Contract.ContainingType, target.ContainingType, bindings))
            {
                continue;
            }

            var runtimeType = Construct(dispatch, bindings);
            var contract = runtimeType?.AllInterfaces.Concat(Hierarchy(runtimeType))
                .SingleOrDefault(type => TypeKey(type) == TypeKey(target.ContainingType));
            var member = contract?.GetMembers().OfType<IMethodSymbol>().SingleOrDefault(method => MethodKey(method) == MethodKey(target));
            var resolvedMethod = runtimeType is null || member is null ? null : ImplementationOf(runtimeType, member);
            if (resolvedMethod is null || !_methods.TryGetValue(MethodKey(resolvedMethod), out var owners) || owners.Count != 1)
            {
                return [];
            }

            implementations.Add(new(owners[0].Project, resolvedMethod.PartialImplementationPart ?? resolvedMethod, TypeKey(runtimeType!)));
        }

        return [.. implementations.OrderBy(implementation => DotNetSubjectIds.MetadataName(implementation.Method.ContainingType.OriginalDefinition), StringComparer.Ordinal)
            .ThenBy(
                implementation => implementation.Method.Locations.Where(location => location.IsInSource)
                .Select(location => CritterStackSource.RangeForProject(location, implementation.Project)?.Path)
                .Order(StringComparer.Ordinal).FirstOrDefault(), StringComparer.Ordinal)
            .ThenBy(implementation => implementation.Type, StringComparer.Ordinal)];
    }

    static INamedTypeSymbol? Construct(Dispatch dispatch, IReadOnlyDictionary<ITypeParameterSymbol, ITypeSymbol> bindings)
    {
        if (!dispatch.Type.IsGenericType)
        {
            return dispatch.Type;
        }

        if (dispatch.Type.ContainingType is not null || dispatch.Type.TypeParameters.Any(parameter =>
                parameter.HasReferenceTypeConstraint || parameter.HasValueTypeConstraint || parameter.HasUnmanagedTypeConstraint ||
                parameter.HasConstructorConstraint || parameter.HasNotNullConstraint || parameter.ConstraintTypes.Length > 0))
        {
            return null;
        }

        var arguments = dispatch.Type.TypeParameters.Select(parameter => bindings.TryGetValue(parameter, out var type) ? ResolveType(type, dispatch.Project.Compilation) : null).ToArray();
        return arguments.Any(argument => argument is null) ? null : dispatch.Type.Construct([.. arguments.OfType<ITypeSymbol>()]);
    }

    static ITypeSymbol? ResolveType(ITypeSymbol type, Compilation compilation)
    {
        if (type is not INamedTypeSymbol named)
        {
            return null;
        }

        var definition = compilation.GetTypeByMetadataName(DotNetSubjectIds.MetadataName(named.OriginalDefinition));
        if (definition?.ContainingAssembly.Identity.Equals(named.ContainingAssembly.Identity) != true)
        {
            return null;
        }

        if (!named.IsGenericType)
        {
            return definition;
        }

        var arguments = named.TypeArguments.Select(argument => ResolveType(argument, compilation)).ToArray();
        return arguments.Any(argument => argument is null) ? null : definition.Construct([.. arguments.OfType<ITypeSymbol>()]);
    }

    static bool BindTypes(ITypeSymbol pattern, ITypeSymbol actual, Dictionary<ITypeParameterSymbol, ITypeSymbol> bindings)
    {
        if (pattern is ITypeParameterSymbol parameter)
        {
            if (bindings.TryGetValue(parameter, out var previous))
            {
                return TypeKey(previous) == TypeKey(actual);
            }

            bindings.Add(parameter, actual);
            return true;
        }

        return pattern is INamedTypeSymbol left && actual is INamedTypeSymbol right &&
            MemberKey(left.OriginalDefinition) == MemberKey(right.OriginalDefinition) &&
            left.TypeArguments.Length == right.TypeArguments.Length &&
            left.TypeArguments.Zip(right.TypeArguments).All(pair => BindTypes(pair.First, pair.Second, bindings));
    }

    static IMethodSymbol? ImplementationOf(INamedTypeSymbol runtimeType, IMethodSymbol contract)
    {
        if (contract.ContainingType.TypeKind == TypeKind.Interface)
        {
            return runtimeType.FindImplementationForInterfaceMember(contract) as IMethodSymbol;
        }

        return Hierarchy(runtimeType).SelectMany(type => type.GetMembers().OfType<IMethodSymbol>())
            .FirstOrDefault(method => Overrides(method, contract));
    }

    static bool Overrides(IMethodSymbol method, IMethodSymbol contract)
    {
        for (var current = method; current is not null; current = current.OverriddenMethod)
        {
            if (MethodKey(current) == MethodKey(contract))
            {
                return true;
            }
        }

        return false;
    }

    static bool IsApplicationCall(IMethodSymbol method)
    {
        var name = method.ContainingNamespace.ToDisplayString();
        return !sourceArray.Any(prefix => name == prefix || name.StartsWith($"{prefix}.", StringComparison.Ordinal));
    }

    static Argument Bind(IOperation operation, IReadOnlyDictionary<string, Argument> arguments)
    {
        while (operation is IConversionOperation conversion)
        {
            operation = conversion.Operand;
        }

        if (operation is IParameterReferenceOperation parameter)
        {
            return arguments.GetValueOrDefault(ParameterKey(parameter.Parameter)) ?? new(null, null);
        }

        if (operation is IPropertyReferenceOperation { Instance: not null } property && Bind(property.Instance, arguments).RequestType is INamedTypeSymbol requestType)
        {
            return new(null, Hierarchy(requestType).SelectMany(type => type.GetMembers().OfType<IPropertySymbol>())
                .FirstOrDefault(member => MemberKey(member) == MemberKey(property.Property)));
        }

        return new(null, null);
    }

    static bool HasReboundArguments(IMethodSymbol method, DotNetProjectCompilation project, IReadOnlyDictionary<string, Argument> arguments) =>
        WolverineMethodSyntax.Declarations(method, project).Any(declaration =>
            WrittenExpressions(declaration.Declaration).Any(expression =>
                expression.DescendantNodesAndSelf().OfType<IdentifierNameSyntax>().Any(identifier =>
                    declaration.SemanticModel.GetSymbolInfo(identifier).Symbol is IParameterSymbol parameter && arguments.ContainsKey(ParameterKey(parameter))))) ||
        CallsIn(method, project).Any(call => call.Operation.Arguments.Any(argument =>
            argument.Parameter?.RefKind is not (null or RefKind.None) &&
            Bind(argument.Value, arguments) is { RequestType: not null } or { Identity: not null }));

    static IEnumerable<ExpressionSyntax> WrittenExpressions(MethodDeclarationSyntax declaration) =>
        declaration.DescendantNodes().OfType<AssignmentExpressionSyntax>().Select(assignment => assignment.Left)
            .Concat(declaration.DescendantNodes().OfType<PrefixUnaryExpressionSyntax>()
                .Where(expression => expression.OperatorToken.ValueText is "++" or "--").Select(expression => expression.Operand))
            .Concat(declaration.DescendantNodes().OfType<PostfixUnaryExpressionSyntax>()
                .Where(expression => expression.OperatorToken.ValueText is "++" or "--").Select(expression => expression.Operand));

    static bool IsConditionallyReached(Call call) =>
        WolverineProductionLinks.IsConditional(call.Syntax, call.Declaration) ||
        call.Declaration.DescendantNodes().Any(node => (node is ReturnStatementSyntax or ThrowStatementSyntax or GotoStatementSyntax) &&
            node.Span.End <= call.Syntax.SpanStart && WolverineProductionLinks.IsInHandlerBody(node, call.Declaration));

    INamedTypeSymbol CanonicalType(INamedTypeSymbol type)
    {
        var owner = _projects.FirstOrDefault(project => project.Compilation.Assembly.Identity.Equals(type.ContainingAssembly.Identity));
        return owner is null ? type : ResolveType(type, owner.Compilation) as INamedTypeSymbol ?? type;
    }

    static bool HasOpenType(ITypeSymbol type) =>
        type is ITypeParameterSymbol || (type is INamedTypeSymbol named && (named.IsUnboundGenericType || named.TypeArguments.Any(HasOpenType)));

    static IReadOnlyList<WolverineProduction> Fail(Traversal traversal, Call call, string reason)
    {
        traversal.Failed = true;
        var source = SourceOf(call);
        var message = $"Cannot follow authored abstraction '{call.Operation.TargetMethod.ToDisplayString()}' because {reason}; persistence behind this call is not inferred";
        if (!traversal.Diagnostics.Exists(diagnostic => diagnostic.Code == WolverineDiagnosticCodes.AuthoredAbstractionUnresolved && diagnostic.Source == source && diagnostic.Message == message))
        {
            traversal.Diagnostics.Add(new GenerationDiagnostic
            {
                Code = WolverineDiagnosticCodes.AuthoredAbstractionUnresolved,
                Severity = GenerationDiagnosticSeverity.Warning,
                Outcome = GenerationDiagnosticOutcome.Unknown,
                Source = source,
                Message = message
            });
        }

        return [];
    }

    static IEnumerable<Call> CallsIn(IMethodSymbol method, DotNetProjectCompilation project) =>
        WolverineMethodSyntax.Declarations(method, project)
            .SelectMany(declaration => declaration.Declaration.DescendantNodes().OfType<InvocationExpressionSyntax>()
                .Where(syntax => WolverineProductionLinks.IsInHandlerBody(syntax, declaration.Declaration))
                .Select(syntax => new { Syntax = syntax, Operation = declaration.SemanticModel.GetOperation(syntax) as IInvocationOperation, declaration.Declaration })
                .Where(call => call.Operation is not null)
                .Select(call => new Call(call.Operation!, call.Syntax, call.Declaration, project)));

    static SourceRange? SourceOf(Call call) => CritterStackSource.RangeForProject(call.Syntax.GetLocation(), call.Project);

    static string Signature(IEnumerable<WolverineProduction> productions) => string.Join('\n', productions
        .Select(production => $"{TypeKey(production.EventType)}:{(production.StreamIdentity is null ? string.Empty : MemberKey(production.StreamIdentity))}:{production.LossReason}:{production.LossOutcome}")
        .Distinct(StringComparer.Ordinal)
        .Order(StringComparer.Ordinal));

    static string ParameterKey(IParameterSymbol parameter) => $"{MemberKey(parameter.ContainingSymbol)}:{parameter.Ordinal}";

    static string MethodKey(IMethodSymbol method) => MemberKey(method.OriginalDefinition);

    static string MemberKey(ISymbol member) => $"{member.ContainingAssembly.Identity}:{member.OriginalDefinition.GetDocumentationCommentId()}";

    static string TypeKey(ITypeSymbol type) => $"{type.ContainingAssembly.Identity}:{type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat)}";

    static IEnumerable<INamedTypeSymbol> Hierarchy(INamedTypeSymbol type)
    {
        for (var current = type; current is not null; current = current.BaseType)
        {
            yield return current;
        }
    }

    static IEnumerable<INamedTypeSymbol> TypesIn(INamespaceOrTypeSymbol container)
    {
        foreach (var member in container.GetMembers())
        {
            if (member is INamedTypeSymbol type)
            {
                yield return type;
            }

            if (member is INamespaceOrTypeSymbol nested)
            {
                foreach (var descendant in TypesIn(nested))
                {
                    yield return descendant;
                }
            }
        }
    }

    sealed record Implementation(DotNetProjectCompilation Project, IMethodSymbol Method, string Type);

    sealed record Dispatch(DotNetProjectCompilation Project, INamedTypeSymbol Type, IMethodSymbol Contract);

    sealed record Argument(ITypeSymbol? RequestType, ISymbol? Identity);

    sealed record Call(IInvocationOperation Operation, InvocationExpressionSyntax Syntax, MethodDeclarationSyntax Declaration, DotNetProjectCompilation Project);

    sealed class TraversalBudget
    {
        public int Calls { get; set; }
    }

    sealed class Traversal(List<GenerationDiagnostic> diagnostics, TraversalBudget budget, SourceRange? root, HashSet<string> path)
    {
        public List<GenerationDiagnostic> Diagnostics { get; } = diagnostics;
        public TraversalBudget Budget { get; } = budget;
        public SourceRange? Root { get; } = root;
        public HashSet<string> Path { get; } = path;
        public bool Failed { get; set; }
    }
}
