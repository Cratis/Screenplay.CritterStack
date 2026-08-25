// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Cratis.Screenplay.Generation;
using Cratis.Screenplay.Generation.DotNet;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Cratis.CritterStack.Screenplay.Marten;

static class MartenDocumentFacts
{
    static readonly HashSet<string> _readMethods = ["Load", "LoadAsync", "LoadMany", "LoadManyAsync", "Query"];
    static readonly HashSet<string> _storeMethods = ["Insert", "Store"];
    static readonly HashSet<string> _updateMethods = ["Update"];
    static readonly HashSet<string> _deleteMethods = ["Delete", "DeleteWhere"];
    static readonly HashSet<string> _configurationMethods = ["For", "RegisterDocumentType"];

    public static MartenDiscoveryResult Discover(
        DotNetProjectCompilation project,
        AdapterIdentity adapter,
        IReadOnlyList<MartenDocumentUsage> projectedDocuments)
    {
        var facts = new List<GenerationFact>();
        var diagnostics = new List<GenerationDiagnostic>();
        var documents = projectedDocuments
            .GroupBy(_ => project.SubjectForType(_.Type))
            .ToDictionary(
                _ => _.Key,
                _ => new DocumentObservation(_.First().Type, _.First().Evidence));
        foreach (var tree in project.AuthoredSyntaxTrees.Where(_ => !DotNetGeneratedSource.IsGenerated(_)))
        {
            var semanticModel = project.Compilation.GetSemanticModel(tree);
            foreach (var invocation in tree.GetRoot().DescendantNodes().OfType<InvocationExpressionSyntax>())
            {
                if (semanticModel.GetSymbolInfo(invocation).Symbol is not IMethodSymbol method || !IsMarten(method))
                {
                    continue;
                }

                if (IsIdentityConfiguration(method))
                {
                    ObserveIdentityConfiguration(project, adapter, invocation, method, semanticModel, documents, diagnostics);
                    continue;
                }

                if (MartenCompiledQueryDiscovery.IsCompiledQueryExecution(method))
                {
                    if (MartenCompiledQueryDiscovery.TryResolve(invocation, semanticModel, out var plan))
                    {
                        var compiledEvidence = UsageEvidence(project, adapter, invocation, method.Name);
                        documents.TryAdd(project.SubjectForType(plan.DocumentType), new(plan.DocumentType, compiledEvidence));
                    }
                    continue;
                }

                var kind = RelationshipKindFor(method.Name);
                if (((kind is RelationshipKind.Stores or RelationshipKind.Updates or RelationshipKind.Deletes) &&
                     IsInEventProjection(invocation, semanticModel)) ||
                    (kind is not null && IsInUnresolvedCustomProcessor(invocation, semanticModel)))
                {
                    continue;
                }

                if (kind is null && !_configurationMethods.Contains(method.Name))
                {
                    continue;
                }

                var documentType = DocumentTypeFrom(method, invocation, semanticModel);
                if (documentType is null || !IsSourceType(documentType))
                {
                    continue;
                }

                var evidence = UsageEvidence(project, adapter, invocation, method.Name);
                var documentSubject = project.SubjectForType(documentType);
                documents.TryAdd(documentSubject, new(documentType, evidence));
                if (kind is not null && semanticModel.GetEnclosingSymbol(invocation.SpanStart) is IMethodSymbol containingMethod)
                {
                    var methodSubject = DotNetMethodIdentity.SubjectFor(project, containingMethod);
                    facts.Add(Artifact(
                        $"marten:handler:{methodSubject.Value}",
                        methodSubject,
                        ArtifactKind.Handler,
                        DotNetMethodIdentity.DisplayName(containingMethod),
                        SourceFileOf(containingMethod, project),
                        [],
                        evidence));
                    facts.Add(Relationship(
                        $"marten:{kind}:{methodSubject.Value}:{documentSubject.Value}:{invocation.SpanStart}",
                        methodSubject,
                        kind.Value,
                        documentSubject,
                        evidence));
                }
            }
        }

        foreach (var (subject, observed) in documents.OrderBy(_ => _.Key.Value, StringComparer.Ordinal))
        {
            var identityMember = observed.IdentityConfigurationUnresolved
                ? null
                : observed.IdentityMember ?? ConventionalIdentityMember(observed.Type);
            var properties = DocumentPropertiesOf(observed.Type);
            var identityProperty = identityMember is null
                ? null
                : properties.SingleOrDefault(_ => SymbolEqualityComparer.Default.Equals(_.Member, identityMember));
            if (identityMember is not null && identityProperty is null)
            {
                diagnostics.Add(new GenerationDiagnostic
                {
                    Code = MartenDiagnosticCodes.DocumentIdentityUnresolved,
                    Severity = GenerationDiagnosticSeverity.Warning,
                    Outcome = GenerationDiagnosticOutcome.Unsupported,
                    Message = $"Marten identity member '{identityMember.ContainingType?.Name}.{identityMember.Name}' cannot be represented uniquely in the emitted '{observed.Type.Name}' document property shape",
                    Source = observed.IdentityEvidence?.Source ?? observed.RegistrationEvidence.Source,
                    Subject = subject
                });
            }

            facts.Add(Artifact(
                $"marten:document:{subject.Value}",
                subject,
                ArtifactKind.Document,
                observed.Type.Name,
                SourceFileOf(observed.Type, project),
                [.. properties.Select(_ => _.Definition with { IsIdentifier = _ == identityProperty })],
                observed.RegistrationEvidence));
        }

        diagnostics.AddRange(documents
            .OrderBy(_ => _.Key.Value, StringComparer.Ordinal)
            .Select(_ => new GenerationDiagnostic
            {
                Code = MartenDiagnosticCodes.DocumentModelOmitted,
                Severity = GenerationDiagnosticSeverity.Information,
                Outcome = GenerationDiagnosticOutcome.Unsupported,
                Message = $"Marten document '{_.Value.Type.Name}' is persisted, queried, or explicitly configured, but the current Screenplay language has no ordinary document-state declaration",
                Source = _.Value.RegistrationEvidence.Source,
                Subject = _.Key
            }));

        return new(facts, diagnostics, []);
    }

