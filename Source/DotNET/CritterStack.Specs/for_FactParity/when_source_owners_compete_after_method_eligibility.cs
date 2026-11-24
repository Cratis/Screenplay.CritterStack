// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using static Cratis.CritterStack.Screenplay.for_FactParity.given.fact_vectors;

namespace Cratis.CritterStack.Screenplay.for_FactParity;

/// <summary>Compares strongest-source and unique-producer fallback before and after exact method eligibility.</summary>
public class when_source_owners_compete_after_method_eligibility : Specification
{
    DotNetProjectCompilation _project = null!;
    DotNetAnalysisContext _context = null!;
    IArtifactSourcePlacementResolver _resolver = null!;
    ArtifactKey _event = null!;
    ArtifactKey _first = null!;
    ArtifactKey _second = null!;
    Evidence _evidence = null!;
    PlacementVector _single = null!;
    PlacementVector _ambiguous = null!;
    PlacementVector _strongSelf = null!;
    PlacementVector _ineligible = null!;

    void Establish()
    {
        _project = Project(Source);
        _context = new([_project]);
        _event = Key(_project, Type(_project, "FactParity.Sales.Orders.Events.OrderPlaced"), ArtifactKind.Event);
        _first = Key(_project, Type(_project, "FactParity.Sales.Orders.First.PlaceOrder"), ArtifactKind.Command);
        _second = Key(_project, Type(_project, "FactParity.Sales.Orders.Second.ImportOrder"), ArtifactKind.Command);
        _evidence = Evidence(_project, Type(_project, "FactParity.Sales.Orders.Events.OrderPlaced"), MartenAdapter);
        var structures = DotNetSourceStructures.Create(_context);
        var policies = structures.Structures.Select(_ => _.Project).Distinct(StringComparer.Ordinal).ToDictionary(
            project => project, _ => new DotNetSourceStructurePolicy { Version = 1, NamespaceSegmentsToSkip = 1 }, StringComparer.Ordinal);
        _resolver = new DotNetArtifactSourcePlacementResolver(structures, policies);
    }

    void Because()
    {
        _single = Observe(false, false, false);
        _ambiguous = Observe(true, false, false);
        _strongSelf = Observe(false, true, false);
        _ineligible = Observe(false, false, true);
    }

    [Fact] void should_select_the_unique_strongest_producer() => _single.Atomic.ShouldEqual(_single.Compatibility);
    [Fact] void should_fall_back_to_self_for_equally_strong_producers() => _ambiguous.Atomic.ShouldEqual(_ambiguous.Compatibility);
    [Fact] void should_prefer_a_stronger_self_observation() => _strongSelf.Atomic.ShouldEqual(_strongSelf.Compatibility);
    [Fact] void should_discard_an_ineligible_producer_before_selection() => _ineligible.Atomic.ShouldEqual(_ineligible.Compatibility);
    [Fact] void should_observe_different_source_owners() => (_single.Atomic == _ambiguous.Atomic).ShouldBeFalse();
    [Fact] void should_retain_the_self_owner_after_filtering() => _ineligible.Atomic.ShouldEqual(_ambiguous.Atomic);
    [Fact] void should_verify_all_native_placements() => Vectors().All(_ => _.Success).ShouldBeTrue();
    [Fact] void should_be_independent_of_contribution_order() => Vectors().All(_ => _.Reverse == _.Atomic).ShouldBeTrue();

    PlacementVector[] Vectors() => [_single, _ambiguous, _strongSelf, _ineligible];

