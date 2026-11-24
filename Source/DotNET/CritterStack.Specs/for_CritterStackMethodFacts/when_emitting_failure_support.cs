// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Cratis.CritterStack.Screenplay.for_CritterStackMethodFacts;

public class when_emitting_failure_support : given.a_method_fact_application
{
    readonly BackendScopeKey _scope = new() { Store = new() { Value = "dotnet://Methods/store" }, EventModelName = "Orders" };
    ArtifactDeclarationFact _candidate = null!;
    GenerationDiagnostic _reason = null!;
    GenerationDiagnostic _conditional = null!;
    MethodFactSupportFact _support = null!;
    MethodExecutionClassificationFact _classification = null!;
    MethodExecutionClassificationFact _otherScope = null!;
    MethodExecutionClassificationFact _otherProducer = null!;

    void Because()
    {
        var evidence = CritterStackSource.EvidenceFor(IntegerHandler, new() { Id = "wolverine", Version = "1.0.0" }, Project, EvidenceStrength.Exact);
        var subject = Project.SubjectForType(IntegerHandler.ContainingType);
        _candidate = new()
        {
            Id = new() { Value = "wolverine:artifact:message" },
            Subject = subject,
            Evidence = evidence,
            Definition = new() { Artifact = new() { Subject = subject, Kind = ArtifactKind.Message, Scope = _scope }, Name = "Message" }
        };
        _reason = new()
        {
            Code = "failure-omitted",
            Message = "The original located failure warning",
            Severity = GenerationDiagnosticSeverity.Warning,
            Outcome = GenerationDiagnosticOutcome.Unsupported,
            Subject = subject,
            Source = evidence.Source
        };
        _conditional = _reason with { Code = "conditional", Message = "A diagnostic conditional on this observation" };
        _support = CritterStackMethodFacts.Support(_candidate,
        [
            CritterStackMethodFacts.Observe(Project, Locals[0], _scope, [_conditional]),
            CritterStackMethodFacts.Observe(Project, StringHandler, _scope)
        ]);
        var observation = CritterStackMethodFacts.Observe(Project, IntegerHandler, _scope);
        _classification = CritterStackMethodFacts.FailureOnly(observation, evidence, _reason);
        _otherScope = CritterStackMethodFacts.FailureOnly(observation with { Scope = _scope with { EventModelName = "Other" } }, evidence, _reason);
        _otherProducer = CritterStackMethodFacts.FailureOnly(observation, evidence with { Adapter = new() { Id = "other", Version = "1.0.0" } }, _reason);
    }

    [Fact] void should_support_the_exact_candidate() => _support.Definition.Candidate.ShouldEqual(_candidate.Id);
    [Fact] void should_keep_the_candidates_subject() => _support.Subject.ShouldEqual(_candidate.Subject);
    [Fact] void should_keep_the_candidates_producer_and_evidence() => _support.Evidence.ShouldEqual(_candidate.Evidence);
    [Fact] void should_keep_all_independent_observations() => _support.Definition.Alternatives.Count.ShouldEqual(2);
    [Fact] void should_keep_the_conditional_diagnostic_on_its_observation() => _support.Definition.Alternatives[0].Diagnostics.ShouldContainOnly(_conditional);
    [Fact] void should_not_attach_it_to_an_independent_observation() => _support.Definition.Alternatives[1].Diagnostics.Count.ShouldEqual(0);
    [Fact] void should_keep_exact_backend_occurrences() => _support.Definition.Alternatives.All(alternative => alternative.Scope == _scope).ShouldBeTrue();
    [Fact] void should_classify_the_exact_handler() => _classification.Definition.Method.ShouldEqual(CritterStackMethodFacts.SubjectFor(Project, IntegerHandler));
    [Fact] void should_keep_the_classified_subject() => _classification.Subject.ShouldEqual(_classification.Definition.Method);
    [Fact] void should_keep_the_failure_only_kind() => _classification.Definition.Kind.ShouldEqual(MethodExecutionKind.FailureOnly);
    [Fact] void should_preserve_the_original_warning_unchanged() => _classification.Definition.OmissionReason.ShouldEqual(_reason);
    [Fact] void should_preserve_the_original_evidence() => _classification.Evidence.ShouldEqual(_candidate.Evidence);
    [Fact] void should_keep_the_classification_scope() => _classification.Definition.Scope.ShouldEqual(_scope);
    [Fact] void should_separate_classifications_for_other_occurrences() => (_classification.Id == _otherScope.Id).ShouldBeFalse();
    [Fact] void should_separate_classifications_from_other_producers() => (_classification.Id == _otherProducer.Id).ShouldBeFalse();
    [Fact] void should_qualify_the_support_identity_with_the_candidate() => _support.Id.Value.ShouldEqual($"{_candidate.Id.Value}:method-support");
}
