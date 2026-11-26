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
        MartenCollectedFacts collected,
        ProjectionRegistration registration,
        bool sideEffectsEnabled)
    {
        var (project, subjects) = (collected.Project, collected.Subjects);
        var projection = registration.Projection ?? registration.Model;
        var projectionSubject = registration.Kind == ProjectionKind.Event
            ? subjects.SubjectForType(project, projection)
            : new SubjectId { Value = $"{subjects.SubjectForType(project, projection).Value}#reducer" };
        var scan = new SideEffectScan(collected.WithOwnership(collected.Ownership, []), projection, projectionSubject, sideEffectsEnabled);

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

    sealed class SideEffectScan(
        MartenCollectedFacts collected,
        INamedTypeSymbol projection,
        SubjectId projectionSubject,
        bool sideEffectsEnabled)
    {
        readonly HashSet<string> _messageArtifactIds = [with(StringComparer.Ordinal)];

        public List<GenerationFact> Facts => collected.Facts;

        public List<GenerationDiagnostic> Diagnostics { get; } = [];

        public void Visit(InvocationExpressionSyntax invocation, SemanticModel semanticModel)
        {
            if (!IsProjectionPublishMessage(invocation, semanticModel))
            {
                return;
            }

            var argument = invocation.ArgumentList.Arguments.FirstOrDefault()?.Expression;
            if (PayloadType(argument, semanticModel) is not { } messageType)
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

        static INamedTypeSymbol? PayloadType(ExpressionSyntax? argument, SemanticModel semanticModel)
        {
            if (argument is not (ObjectCreationExpressionSyntax or ImplicitObjectCreationExpressionSyntax))
            {
                return null;
            }

            var type = semanticModel.GetTypeInfo(argument).Type as INamedTypeSymbol;
            return type?.TypeKind == TypeKind.Error ? null : type;
        }

        bool IsProjectionPublishMessage(InvocationExpressionSyntax invocation, SemanticModel semanticModel) =>
            semanticModel.GetSymbolInfo(invocation).Symbol is IMethodSymbol method &&
            string.Equals(method.Name, "PublishMessage", StringComparison.Ordinal) &&
            DotNetSubjectIds.MetadataName(method.ContainingType.OriginalDefinition) == WellKnownTypes.JasperFxEventSlice &&
            semanticModel.GetEnclosingSymbol(invocation.SpanStart) is IMethodSymbol containingMethod &&
            SymbolEqualityComparer.Default.Equals(containingMethod.ContainingType, projection);

        bool IsSendMyselfPayload(INamedTypeSymbol messageType) =>
            collected.Project.Compilation.GetTypeByMetadataName(WellKnownTypes.WolverineSendMyself) is { } sendMyself &&
            Wolverine.WolverineSymbolAuthority.IsAuthoredOrMetadataSymbol(sendMyself, collected.Project) &&
            messageType.AllInterfaces.Any(contract => SymbolEqualityComparer.Default.Equals(contract, sendMyself)) &&
            !Wolverine.WolverineReturnConsequences.IsTimeoutMessage(messageType);

        GenerationDiagnostic Unresolved(InvocationExpressionSyntax invocation, GenerationDiagnosticOutcome outcome, string message) => new()
        {
            Code = MartenDiagnosticCodes.ProjectionSideEffectUnresolved,
            Severity = GenerationDiagnosticSeverity.Warning,
            Outcome = outcome,
            Message = message,
            Source = CritterStackSource.RangeForProject(invocation.GetLocation(), collected.Project),
            Subject = projectionSubject
        };

        void AddPublication(InvocationExpressionSyntax invocation, INamedTypeSymbol messageType)
        {
            var explanation = sideEffectsEnabled
                ? "This projection publishes a message as an inline side effect"
                : "This projection publishes a message as an inline side effect; side-effect option not observed in authored configuration";
            var evidence = new Evidence
            {
                Adapter = collected.Adapter,
                Strength = sideEffectsEnabled ? EvidenceStrength.Exact : EvidenceStrength.Conventional,
                Source = CritterStackSource.RangeForProject(invocation.GetLocation(), collected.Project),
                Explanation = explanation
            };
            var messageSubject = collected.Subjects.SubjectForType(collected.Project, messageType);
            var artifactId = $"wolverine:message:{messageSubject.Value}";
            if (_messageArtifactIds.Add(artifactId))
            {
                new MartenSourceArtifact
                {
                    Id = artifactId,
                    Subject = messageSubject,
                    Kind = ArtifactKind.Message,
                    Name = messageType.Name,
                    File = MartenSourceFiles.FileOf(messageType, collected.Project),
                    SourceType = messageType,
                    Evidence = evidence
                }.AddTo(collected);
            }

            Facts.Add(new MartenRelationshipFact
            {
                Id = $"marten:projection:publishes:{projectionSubject.Value}:{messageSubject.Value}:{invocation.SpanStart}",
                Source = projectionSubject,
                Kind = RelationshipKind.Publishes,
                Target = messageSubject,
                Evidence = evidence
            }.ToFact());
        }
    }
}
