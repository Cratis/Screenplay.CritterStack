// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Cratis.Screenplay.Generation;
using Cratis.Screenplay.Generation.DotNet;
using Microsoft.CodeAnalysis;

namespace Cratis.CritterStack.Screenplay;

internal static partial class EnumConceptFacts
{
    /// <summary>
    /// Emits source-native enum concept declarations, representations and omissions for the enums a contribution's own
    /// observed member types use, leaving name conflicts and captured names to the published concept-name yield.
    /// </summary>
    /// <param name="context">The analysis context.</param>
    /// <param name="contribution">The contribution holding the observed member type uses and registered concepts.</param>
    /// <returns>The contribution with enum concept facts added.</returns>
    public static AdapterContribution AddToNative(DotNetAnalysisContext context, AdapterContribution contribution)
    {
        var typeUses = contribution.Facts.OfType<ArtifactMemberTypeUseFact>()
            .Where(_ => _.Definition.Type.ObservedTypeSubject is not null && !MixesOptionalAndCollection(_.Definition.Type))
            .ToArray();
        var referenced = typeUses.Select(_ => _.Definition.Type.ObservedTypeSubject!).ToHashSet();
        var registered = contribution.Facts.OfType<ArtifactDeclarationFact>()
            .Where(_ => _.Definition.Artifact.Kind == ArtifactKind.Concept)
            .Select(_ => _.Subject)
            .ToHashSet();
        var collector = new NativeEnumFactCollector(contribution, typeUses, registered);
        var candidates = context.Projects
            .SelectMany(project => new DotNetArtifactCatalog(project.Compilation).Types
                .Where(type => type.TypeKind == TypeKind.Enum)
                .Select(type => new { Project = project, Type = type, Subject = context.SubjectForType(type) }))
            .Where(_ => _.Subject is not null && referenced.Contains(_.Subject))
            .OrderBy(_ => _.Subject!.Value, StringComparer.Ordinal);

        foreach (var candidate in candidates)
        {
            collector.Add(candidate.Project, candidate.Type, candidate.Subject!);
        }

        return collector.ToContribution();
    }

    // The compatibility path resolves no concept for a collection of optionals (or the reverse), so no enum concept is declared for it.
    static bool MixesOptionalAndCollection(TypeUseDefinition type) =>
        type.Shape.Contains(TypeUseShapeKind.Optional) && type.Shape.Contains(TypeUseShapeKind.Collection);

    static TypeUseTargetOmittedFact OmittedTargetFact(AdapterIdentity adapter, ArtifactMemberTypeUseFact typeUse, Evidence evidence, GenerationDiagnostic omission) => new()
    {
        Id = new FactId { Value = $"{adapter.Id}:enum-omitted:{typeUse.Id.Value}" },
        Subject = typeUse.Subject,
        Evidence = evidence,
        Definition = new TypeUseTargetOmittedDefinition { Member = typeUse.Definition.Member, Diagnostic = omission }
    };

    static ArtifactDeclarationFact YieldingConceptFact(AdapterIdentity adapter, SubjectId subject, string name, Evidence evidence)
    {
        var id = new FactId { Value = ConceptFactId(adapter, subject) };
        return new()
        {
            Id = id,
            Subject = subject,
            Evidence = evidence,
            Definition = new ArtifactDeclarationDefinition
            {
                Artifact = new ArtifactKey { Subject = subject, Kind = ArtifactKind.Concept },
                Name = name,
                File = evidence.Source?.Path,
                NameConflictYield = new ConceptNameYieldDefinition
                {
                    Diagnostic = new GenerationDiagnostic
                    {
                        Code = CritterStackDiagnosticCodes.EnumConceptNameConflict,
                        Severity = GenerationDiagnosticSeverity.Warning,
                        Outcome = GenerationDiagnosticOutcome.Conflict,
                        Subject = subject,
                        Source = evidence.Source,
                        Facts = [id],
                        Message = ConflictMessage(name)
                    }
                }
            }
        };
    }

    sealed class NativeEnumFactCollector(AdapterContribution contribution, ArtifactMemberTypeUseFact[] typeUses, HashSet<SubjectId> registered)
    {
        readonly List<GenerationFact> _facts = [.. contribution.Facts];
        readonly List<GenerationDiagnostic> _diagnostics = [.. contribution.Diagnostics];
        readonly HashSet<SubjectId> _declared = [.. registered];
        readonly HashSet<SubjectId> _dropped = [];

        public void Add(DotNetProjectCompilation project, INamedTypeSymbol type, SubjectId subject)
        {
            var declaration = AuthoredDeclaration(type, project);
            if (declaration is null)
            {
                return;
            }

            var evidence = CritterStackSource.EvidenceFor(
                type,
                contribution.Adapter,
                project,
                EvidenceStrength.Exact,
                $"Authored enum '{type.Name}' declares its named values");
            var enumValues = new EnumValues(
                [.. declaration.Members.Select(_ => _.Identifier.ValueText)],
                NumberingOf(type, declaration));
            if (OmissionOf(type, subject, evidence, enumValues) is { } omission)
            {
                Omit(subject, evidence, omission);
                return;
            }

            if (_declared.Add(subject))
            {
                _facts.Add(YieldingConceptFact(contribution.Adapter, subject, type.Name, evidence));
            }

            _facts.Add(RepresentationFact(contribution.Adapter, subject, evidence, enumValues));
        }

        public AdapterContribution ToContribution() => contribution with
        {
            Facts = [.. _facts.Where(fact => !IsDroppedRegistration(fact))],
            Diagnostics = _diagnostics
        };

        void Omit(SubjectId subject, Evidence evidence, GenerationDiagnostic omission)
        {
            var unnumberable = omission.Code == CritterStackDiagnosticCodes.EnumNumbersNotPreserved;
            if (registered.Contains(subject) && !unnumberable)
            {
                // A registered concept stays declared, so its uses keep referencing it and the omission has no use to attach to.
                _diagnostics.Add(omission);
                return;
            }

            if (unnumberable)
            {
                _dropped.Add(subject);
            }

            _facts.AddRange(typeUses
                .Where(_ => _.Definition.Type.ObservedTypeSubject == subject)
                .Select(use => OmittedTargetFact(contribution.Adapter, use, evidence, omission)));
        }

        // An enum whose numbering cannot be preserved is not a concept from any source, including Marten RegisterValueType.
        bool IsDroppedRegistration(GenerationFact fact) =>
            fact is ArtifactDeclarationFact { Definition.Artifact.Kind: ArtifactKind.Concept } declaration && _dropped.Contains(declaration.Subject);
    }
}
