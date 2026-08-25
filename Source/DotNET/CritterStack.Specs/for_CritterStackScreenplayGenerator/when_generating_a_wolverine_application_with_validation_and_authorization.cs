// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Cratis.CritterStack.Screenplay.for_CritterStackScreenplayGenerator;

public class when_generating_a_wolverine_application_with_validation_and_authorization : given.a_wolverine_validation_authorization_application
{
    GeneratedScreenplayDefinition _result = null!;

    void Because() => _result = new CritterStackScreenplayGenerator().Generate(
        [PositiveProject],
        new CritterStackScreenplayOptions { Domain = "ValidationAuthorization" });

    [Fact] void should_compile_the_fixture() => PositiveProject.Compilation.GetDiagnostics().Where(_ => _.Severity == Microsoft.CodeAnalysis.DiagnosticSeverity.Error).ShouldBeEmpty();
    [Fact] void should_succeed() => _result.IsSuccess.ShouldBeTrue();
    [Fact] void should_classify_each_recognized_policy_gap_as_unsupported() => _result.Diagnostics.All(_ => _.Outcome == GenerationDiagnosticOutcome.Unsupported).ShouldBeTrue();
    [Fact] void should_record_each_exact_validation_policy_activation() => _result.Diagnostics.Count(_ => _.Code == WolverineDiagnosticCodes.ValidationPolicyOmitted).ShouldEqual(4);
    [Fact] void should_record_fluent_validation_application() => _result.Diagnostics.Any(_ => _.Code == WolverineDiagnosticCodes.ValidationOmitted && _.Message.Contains("FluentValidation HTTP endpoint validation for 'CreateOrder'", StringComparison.Ordinal)).ShouldBeTrue();
    [Fact] void should_record_data_annotations_application() => _result.Diagnostics.Any(_ => _.Code == WolverineDiagnosticCodes.ValidationOmitted && _.Message.Contains("DataAnnotations HTTP endpoint validation for 'RegisterUser'", StringComparison.Ordinal)).ShouldBeTrue();
    [Fact] void should_record_fluent_validation_message_handler_application() => _result.Diagnostics.Any(_ => _.Code == WolverineDiagnosticCodes.ValidationOmitted && _.Message.Contains("FluentValidation message handler validation for 'ProcessPayment'", StringComparison.Ordinal)).ShouldBeTrue();
    [Fact] void should_record_data_annotations_message_handler_application() => _result.Diagnostics.Any(_ => _.Code == WolverineDiagnosticCodes.ValidationOmitted && _.Message.Contains("DataAnnotations message handler validation for 'ImportUser'", StringComparison.Ordinal)).ShouldBeTrue();
    [Fact] void should_attach_automation_validation_to_the_reaction() => _result.Diagnostics.Any(_ => _.Code == WolverineDiagnosticCodes.ValidationOmitted && _.Message.Contains("ValidateAutomationTrigger", StringComparison.Ordinal) && _.Subject == ValidateAutomationReactionSubject).ShouldBeTrue();
    [Fact] void should_not_attach_automation_validation_to_the_trigger_message() => _result.Diagnostics.Any(_ => _.Code == WolverineDiagnosticCodes.ValidationOmitted && _.Message.Contains("ValidateAutomationTrigger", StringComparison.Ordinal) && _.Subject.Value.EndsWith("/ValidationAuthorization.ValidateAutomationTrigger", StringComparison.Ordinal)).ShouldBeFalse();
    [Fact] void should_keep_validate_as_compound_middleware() => CompoundValidationDiagnostics.Count(_ => _.Message.Contains("'Validate'", StringComparison.Ordinal)).ShouldEqual(1);
    [Fact] void should_keep_validate_async_as_compound_middleware() => CompoundValidationDiagnostics.Count(_ => _.Message.Contains("'ValidateAsync'", StringComparison.Ordinal)).ShouldEqual(1);
    [Fact] void should_attach_compound_validation_to_one_handler_subject() => CompoundValidationDiagnostics.Select(_ => _.Subject).Distinct().Count().ShouldEqual(1);
    [Fact] void should_not_create_separate_artifacts_for_compound_validation() => _result.Graph.Artifacts.Count(_ => _.Key.Kind == ArtifactKind.Command && _.Key.Subject.Value.EndsWith("/ValidationAuthorization.CloseOrder", StringComparison.Ordinal)).ShouldEqual(1);
    [Fact] void should_record_global_and_configured_authorization_activation() => _result.Diagnostics.Count(_ => _.Code == WolverineDiagnosticCodes.AuthorizationOmitted && _.Source?.Path == "ValidationAuthorization/Program.cs").ShouldEqual(2);
    [Fact] void should_record_the_authorize_policy_and_role() => _result.Diagnostics.Any(_ => _.Code == WolverineDiagnosticCodes.AuthorizationOmitted && _.Message.Contains("policy 'orders'", StringComparison.Ordinal) && _.Message.Contains("roles 'operator'", StringComparison.Ordinal)).ShouldBeTrue();
    [Fact] void should_record_allow_anonymous_as_an_override() => _result.Diagnostics.Any(_ => _.Code == WolverineDiagnosticCodes.AuthorizationOmitted && _.Message.Contains("explicitly allows anonymous access", StringComparison.Ordinal)).ShouldBeTrue();
    [Fact] void should_retain_activation_source_evidence() => _result.Diagnostics.Where(_ => _.Code == WolverineDiagnosticCodes.ValidationPolicyOmitted).All(_ => _.Source?.Path == "ValidationAuthorization/Program.cs").ShouldBeTrue();
    [Fact] void should_retain_applied_behavior_source_evidence() => _result.Diagnostics.Where(_ => _.Code == WolverineDiagnosticCodes.ValidationOmitted || (_.Code == WolverineDiagnosticCodes.AuthorizationOmitted && _.Source?.Path != "ValidationAuthorization/Program.cs")).All(_ => _.Source?.Path == "ValidationAuthorization/Endpoints.cs").ShouldBeTrue();
    [Fact] void should_not_overload_unrelated_relationships()
    {
        _result.Graph.Relationships.Count.ShouldEqual(4);
        _result.Graph.Relationships.Count(_ => _.Key.Kind == RelationshipKind.Produces).ShouldEqual(2);
        _result.Graph.Relationships.Count(_ => _.Key.Kind == RelationshipKind.Handles).ShouldEqual(1);
        _result.Graph.Relationships.Count(_ => _.Key.Kind == RelationshipKind.Publishes).ShouldEqual(1);
    }

    SubjectId ValidateAutomationReactionSubject => _result.Graph.Artifacts.Single(_ =>
        _.Key.Kind == ArtifactKind.Reaction &&
        _.Variants[0].Definition.Name == "ValidateAutomation").Key.Subject;

    IEnumerable<GenerationDiagnostic> CompoundValidationDiagnostics => _result.Diagnostics.Where(_ =>
        _.Code == WolverineDiagnosticCodes.ValidationOmitted &&
        _.Message.StartsWith("Compound middleware", StringComparison.Ordinal));
}
