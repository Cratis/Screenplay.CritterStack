// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Cratis.Screenplay.Generation;
using Cratis.Screenplay.Generation.DotNet;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Cratis.CritterStack.Screenplay.Marten;

sealed record MartenProjectionSideEffectResult(
    IReadOnlyList<GenerationFact> Facts,
    IReadOnlyList<GenerationDiagnostic> Diagnostics);

static class MartenProjectionSideEffects
{
    public static MartenProjectionSideEffectResult Discover(
        DotNetProjectCompilation project,
        AdapterIdentity adapter,
        ProjectionRegistration registration,
        bool sideEffectsEnabled)
    {
        var projection = registration.Projection ?? registration.Model;
        var projectionSubject = registration.Kind == ProjectionKind.Event
            ? project.SubjectForType(projection)
            : new SubjectId { Value = $"{project.SubjectForType(projection).Value}#reducer" };
        var facts = new List<GenerationFact>();
        var diagnostics = new List<GenerationDiagnostic>();
        var messageArtifactIds = new HashSet<string>(StringComparer.Ordinal);

        foreach (var tree in project.Compilation.SyntaxTrees
                     .Where(_ => project.AuthoredSyntaxTrees.Contains(_) && !DotNetGeneratedSource.IsGenerated(_))
                     .OrderBy(_ => _.FilePath, StringComparer.Ordinal))
        {
            var semanticModel = project.Compilation.GetSemanticModel(tree);
            foreach (var invocation in tree.GetRoot().DescendantNodes().OfType<InvocationExpressionSyntax>().OrderBy(_ => _.SpanStart))
            {
                if (semanticModel.GetSymbolInfo(invocation).Symbol is not IMethodSymbol method ||
                    !string.Equals(method.Name, "PublishMessage", StringComparison.Ordinal) ||
                    DotNetSubjectIds.MetadataName(method.ContainingType.OriginalDefinition) != WellKnownTypes.JasperFxEventSlice ||
                    semanticModel.GetEnclosingSymbol(invocation.SpanStart) is not IMethodSymbol containingMethod ||
                    !SymbolEqualityComparer.Default.Equals(containingMethod.ContainingType, projection))
                {
                    continue;
                }

                var argument = invocation.ArgumentList.Arguments.FirstOrDefault()?.Expression;
                if (argument is not (ObjectCreationExpressionSyntax or ImplicitObjectCreationExpressionSyntax) ||
                    semanticModel.GetTypeInfo(argument).Type is not INamedTypeSymbol messageType ||
                    messageType.TypeKind == TypeKind.Error)
                {
                    diagnostics.Add(new()
                    {
                        Code = MartenDiagnosticCodes.ProjectionSideEffectUnresolved,
                        Severity = GenerationDiagnosticSeverity.Warning,
                        Outcome = GenerationDiagnosticOutcome.Unknown,
                        Message = $"Projection '{projection.Name}' publishes a message through a non-literal payload whose type could not be resolved safely",
                        Source = CritterStackSource.RangeForProject(invocation.GetLocation(), project),
                        Subject = projectionSubject
                    });
                    continue;
                }

                var explanation = sideEffectsEnabled
                    ? "This projection publishes a message as an inline side effect"
                    : "This projection publishes a message as an inline side effect; side-effect option not observed in authored configuration";
                var evidence = new Evidence
                {
                    Adapter = adapter,
                    Strength = sideEffectsEnabled ? EvidenceStrength.Exact : EvidenceStrength.Conventional,
                    Source = CritterStackSource.RangeForProject(invocation.GetLocation(), project),
                    Explanation = explanation
                };
                var messageSubject = project.SubjectForType(messageType);
                var artifactId = $"wolverine:message:{messageSubject.Value}";
                if (messageArtifactIds.Add(artifactId))
                {
                    facts.Add(Artifact(
                        artifactId,
                        messageSubject,
                        messageType.Name,
                        SourceFileOf(messageType, project),
                        DotNetTypeShapes.PropertiesOf(messageType),
                        evidence));
                }

                facts.Add(Relationship(
                    $"marten:projection:publishes:{projectionSubject.Value}:{messageSubject.Value}:{invocation.SpanStart}",
                    projectionSubject,
                    messageSubject,
                    evidence));
            }
        }

        return new(facts, diagnostics);
    }

    static string? SourceFileOf(ISymbol symbol, DotNetProjectCompilation project) =>
        CritterStackSource.EvidenceFor(symbol, new AdapterIdentity { Id = "source", Version = "1" }, project, EvidenceStrength.Exact).Source?.Path;

    static ArtifactFact Artifact(
        string id,
        SubjectId subject,
        string name,
        string? file,
        IReadOnlyList<PropertyDefinition> properties,
        Evidence evidence) => new()
        {
            Id = new FactId { Value = id },
            Subject = subject,
            Definition = new ArtifactDefinition
            {
                Key = new ArtifactKey { Subject = subject, Kind = ArtifactKind.Message },
                Name = name,
                File = file,
                Properties = properties
            },
            Evidence = evidence
        };

    static RelationshipFact Relationship(
        string id,
        SubjectId source,
        SubjectId target,
        Evidence evidence) => new()
        {
            Id = new FactId { Value = id },
            Subject = source,
            Definition = new RelationshipDefinition
            {
                Key = new RelationshipKey
                {
                    Kind = RelationshipKind.Publishes,
                    Source = source,
                    Target = target
                }
            },
            Evidence = evidence
        };
}