    static void ObserveIdentityConfiguration(
        DotNetProjectCompilation project,
        AdapterIdentity adapter,
        InvocationExpressionSyntax invocation,
        IMethodSymbol method,
        SemanticModel semanticModel,
        Dictionary<SubjectId, DocumentObservation> documents,
        List<GenerationDiagnostic> diagnostics)
    {
        var candidate = method.ReducedFrom ?? method;
        if (candidate.ContainingType.TypeArguments.FirstOrDefault() is not INamedTypeSymbol documentType ||
            !IsSourceType(documentType))
        {
            return;
        }

        var evidence = new Evidence
        {
            Adapter = adapter,
            Strength = EvidenceStrength.Configured,
            Source = CritterStackSource.RangeForProject(invocation.GetLocation(), project),
            Explanation = $"Marten document identity configured through Schema.For<{documentType.Name}>().Identity(...)"
        };
        var subject = project.SubjectForType(documentType);
        documents.TryAdd(subject, new(documentType, evidence));
        var identityMember = ResolveIdentityMember(invocation, semanticModel, documentType);
        if (identityMember is null)
        {
            var unresolved = documents[subject];
            documents[subject] = unresolved with
            {
                RegistrationEvidence = evidence,
                IdentityMember = null,
                IdentityEvidence = evidence,
                IdentityConfigurationUnresolved = true
            };
            diagnostics.Add(new GenerationDiagnostic
            {
                Code = MartenDiagnosticCodes.DocumentIdentityUnresolved,
                Severity = GenerationDiagnosticSeverity.Warning,
                Outcome = GenerationDiagnosticOutcome.Unknown,
                Message = $"Marten identity configuration for '{documentType.Name}' is not a direct member expression and cannot be resolved safely",
                Source = evidence.Source,
                Subject = subject
            });
            return;
        }

        var observed = documents[subject];
        documents[subject] = observed with
        {
            RegistrationEvidence = evidence,
            IdentityMember = identityMember,
            IdentityEvidence = evidence,
            IdentityConfigurationUnresolved = false
        };
    }

