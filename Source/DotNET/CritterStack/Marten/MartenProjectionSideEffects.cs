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
        CritterStackSubjectResolver subjects,
        ProjectionRegistration registration,
        bool sideEffectsEnabled)
    {
        var projection = registration.Projection ?? registration.Model;
        var projectionSubject = registration.Kind == ProjectionKind.Event
            ? subjects.SubjectForType(project, projection)
            : new SubjectId { Value = $"{subjects.SubjectForType(project, projection).Value}#reducer" };
        var scan = new SideEffectScan(project, adapter, subjects, projection, projectionSubject, sideEffectsEnabled);

        foreach (var tree in project.Compilation.SyntaxTrees
                     .Where(_ => project.AuthoredSyntaxTrees.Contains(_) && !DotNetGeneratedSource.IsGenerated(_))
                     .OrderBy(_ => _.FilePath, StringComparer.Ordinal))
        {
            var semanticModel = project.Compilation.GetSemanticModel(tree);
            foreach (var invocation in tree.GetRoot().DescendantNodes().OfType<InvocationExpressionSyntax>().OrderBy(_ => _.SpanStart))
            {
                scan.Visit(invocation, semanticModel);
            }
        }

        return new(scan.Facts, scan.Diagnostics);
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

    sealed class SideEffectScan(
        DotNetProjectCompilation project,
        AdapterIdentity adapter,
        CritterStackSubjectResolver subjects,
        INamedTypeSymbol projection,
        SubjectId projectionSubject,
        bool sideEffectsEnabled)
    {
        readonly HashSet<string> _messageArtifactIds = new(StringComparer.Ordinal);

        public List<GenerationFact> Facts { get; } = [];

        public List<GenerationDiagnostic> Diagnostics { get; } = [];

        public void Visit(InvocationExpressionSyntax invocation, SemanticModel semanticModel)
        {
            if (!IsProjectionPublishMessage(invocation, semanticModel))
            {
                return;
            }

            var argument = invocation.ArgumentList.Arguments.FirstOrDefault()?.Expression;
            if (argument is not (ObjectCreationExpressionSyntax or ImplicitObjectCreationExpressionSyntax) ||
                semanticModel.GetTypeInfo(argument).Type is not INamedTypeSymbol messageType ||
                messageType.TypeKind == TypeKind.Error)
            {
                Diagnostics.Add(Unresolved(
                    invocation,
                    GenerationDiagnosticOutcome.Unknown,
                    $"Projection '{projection.Name}' publishes a message through a non-literal payload whose type could not be resolved safely"));
                return;
            }

            if (IsSendMyselfPayload(messageType))
            {
                Diagnostics.Add(Unresolved(
                    invocation,
                    GenerationDiagnosticOutcome.Unsupported,
                    $"Projection '{projection.Name}' supplies ISendMyself payload '{messageType.Name}'; custom ApplyAsync sending behavior is not interpreted, and the wrapper is not inferred as a published domain message"));
                return;
            }

            AddPublication(invocation, messageType);
        }

        bool IsProjectionPublishMessage(InvocationExpressionSyntax invocation, SemanticModel semanticModel) =>
            semanticModel.GetSymbolInfo(invocation).Symbol is IMethodSymbol method &&
            string.Equals(method.Name, "PublishMessage", StringComparison.Ordinal) &&
            DotNetSubjectIds.MetadataName(method.ContainingType.OriginalDefinition) == WellKnownTypes.JasperFxEventSlice &&
            semanticModel.GetEnclosingSymbol(invocation.SpanStart) is IMethodSymbol containingMethod &&
            SymbolEqualityComparer.Default.Equals(containingMethod.ContainingType, projection);

        bool IsSendMyselfPayload(INamedTypeSymbol messageType) =>
            project.Compilation.GetTypeByMetadataName(WellKnownTypes.WolverineSendMyself) is { } sendMyself &&
            Wolverine.WolverineSymbolAuthority.IsAuthoredOrMetadataSymbol(sendMyself, project) &&
            messageType.AllInterfaces.Any(contract => SymbolEqualityComparer.Default.Equals(contract, sendMyself)) &&
            !Wolverine.WolverineReturnConsequences.IsTimeoutMessage(messageType);

        GenerationDiagnostic Unresolved(InvocationExpressionSyntax invocation, GenerationDiagnosticOutcome outcome, string message) => new()
        {
            Code = MartenDiagnosticCodes.ProjectionSideEffectUnresolved,
            Severity = GenerationDiagnosticSeverity.Warning,
            Outcome = outcome,
            Message = message,
            Source = CritterStackSource.RangeForProject(invocation.GetLocation(), project),
            Subject = projectionSubject
        };

        void AddPublication(InvocationExpressionSyntax invocation, INamedTypeSymbol messageType)
        {
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
            var messageSubject = subjects.SubjectForType(project, messageType);
            var artifactId = $"wolverine:message:{messageSubject.Value}";
            if (_messageArtifactIds.Add(artifactId))
            {
                Facts.Add(Artifact(
                    artifactId,
                    messageSubject,
                    messageType.Name,
                    SourceFileOf(messageType, project),
                    DotNetTypeShapes.PropertiesOf(messageType),
                    evidence));
            }

            Facts.Add(Relationship(
                $"marten:projection:publishes:{projectionSubject.Value}:{messageSubject.Value}:{invocation.SpanStart}",
                projectionSubject,
                messageSubject,
                evidence));
        }
    }
}
