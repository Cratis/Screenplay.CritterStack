// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Cratis.CritterStack.Screenplay.for_CritterStackScreenplayGenerator;

public class when_external_adapter_contributions_conflict : given.a_composed_vogen_critter_stack_application
{
    GeneratedScreenplayDefinition _result = null!;

    void Because() => _result = new CritterStackScreenplayGenerator(
        [
            new VogenConceptScreenplayAdapter(),
            new CritterStackScreenplayAdapter(),
            new ConflictingConceptAdapter()
        ]).Generate([Project], new CritterStackScreenplayOptions { Domain = "Ordering" });

    [Fact] void should_report_the_concept_representation_conflict() => _result.Diagnostics.Select(_ => _.Code).ShouldContain(GenerationDiagnosticCodes.ConflictingConceptRepresentation);
    [Fact] void should_keep_both_representation_variants() => _result.Graph.ConceptRepresentations.Single(_ => _.Concept.Value.EndsWith("/Ordering.CustomerCode", StringComparison.Ordinal)).Variants.Count.ShouldEqual(2);
    [Fact] void should_keep_vogen_provenance() => RepresentationAdapterIds().ShouldContain("vogen");
    [Fact] void should_keep_external_adapter_provenance() => RepresentationAdapterIds().ShouldContain("external.concepts");
    [Fact] void should_not_let_adapter_order_choose_a_representation() => _result.Source.ShouldNotContain("concept CustomerCode");

    IEnumerable<string> RepresentationAdapterIds() => _result.Graph.ConceptRepresentations
        .Single(_ => _.Concept.Value.EndsWith("/Ordering.CustomerCode", StringComparison.Ordinal))
        .Variants
        .SelectMany(_ => _.Evidence)
        .Select(_ => _.Adapter.Id);

    sealed class ConflictingConceptAdapter : IDotNetScreenplayAdapter
    {
        public AdapterIdentity Identity { get; } = new() { Id = "external.concepts", Version = "1.0.0" };

        public bool CanAnalyze(DotNetAnalysisContext context) => true;

        public AdapterContribution Analyze(DotNetAnalysisContext context, DotNetAdapterOptions options)
        {
            _ = options;
            var type = context.Projects.Single().Compilation.GetTypeByMetadataName("Ordering.CustomerCode")!;
            var subject = context.Projects.Single().SubjectForType(type);
            return new()
            {
                Adapter = Identity,
                Facts =
                [
                    new ConceptRepresentationFact
                    {
                        Id = new FactId { Value = "external:customer-code:representation" },
                        Subject = subject,
                        Definition = new ConceptRepresentationDefinition
                        {
                            Concept = subject,
                            Kind = ConceptRepresentationKind.Primitive,
                            Primitive = GenerationPrimitiveKind.Number
                        },
                        Evidence = new Evidence { Adapter = Identity, Strength = EvidenceStrength.Exact }
                    }
                ]
            };
        }
    }
}