    static ISymbol? ResolveIdentityMember(
        InvocationExpressionSyntax invocation,
        SemanticModel semanticModel,
        INamedTypeSymbol documentType)
    {
        if (invocation.ArgumentList.Arguments.FirstOrDefault()?.Expression is not LambdaExpressionSyntax lambda ||
            lambda.ExpressionBody is null)
        {
            return null;
        }

        var expression = Unwrap(lambda.ExpressionBody);
        if (expression is not MemberAccessExpressionSyntax memberAccess ||
            Unwrap(memberAccess.Expression) is not IdentifierNameSyntax identifier ||
            !string.Equals(identifier.Identifier.ValueText, LambdaParameterName(lambda), StringComparison.Ordinal))
        {
            return null;
        }

        var member = semanticModel.GetSymbolInfo(memberAccess).Symbol;
        return member is IPropertySymbol or IFieldSymbol && IsMemberOfDocumentType(member, documentType)
            ? member
            : null;
    }

    static ExpressionSyntax Unwrap(ExpressionSyntax expression)
    {
        while (true)
        {
            expression = expression switch
            {
                ParenthesizedExpressionSyntax parenthesized => parenthesized.Expression,
                CastExpressionSyntax cast => cast.Expression,
                _ => expression
            };
            if (expression is not (ParenthesizedExpressionSyntax or CastExpressionSyntax))
            {
                return expression;
            }
        }
    }

    static string? LambdaParameterName(LambdaExpressionSyntax lambda) => lambda switch
    {
        SimpleLambdaExpressionSyntax simple => simple.Parameter.Identifier.ValueText,
        ParenthesizedLambdaExpressionSyntax { ParameterList.Parameters.Count: 1 } parenthesized =>
            parenthesized.ParameterList.Parameters[0].Identifier.ValueText,
        _ => null
    };

    static bool IsMemberOfDocumentType(ISymbol member, INamedTypeSymbol documentType)
    {
        for (var current = documentType; current is not null; current = current.BaseType)
        {
            if (SymbolEqualityComparer.Default.Equals(member.ContainingType, current))
            {
                return true;
            }
        }

        return false;
    }

    static IReadOnlyList<DocumentProperty> DocumentPropertiesOf(INamedTypeSymbol documentType)
    {
        var properties = new List<IPropertySymbol>();
        for (var current = documentType; current is not null; current = current.BaseType)
        {
            properties.AddRange(current.GetMembers()
                .OfType<IPropertySymbol>()
                .Where(_ => !_.IsStatic &&
                            !_.IsIndexer &&
                            _.DeclaredAccessibility == Accessibility.Public &&
                            _.GetMethod?.DeclaredAccessibility == Accessibility.Public &&
                            properties.TrueForAll(existing => !string.Equals(existing.Name, _.Name, StringComparison.Ordinal)))
                .OrderBy(SourceOrder)
                .ThenBy(_ => _.Name, StringComparer.Ordinal));
        }

        return
        [
            .. properties.Select(_ => new DocumentProperty(
                _,
                new PropertyDefinition
                {
                    Name = LowerFirst(_.Name),
                    Type = DotNetTypeShapes.TypeReferenceFor(_.Type)
                }))
        ];
    }

    static ISymbol? ConventionalIdentityMember(INamedTypeSymbol documentType)
    {
        var members = MembersOf(documentType).ToArray();
        var attributedProperty = members.OfType<IPropertySymbol>().FirstOrDefault(HasIdentityAttribute);
        var attributedField = members.OfType<IFieldSymbol>().FirstOrDefault(HasIdentityAttribute);
        var idProperty = members.OfType<IPropertySymbol>().FirstOrDefault(_ => string.Equals(_.Name, "Id", StringComparison.OrdinalIgnoreCase));
        var idField = members.OfType<IFieldSymbol>().FirstOrDefault(_ => string.Equals(_.Name, "Id", StringComparison.OrdinalIgnoreCase));
        return (ISymbol?)attributedProperty ?? (ISymbol?)attributedField ?? (ISymbol?)idProperty ?? idField;
    }

    static IEnumerable<ISymbol> MembersOf(INamedTypeSymbol type)
    {
        for (var current = type; current is not null; current = current.BaseType)
        {
            foreach (var member in current.GetMembers().Where(_ => !_.IsStatic))
            {
                yield return member;
            }
        }
    }

