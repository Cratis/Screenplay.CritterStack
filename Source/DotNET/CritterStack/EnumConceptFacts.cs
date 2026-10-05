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
                    Message = $"Flags enum '{candidate.Type.Name}' permits combinations that cannot be represented by an enumeration concept; it is not declared as a concept and its uses keep the plain type name without a concept reference"
                });
                continue;
            }

            var values = declaration.Members.Select(_ => _.Identifier.ValueText).ToArray();
            if (!CanLower(values))
            {
                diagnostics.Add(new GenerationDiagnostic
                {
                    Code = CritterStackDiagnosticCodes.EnumValuesUnsupported,
                    Severity = GenerationDiagnosticSeverity.Warning,
                    Outcome = GenerationDiagnosticOutcome.Unsupported,
                    Subject = subject,
                    Source = evidence.Source,
                    Message = values.Length == 0
                        ? $"Enum '{candidate.Type.Name}' declares no named values, which an enumeration concept cannot represent; it is not declared as a concept and its uses keep the plain type name without a concept reference"
                        : $"Enum '{candidate.Type.Name}' has values that collide or are not valid Screenplay identifiers after Screenplay naming; it is not declared as a concept and its uses keep the plain type name without a concept reference"
                });
                continue;
            }

            if (concepts.Add(subject))
            {
                facts.Add(new ArtifactFact
                {
                    Id = new FactId { Value = ConceptFactId(contribution.Adapter, subject) },
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
                Id = new FactId { Value = RepresentationFactId(contribution.Adapter, subject) },
                Subject = subject,
                Evidence = evidence,
                Definition = new ConceptRepresentationDefinition
                {
                    Concept = subject,
                    Kind = ConceptRepresentationKind.Enumeration,
                    EnumerationValues = [.. values]
                }
            });
        }

        // Bind here for direct adapter consumers; the generator also binds independently contributed Vogen concepts.
        var resolved = WithoutConflictingNames([contribution with { Facts = facts, Diagnostics = diagnostics }]);
        return ConceptTypeReferenceBinder.Bind(context, resolved)[0];
    }

    /// <summary>
    /// Removes enum concepts whose concept name is also required by another source subject.
    /// </summary>
    /// <remarks>
    /// The shared lowerer names a concept by its definition name and rejects a name required by more than one subject
    /// (GEN0015), which fails generation. An enum concept involved in such a collision is dropped together with its
    /// representation, and its uses keep the plain type name; concepts contributed by other conventions (Vogen, Marten
    /// value types) are never dropped, so a remaining collision between them stays the lowerer's to report.
    /// </remarks>
    /// <param name="contributions">The contributions to normalize together.</param>
    /// <returns>The contributions without conflicting enum concepts, with a located diagnostic for each dropped enum.</returns>
    public static IReadOnlyList<AdapterContribution> WithoutConflictingNames(IReadOnlyList<AdapterContribution> contributions)
    {
        var concepts = contributions
            .SelectMany(_ => _.Facts)
            .OfType<ArtifactFact>()
            .Where(_ => _.Definition.Key.Kind == ArtifactKind.Concept)
            .ToArray();
        var enumRepresentations = contributions
            .SelectMany(_ => _.Facts)
            .OfType<ConceptRepresentationFact>()
            .Where(IsEnumRepresentation)
            .Select(_ => _.Definition.Concept)
            .ToHashSet();
        var enumSubjects = concepts
            .GroupBy(_ => _.Subject)
            .Where(_ => enumRepresentations.Contains(_.Key) && _.All(IsEnumConcept))
            .Select(_ => _.Key)
            .ToHashSet();
        var conflicts = concepts
            .GroupBy(_ => _.Definition.Name, StringComparer.Ordinal)
            .Select(_ => new { Name = _.Key, Subjects = _.Select(fact => fact.Subject).Distinct().ToArray() })
            .Where(_ => _.Subjects.Length > 1)
            .SelectMany(conflict => conflict.Subjects
                .Where(enumSubjects.Contains)
                .Select(subject => new { Subject = subject, conflict.Name, Others = conflict.Subjects.Length - 1 }))
            .GroupBy(_ => _.Subject)
            .ToDictionary(_ => _.Key, _ => _.OrderBy(conflict => conflict.Name, StringComparer.Ordinal).First());
        if (conflicts.Count == 0)
        {
            return contributions;
        }

        var reporters = contributions
            .SelectMany((contribution, index) => contribution.Facts
                .OfType<ArtifactFact>()
                .Where(_ => _.Definition.Key.Kind == ArtifactKind.Concept && conflicts.ContainsKey(_.Subject))
                .Select(fact => new { Index = index, Fact = fact }))
            .GroupBy(_ => _.Fact.Subject)
            .Select(_ => _.First())
            .ToArray();
        return
        [
            .. contributions.Select((contribution, index) =>
            {
                var diagnostics = contribution.Diagnostics.ToList();
                diagnostics.AddRange(reporters
                    .Where(_ => _.Index == index)
                    .OrderBy(_ => _.Fact.Subject.Value, StringComparer.Ordinal)
                    .Select(_ => ConflictDiagnostic(_.Fact, conflicts[_.Fact.Subject].Name, conflicts[_.Fact.Subject].Others)));

                return contribution with
                {
                    Facts =
                    [
                        .. contribution.Facts.Where(fact => fact switch
                        {
                            ArtifactFact artifact => artifact.Definition.Key.Kind != ArtifactKind.Concept || !conflicts.ContainsKey(artifact.Subject),
                            ConceptRepresentationFact representation => !conflicts.ContainsKey(representation.Definition.Concept) || !IsEnumRepresentation(representation),
                            _ => true
                        })
                    ],
                    Diagnostics = diagnostics
                };
            })
        ];
    }

    static GenerationDiagnostic ConflictDiagnostic(ArtifactFact concept, string name, int others) => new()
    {
        Code = CritterStackDiagnosticCodes.EnumConceptNameConflict,
        Severity = GenerationDiagnosticSeverity.Warning,
        Outcome = GenerationDiagnosticOutcome.Conflict,
        Subject = concept.Subject,
        Source = concept.Evidence.Source,
        Message = $"Enum '{name}' shares its Screenplay concept name with {others} other source subject{(others == 1 ? string.Empty : "s")}; it is not declared as a concept and its uses keep the plain type name without a concept reference"
    };

    static string ConceptFactId(AdapterIdentity adapter, SubjectId subject) => $"{adapter.Id}:concept:{subject.Value}";

    static string RepresentationFactId(AdapterIdentity adapter, SubjectId subject) => $"{adapter.Id}:concept-representation:{subject.Value}";

    static bool IsEnumConcept(ArtifactFact fact) =>
        string.Equals(fact.Id.Value, ConceptFactId(fact.Evidence.Adapter, fact.Subject), StringComparison.Ordinal);

    static bool IsEnumRepresentation(ConceptRepresentationFact fact) =>
        fact.Definition.Kind == ConceptRepresentationKind.Enumeration &&
        string.Equals(fact.Id.Value, RepresentationFactId(fact.Evidence.Adapter, fact.Subject), StringComparison.Ordinal);

    // Mirrors ScreenplayLowerer (Screenplay.Generation 0.18): an enumeration concept needs at least one value, and every
    // value must remain a distinct, non-reserved Screenplay identifier after its first character is lowercased.
    static bool CanLower(IReadOnlyList<string> values)
    {
        var lowered = values.Select(ScreenplayEnumValue).ToArray();
        return lowered.Length > 0 &&
            lowered.All(IsScreenplayEnumValue) &&
            lowered.Distinct(StringComparer.Ordinal).Count() == lowered.Length;
    }

    static string ScreenplayEnumValue(string value) => value.Length == 0
        ? value
        : $"{char.ToLowerInvariant(value[0])}{value[1..]}";

    static bool IsScreenplayEnumValue(string value) =>
        value.Length > 0 &&
        (value[0] == '_' || char.IsAsciiLetterLower(value[0])) &&
        value.Skip(1).All(_ => _ == '_' || char.IsLetterOrDigit(_)) &&
        !string.Equals(value, "file", StringComparison.Ordinal) &&
        !string.Equals(value, "validate", StringComparison.Ordinal);
}
