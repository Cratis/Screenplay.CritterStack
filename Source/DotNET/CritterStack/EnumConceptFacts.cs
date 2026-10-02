// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Cratis.Screenplay.Generation;
using Cratis.Screenplay.Generation.DotNet;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Cratis.CritterStack.Screenplay;

static class EnumConceptFacts
{
    public static AdapterContribution AddTo(DotNetAnalysisContext context, AdapterContribution contribution)
    {
        var facts = contribution.Facts.ToList();
        var diagnostics = contribution.Diagnostics.ToList();
        var artifacts = facts.OfType<ArtifactFact>().ToArray();
        var referencedSubjects = ConceptTypeReferenceBinder.ReferencedSubjects(context, artifacts);
        var concepts = artifacts
            .Where(_ => _.Definition.Key.Kind == ArtifactKind.Concept)
            .Select(_ => _.Subject)
            .ToHashSet();
        var candidates = context.Projects
            .SelectMany(project => new DotNetArtifactCatalog(project.Compilation).Types
                .Where(type => type.TypeKind == TypeKind.Enum)
                .Select(type => new { Project = project, Type = type, Subject = context.SubjectForType(type) }))
            .Where(_ => _.Subject is not null && referencedSubjects.Contains(_.Subject))
            .OrderBy(_ => _.Subject!.Value, StringComparer.Ordinal);

        foreach (var candidate in candidates)
        {
            var declaration = DotNetSource.AuthoredDeclarationsOf(candidate.Type, candidate.Project.AuthoredSyntaxTrees)
                .Where(_ => !DotNetGeneratedSource.IsGenerated(_.SyntaxTree))
                .Select(_ => _.GetSyntax())
                .OfType<EnumDeclarationSyntax>()
                .SingleOrDefault();
            if (declaration is null)
            {
                continue;
            }

            var subject = candidate.Subject!;
            var evidence = CritterStackSource.EvidenceFor(
                candidate.Type,
                contribution.Adapter,
                candidate.Project,
                EvidenceStrength.Exact,
                $"Authored enum '{candidate.Type.Name}' declares its named values");
            if (DotNetSymbols.HasAttribute(candidate.Type, "System.FlagsAttribute"))
            {
                diagnostics.Add(new GenerationDiagnostic
                {
                    Code = CritterStackDiagnosticCodes.FlagsEnumOmitted,
                    Severity = GenerationDiagnosticSeverity.Warning,
                    Outcome = GenerationDiagnosticOutcome.Unsupported,
                    Subject = subject,
                    Source = evidence.Source,
                    Message = $"Flags enum '{candidate.Type.Name}' permits combinations that cannot be represented by an enumeration concept; its field types were left unchanged"
                });
                continue;
            }

            if (concepts.Add(subject))
            {
                facts.Add(new ArtifactFact
                {
                    Id = new FactId { Value = $"{contribution.Adapter.Id}:concept:{subject.Value}" },
                    Subject = subject,
                    Evidence = evidence,
                    Definition = new ArtifactDefinition
                    {
                        Key = new ArtifactKey { Subject = subject, Kind = ArtifactKind.Concept },
                        Name = candidate.Type.Name,
                        File = evidence.Source?.Path
                    }
                });
            }

            facts.Add(new ConceptRepresentationFact
            {
                Id = new FactId { Value = $"{contribution.Adapter.Id}:concept-representation:{subject.Value}" },
                Subject = subject,
                Evidence = evidence,
                Definition = new ConceptRepresentationDefinition
                {
                    Concept = subject,
                    Kind = ConceptRepresentationKind.Enumeration,
                    EnumerationValues = [.. declaration.Members.Select(_ => _.Identifier.ValueText)]
                }
            });
        }

        // Bind here for direct adapter consumers; the generator also binds independently contributed Vogen concepts.
        return ConceptTypeReferenceBinder.Bind(context, [contribution with { Facts = facts, Diagnostics = diagnostics }])[0];
    }
}
