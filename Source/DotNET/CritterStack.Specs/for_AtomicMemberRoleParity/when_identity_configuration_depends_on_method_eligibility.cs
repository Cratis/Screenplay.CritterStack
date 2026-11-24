// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Cratis.CritterStack.Screenplay.for_AtomicMemberRoleParity;

/// <summary>
/// Proves that ordered identity alternatives select the last eligible configuration through the shared contract.
/// </summary>
public class when_identity_configuration_depends_on_method_eligibility : given.a_method_supported_document_identity_application
{
    readonly AdapterIdentity _adapter = new() { Id = "marten-role-vector", Version = "1.0.0" };
    GeneratedScreenplayDefinition _standalone = null!;
    GeneratedScreenplayDefinition _classified = null!;
    GeneratedScreenplayDefinition _reversed = null!;
    GeneratedScreenplayDefinition _reversedFacts = null!;
    AdapterContribution _members = null!;

    void Because()
    {
        _members = ObserveMembers();
        var classification = ClassifyFailure();
        var generator = new ScreenplayDefinitionGenerator();
        var options = new ScreenplayGenerationOptions { Domain = "IdentityParity" };
        _standalone = generator.Generate([_members], options);
        _classified = generator.Generate([_members, classification], options);
        _reversed = generator.Generate([classification, _members], options);
        _reversedFacts = generator.Generate([_members with { Facts = [.. _members.Facts.Reverse()] }, classification], options);
    }

    [Fact] void should_keep_the_last_configuration_without_a_peer_classifier() => CompatibilityIdentifiers(CompatibilityMarten).ShouldEqual("last");
    [Fact] void should_fall_back_to_the_previous_configuration_when_the_last_method_is_failure_only() => CompatibilityIdentifiers(CompatibilityCombined).ShouldEqual("first");
    [Fact] void should_retain_the_compatibility_failure_warning() => CompatibilityCombined.Diagnostics.Count(_ => _.Code == WolverineDiagnosticCodes.FailureStageConditionOmitted).ShouldEqual(1);
    [Fact] void should_observe_both_configured_role_alternatives() => Configurations().Length.ShouldEqual(2);
    [Fact] void should_not_assert_roles_before_selection() => _members.Facts.OfType<ArtifactMemberRoleFact>().ShouldBeEmpty();
    [Fact] void should_support_each_role_by_its_exact_observing_method() => _members.Facts.OfType<MethodFactSupportFact>().Count(fact => Configurations().Any(configuration => configuration.Id == fact.Definition.Candidate)).ShouldEqual(2);
    [Fact] void should_verify_the_standalone_native_graph() => _standalone.IsSuccess.ShouldBeTrue();
    [Fact] void should_verify_the_classified_native_graph() => _classified.IsSuccess.ShouldBeTrue();
    [Fact] void should_select_the_last_configuration_without_a_peer_classifier() => NativeIdentifiers(_standalone).ShouldEqual(CompatibilityIdentifiers(CompatibilityMarten));
    [Fact] void should_preserve_the_previous_configuration_in_the_classified_native_graph() => NativeIdentifiers(_classified).ShouldEqual(CompatibilityIdentifiers(CompatibilityCombined));
    [Fact] void should_not_depend_on_contribution_order() => NativeIdentifiers(_reversed).ShouldEqual(NativeIdentifiers(_classified));
    [Fact] void should_not_depend_on_fact_order() => NativeIdentifiers(_reversedFacts).ShouldEqual(NativeIdentifiers(_classified));

    ArtifactMemberRoleSelectionFact[] Configurations() => [.. _members.Facts.OfType<ArtifactMemberRoleSelectionFact>().Where(fact => fact.Definition.Priority == 1)];

    AdapterContribution ObserveMembers()
    {
        var subjects = new CritterStackSubjectResolver(Context);
        var (_, ownership) = MartenStoreScopeDiscovery.DiscoverOwnership(Context, _adapter, subjects);
        var observations = new MartenDocumentObservations(Project, _adapter, ownership).Discover();
        var candidates = new MartenDocumentCandidates(Project, Context, subjects).Discover(observations);
        return new() { Adapter = _adapter, Facts = candidates.Facts, Diagnostics = candidates.Diagnostics };
    }

    AdapterContribution ClassifyFailure()
    {
        var adapter = new WolverineScreenplayAdapter().Identity;
        var subject = CritterStackMethodFacts.SubjectFor(Project, Failure);
        var evidence = CritterStackSource.EvidenceFor(Failure, adapter, Project, EvidenceStrength.Exact, "Wolverine failure-only method");
        var warning = new GenerationDiagnostic
        {
            Code = WolverineDiagnosticCodes.FailureStageConditionOmitted,
            Severity = GenerationDiagnosticSeverity.Warning,
            Outcome = GenerationDiagnosticOutcome.Unsupported,
            Message = "Failure-only identity configuration is omitted",
            Subject = subject,
            Source = evidence.Source
        };
        return new()
        {
            Adapter = adapter,
            Facts = [CritterStackMethodFacts.FailureOnly(CritterStackMethodFacts.Observe(Project, Failure), evidence, warning)]
        };
    }
}
