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
        AdapterIdentity adapter)
    {
        var facts = new List<GenerationFact>();
        var documents = new Dictionary<SubjectId, (INamedTypeSymbol Type, Evidence Evidence)>();
        foreach (var tree in project.Compilation.SyntaxTrees.Where(_ => !DotNetGeneratedSource.IsGenerated(_)))
        {
            var semanticModel = project.Compilation.GetSemanticModel(tree);
            foreach (var invocation in tree.GetRoot().DescendantNodes().OfType<InvocationExpressionSyntax>())
            {
                if (semanticModel.GetSymbolInfo(invocation).Symbol is not IMethodSymbol method || !IsMarten(method))
                {
                    continue;
                }

                var kind = RelationshipKindFor(method.Name);
                if (kind is null && !_configurationMethods.Contains(method.Name))
                {
                    continue;
                }

                var documentType = DocumentTypeFrom(method, invocation, semanticModel);
                if (documentType is null || !IsSourceType(documentType))
                {
                    continue;
                }

                var evidence = new Evidence
                {
                    Adapter = adapter,
                    Strength = EvidenceStrength.Exact,
                    Source = DotNetSource.Range(invocation.GetLocation(), project.SourceRoot),
                    Explanation = $"Marten document usage through {method.Name}"
                };
                var documentSubject = project.SubjectForType(documentType);
                documents[documentSubject] = (documentType, evidence);
                if (kind is not null && semanticModel.GetEnclosingSymbol(invocation.SpanStart) is IMethodSymbol containingMethod)
                {
                    var methodSubject = MethodSubject(project, containingMethod);
                    facts.Add(Artifact(
                        $"marten:handler:{methodSubject.Value}",
                        methodSubject,
                        ArtifactKind.Handler,
                        $"{containingMethod.ContainingType.Name}.{containingMethod.Name}",
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

        foreach (var (subject, document) in documents.OrderBy(_ => _.Key.Value, StringComparer.Ordinal))
        {
            facts.Add(Artifact(
                $"marten:document:{subject.Value}",
                subject,
                ArtifactKind.Document,
                document.Type.Name,
                SourceFileOf(document.Type, project),
                DotNetTypeShapes.PropertiesOf(document.Type),
                document.Evidence));
        }

        var diagnostics = documents
            .OrderBy(_ => _.Key.Value, StringComparer.Ordinal)
            .Select(_ => new GenerationDiagnostic
            {
                Code = MartenDiagnosticCodes.DocumentModelOmitted,
                Severity = GenerationDiagnosticSeverity.Information,
                Message = $"Marten document '{_.Value.Type.Name}' is persisted or queried directly, but the current Screenplay language has no ordinary document-state declaration",
                Source = _.Value.Evidence.Source,
                Subject = _.Key
            })
            .ToArray();

        return new(facts, diagnostics);
    }

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
        return @namespace.StartsWith("Marten", StringComparison.Ordinal) ||
               @namespace.StartsWith("Wolverine.Marten", StringComparison.Ordinal);
    }

    static bool IsSourceType(INamedTypeSymbol type) => type.TypeKind != TypeKind.Error && type.Locations.Any(_ => _.IsInSource);

    static SubjectId MethodSubject(DotNetProjectCompilation project, IMethodSymbol method) => new()
    {
        Value = $"{project.SubjectForType(method.ContainingType).Value}#method:{method.MetadataName}"
    };

    static string? SourceFileOf(ISymbol symbol, DotNetProjectCompilation project) =>
        DotNetSource.EvidenceFor(symbol, new AdapterIdentity { Id = "source", Version = "1" }, EvidenceStrength.Exact, project.SourceRoot).Source?.Path;

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
}