    static bool HasIdentityAttribute(ISymbol member) => member.GetAttributes().Any(_ =>
        _.AttributeClass is not null &&
        DotNetSubjectIds.MetadataName(_.AttributeClass) == WellKnownTypes.JasperFxIdentityAttribute);

    static int SourceOrder(ISymbol member) => member.Locations
        .Where(_ => _.IsInSource)
        .Select(_ => _.SourceSpan.Start)
        .DefaultIfEmpty(int.MaxValue)
        .Min();

    static bool IsIdentityConfiguration(IMethodSymbol method)
    {
        var candidate = method.ReducedFrom ?? method;
        return candidate.Name == "Identity" &&
               DotNetSubjectIds.MetadataName(candidate.ContainingType.OriginalDefinition) == WellKnownTypes.MartenDocumentMappingExpression;
    }

    static Evidence UsageEvidence(
        DotNetProjectCompilation project,
        AdapterIdentity adapter,
        InvocationExpressionSyntax invocation,
        string methodName) => new()
        {
            Adapter = adapter,
            Strength = EvidenceStrength.Exact,
            Source = CritterStackSource.RangeForProject(invocation.GetLocation(), project),
            Explanation = $"Marten document usage through {methodName}"
        };

    static RelationshipKind? RelationshipKindFor(string methodName)
    {
        if (_readMethods.Contains(methodName)) return RelationshipKind.Reads;
        if (_storeMethods.Contains(methodName)) return RelationshipKind.Stores;
        if (_updateMethods.Contains(methodName)) return RelationshipKind.Updates;
        if (_deleteMethods.Contains(methodName)) return RelationshipKind.Deletes;
        return null;
    }

    static INamedTypeSymbol? DocumentTypeFrom(
        IMethodSymbol method,
        InvocationExpressionSyntax invocation,
        SemanticModel semanticModel)
    {
        var candidate = method.ReducedFrom ?? method;
        var typeArgument = method.TypeArguments.Concat(candidate.TypeArguments)
            .OfType<INamedTypeSymbol>()
            .FirstOrDefault(IsSourceType);
        return typeArgument ?? invocation.ArgumentList.Arguments
            .Select(_ => semanticModel.GetTypeInfo(_.Expression).Type)
            .OfType<INamedTypeSymbol>()
            .FirstOrDefault(IsSourceType);
    }

    static bool IsMarten(IMethodSymbol method)
    {
        var candidate = method.ReducedFrom ?? method;
        var @namespace = candidate.ContainingNamespace.ToDisplayString();
        return @namespace == "Marten" ||
               @namespace.StartsWith("Marten.", StringComparison.Ordinal) ||
               @namespace == "Wolverine.Marten" ||
               @namespace.StartsWith("Wolverine.Marten.", StringComparison.Ordinal);
    }

    static bool IsInEventProjection(
        InvocationExpressionSyntax invocation,
        SemanticModel semanticModel) =>
        semanticModel.GetEnclosingSymbol(invocation.SpanStart) is IMethodSymbol containingMethod &&
        DotNetSymbols.IsOrInheritsFrom(containingMethod.ContainingType, WellKnownTypes.MartenEventProjection);

    static bool IsInUnresolvedCustomProcessor(
        InvocationExpressionSyntax invocation,
        SemanticModel semanticModel) =>
        semanticModel.GetEnclosingSymbol(invocation.SpanStart) is IMethodSymbol containingMethod &&
        MartenConfigurationDiscovery.IsUnresolvedProcessorType(containingMethod.ContainingType);

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
        Evidence evidence) => new()
        {
            Id = new FactId { Value = id },
            Subject = source,
            Definition = new RelationshipDefinition
            {
                Key = new RelationshipKey { Kind = kind, Source = source, Target = target }
            },
            Evidence = evidence
        };

    static string LowerFirst(string value) => value.Length == 0
        ? value
        : $"{char.ToLowerInvariant(value[0])}{value[1..]}";

    sealed record DocumentProperty(IPropertySymbol Member, PropertyDefinition Definition);

    sealed record DocumentObservation(
        INamedTypeSymbol Type,
        Evidence RegistrationEvidence,
        ISymbol? IdentityMember = null,
        Evidence? IdentityEvidence = null,
        bool IdentityConfigurationUnresolved = false);
}
