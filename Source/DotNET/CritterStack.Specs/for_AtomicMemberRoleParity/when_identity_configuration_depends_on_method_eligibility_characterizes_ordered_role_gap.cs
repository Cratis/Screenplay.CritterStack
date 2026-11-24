// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Cratis.CritterStack.Screenplay.for_AtomicMemberRoleParity;

/// <summary>
/// Characterizes the ordered identity-role gap recorded in .ai-work/44/decision-identity-role.md:
/// SG 0.34.0 unions eligible roles, while compatibility selects the last eligible configuration.
/// </summary>
public class when_identity_configuration_depends_on_method_eligibility_characterizes_ordered_role_gap : given.a_method_supported_document_identity_application
{
    readonly AdapterIdentity _adapter = new() { Id = "marten-role-vector", Version = "1.0.0" };
    GeneratedScreenplayDefinition _standalone = null!;
    GeneratedScreenplayDefinition _classified = null!;
    GeneratedScreenplayDefinition _reversed = null!;
    AdapterContribution _members = null!;
    ArtifactKey _key = null!;

    void Because()
    {
        _key = new() { Subject = Project.SubjectForType(Document), Kind = ArtifactKind.Document };
        _members = ObserveMembers();
        var classification = ClassifyFailure();
        var generator = new ScreenplayDefinitionGenerator();
        var options = new ScreenplayGenerationOptions { Domain = "IdentityParity" };
        _standalone = generator.Generate([_members], options);
        _classified = generator.Generate([_members, classification], options);
        _reversed = generator.Generate([classification, _members], options);
    }

    [Fact] void should_keep_the_last_configuration_without_a_peer_classifier() => CompatibilityIdentifiers(CompatibilityMarten).ShouldEqual("last");
    [Fact] void should_fall_back_to_the_previous_configuration_when_the_last_method_is_failure_only() => CompatibilityIdentifiers(CompatibilityCombined).ShouldEqual("first");
    [Fact] void should_retain_the_compatibility_failure_warning() => CompatibilityCombined.Diagnostics.Count(_ => _.Code == WolverineDiagnosticCodes.FailureStageConditionOmitted).ShouldEqual(1);
    [Fact] void should_observe_both_configured_member_roles() => _members.Facts.OfType<ArtifactMemberRoleFact>().Count().ShouldEqual(2);
    [Fact] void should_support_each_role_by_its_exact_observing_method() => _members.Facts.OfType<MethodFactSupportFact>().Count().ShouldEqual(2);
    [Fact] void should_verify_the_standalone_native_graph() => _standalone.IsSuccess.ShouldBeTrue();
    [Fact] void should_verify_the_classified_native_graph() => _classified.IsSuccess.ShouldBeTrue();
    [Fact] void should_characterize_the_atomic_union_of_eligible_configurations() => NativeIdentifiers(_standalone).ShouldEqual("first,last");
    [Fact] void should_preserve_the_previous_configuration_in_the_classified_native_graph() => NativeIdentifiers(_classified).ShouldEqual(CompatibilityIdentifiers(CompatibilityCombined));
    [Fact] void should_not_depend_on_contribution_order() => NativeIdentifiers(_reversed).ShouldEqual(NativeIdentifiers(_classified));

    AdapterContribution ObserveMembers()
    {
        var evidence = CritterStackSource.EvidenceFor(Document, _adapter, Project, EvidenceStrength.Exact, "Authored document declaration");
        var facts = new List<GenerationFact>
        {
            new ArtifactDeclarationFact
            {
                Id = new() { Value = "marten-role-vector:document" }, Subject = _key.Subject, Evidence = evidence,
                Definition = new() { Artifact = _key, Name = Document.Name, File = evidence.Source?.Path }
            }
        };
        facts.AddRange(DotNetTypeUseFacts.Emit(Document, _key, Context, evidence));
        facts.AddRange(ObserveIdentity("First", Configure));
        facts.AddRange(ObserveIdentity("Last", Failure));
        return new() { Adapter = _adapter, Facts = facts };
    }

    IReadOnlyList<GenerationFact> ObserveIdentity(string memberName, IMethodSymbol method)
    {
        var member = Document.GetMembers(memberName).OfType<IPropertySymbol>().Single();
        var evidence = CritterStackSource.EvidenceFor(method, _adapter, Project, EvidenceStrength.Exact, $"Explicit identity configuration for '{member.Name}'");
        var role = new ArtifactMemberRoleFact
        {
            Id = new() { Value = $"marten-role-vector:configured-identity:{member.Name}" },
            Subject = _key.Subject,
            Evidence = evidence,
            Definition = new()
            {
                Member = new() { Artifact = _key, Name = WolverineSagaFacts.LowerFirst(member.Name) },
                Role = ArtifactMemberRoleKind.EventSourceIdentifier
            }
        };
        return [role, CritterStackMethodFacts.Support(role, [CritterStackMethodFacts.Observe(Project, method)])];
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
