// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Cratis.CritterStack.Screenplay.for_CritterStackScreenplayAdapter;

public class when_an_enum_is_shared_by_marten_and_wolverine : given.an_enum_application
{
    AdapterContribution _contribution = null!;

    void Because() => _contribution = new CritterStackScreenplayAdapter().Analyze(new([Project]), new());

    [Fact] void should_emit_the_marten_document() => Artifacts.Any(_ => _.Definition.Key.Kind == ArtifactKind.Document && _.Definition.Name == "Order").ShouldBeTrue();
    [Fact] void should_emit_the_wolverine_command() => Artifacts.Any(_ => _.Definition.Key.Kind == ArtifactKind.Command && _.Definition.Name == "PlaceOrder").ShouldBeTrue();
    [Fact] void should_emit_the_wolverine_event() => Artifacts.Any(_ => _.Definition.Key.Kind == ArtifactKind.Event && _.Definition.Name == "OrderPlaced").ShouldBeTrue();
    [Fact] void should_emit_one_concept() => Artifacts.Count(_ => _.Definition.Key.Kind == ArtifactKind.Concept).ShouldEqual(1);
    [Fact] void should_emit_one_representation() => _contribution.Facts.OfType<ConceptRepresentationFact>().Count().ShouldEqual(1);
    [Fact] void should_retain_declaration_order_and_numeric_aliases() => Representation.Definition.EnumerationValues.SequenceEqual(["Urgent", "Normal", "Alias", "low"]).ShouldBeTrue();
    [Fact] void should_emit_an_enumeration() => Representation.Definition.Kind.ShouldEqual(ConceptRepresentationKind.Enumeration);
    [Fact] void should_not_invent_a_primitive_backing() => Representation.Definition.Primitive.ShouldBeNull();
    [Fact] void should_use_exact_declaration_evidence() => Representation.Evidence.Strength.ShouldEqual(EvidenceStrength.Exact);
    [Fact] void should_locate_the_full_enum_declaration() => Representation.Evidence.Source.ShouldEqual(new SourceRange { Path = "Ordering/Priority.cs", FileIdentity = new SourceFileIdentity { Project = "Ordering/Ordering", Path = "Priority.cs" }, StartLine = 2, StartColumn = 1, EndLine = 2, EndColumn = EnumDeclaration.Length + 1 });
    [Fact] void should_use_the_same_evidence_for_the_concept() => Artifacts.Single(_ => _.Definition.Key.Kind == ArtifactKind.Concept).Evidence.ShouldEqual(Representation.Evidence);
    [Fact] void should_bind_every_emitted_priority_property() => Artifacts.SelectMany(_ => _.Definition.Properties).Where(_ => _.Name == "priority").All(_ => _.Type.Subject == Representation.Subject).ShouldBeTrue();
    [Fact] void should_use_the_adapter_and_subject_for_stable_fact_identity() => Representation.Id.Value.ShouldEqual($"cratis.critter-stack:concept-representation:{Representation.Subject.Value}");

    IEnumerable<ArtifactFact> Artifacts => _contribution.Facts.OfType<ArtifactFact>();
    ConceptRepresentationFact Representation => _contribution.Facts.OfType<ConceptRepresentationFact>().Single();
}