    PlacementVector Observe(bool secondProducer, bool strongSelf, bool ineligible)
    {
        var eventStrength = strongSelf ? EvidenceStrength.Exact : EvidenceStrength.Conventional;
        var producerStrength = strongSelf ? EvidenceStrength.Conventional : EvidenceStrength.Exact;
        var eventEvidence = _evidence with { Strength = eventStrength };
        var commandEvidence = _evidence with { Adapter = WolverineAdapter, Strength = producerStrength };
        var intents = new List<CritterStackPlacementIntent> { Intent(_event, eventEvidence) };
        var eventFacts = Declarations(_event, eventEvidence);
        eventFacts.Add(Candidate(_event, _event, ArtifactSourcePlacementReason.Self, eventEvidence));
        var commandFacts = Declarations(_first, commandEvidence);
        commandFacts.Add(Candidate(_first, _first, ArtifactSourcePlacementReason.Self, commandEvidence));
        var producer = Candidate(_event, _first, ArtifactSourcePlacementReason.ProducingCommand, commandEvidence);
        commandFacts.Add(producer);
        var production = Production(_first, commandEvidence);
        commandFacts.Add(production);
        if (!ineligible)
        {
            intents.Add(Intent(_event, commandEvidence, _first.Subject));
        }
        if (secondProducer)
        {
            commandFacts.AddRange(Declarations(_second, commandEvidence));
            commandFacts.Add(Candidate(_second, _second, ArtifactSourcePlacementReason.Self, commandEvidence));
            commandFacts.Add(Candidate(_event, _second, ArtifactSourcePlacementReason.ProducingCommand, commandEvidence));
            commandFacts.Add(Production(_second, commandEvidence));
            intents.Add(Intent(_event, commandEvidence, _second.Subject));
        }
        var classifier = Classification(ineligible, commandFacts, producer, production);
        var diagnostics = new List<GenerationDiagnostic>();
        var compatibility = CritterStackSourcePlacement.Derive(_context, new() { NamespaceSegmentsToSkip = 1 }, intents, diagnostics);
        diagnostics.Select(_ => $"{_.Code}: {_.Message}").ShouldBeEmpty();
        var contributions = new[] { Contribution(MartenAdapter, eventFacts), Contribution(WolverineAdapter, commandFacts), classifier };
        var forward = GeneratePlacements(contributions);
        var reverse = GeneratePlacements(contributions.Reverse());
        return new(Meaning(compatibility.OfType<ArtifactPlacementFact>().Single(_ => _.Artifact == _event).Placement), Meaning(Placement(forward)), Meaning(Placement(reverse)), forward.IsSuccess && reverse.IsSuccess);
    }

    AdapterContribution Classification(bool ineligible, List<GenerationFact> facts, params GenerationFact[] candidates)
    {
        if (!ineligible)
        {
            return Contribution(WolverineAdapter, []);
        }
        var method = Type(_project, "FactParity.Sales.Orders.First.PlaceOrder").GetMembers("OnException").OfType<IMethodSymbol>().Single();
        facts.AddRange(candidates.Select(candidate => CritterStackMethodFacts.Support(candidate, [CritterStackMethodFacts.Observe(_project, method)])));
        return Failure(_project, method);
    }

    GeneratedScreenplayDefinition GeneratePlacements(IEnumerable<AdapterContribution> contributions) =>
        new ScreenplayDefinitionGenerator().Generate(contributions, new() { Domain = "FactParity", SourcePlacementResolver = _resolver });

    ArtifactPlacement Placement(GeneratedScreenplayDefinition result) => result.Graph.Placements.Single(_ => _.Artifact == _event).EffectiveVariants.Single().Placement;

    List<GenerationFact> Declarations(ArtifactKey key, Evidence evidence)
    {
        var name = key == _event ? "FactParity.Sales.Orders.Events.OrderPlaced" : CommandName(key);
        return [Declaration(key, Type(_project, name), evidence)];
    }

    string CommandName(ArtifactKey key) => key == _first ? "FactParity.Sales.Orders.First.PlaceOrder" : "FactParity.Sales.Orders.Second.ImportOrder";

    ArtifactSourcePlacementCandidateFact Candidate(ArtifactKey target, ArtifactKey owner, ArtifactSourcePlacementReason reason, Evidence evidence) => new()
    {
        Id = new() { Value = $"owner-vector:{target.Subject.Value}:{owner.Subject.Value}" }, Subject = target.Subject, Evidence = evidence,
        Definition = new() { Artifact = target, Owner = owner, Reason = reason, SliceKind = GenerationSliceKind.StateChange, Policy = new() }
    };

    RelationshipFact Production(ArtifactKey command, Evidence evidence) => new()
    {
        Id = new() { Value = $"owner-vector:produces:{command.Subject.Value}" }, Subject = command.Subject, Evidence = evidence,
        Definition = new() { Key = new() { Source = command.Subject, Kind = RelationshipKind.Produces, Target = _event.Subject } }
    };

    static CritterStackPlacementIntent Intent(ArtifactKey key, Evidence evidence, SubjectId? owner = null) => new(
        $"owner-vector:intent:{key.Subject.Value}",
        key,
        owner,
        new() { Module = "FactParity", Features = ["Fallback"], Slice = "Fallback", SliceKind = GenerationSliceKind.StateChange },
        evidence);

    static string Meaning(ArtifactPlacement placement) => $"{placement.Module}/{string.Join('/', placement.Features)}/{placement.Slice}:{placement.SliceKind}";

    sealed record PlacementVector(string Compatibility, string Atomic, string Reverse, bool Success);

    const string Source = """
        namespace FactParity.Sales.Orders.Events { public sealed class OrderPlaced; }
        namespace FactParity.Sales.Orders.First
        {
            public sealed class PlaceOrder { public static void OnException() { } }
        }
        namespace FactParity.Sales.Orders.Second { public sealed class ImportOrder; }
        """;
}
