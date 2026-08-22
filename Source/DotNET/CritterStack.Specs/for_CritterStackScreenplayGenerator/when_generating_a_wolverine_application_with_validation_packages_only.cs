// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Cratis.CritterStack.Screenplay.for_CritterStackScreenplayGenerator;

public class when_generating_a_wolverine_application_with_validation_packages_only : given.a_wolverine_validation_authorization_application
{
    GeneratedScreenplayDefinition _result = null!;

    void Because() => _result = new CritterStackScreenplayGenerator().Generate(
        [PackageOnlyProject],
        new CritterStackScreenplayOptions { Domain = "PackageOnly" });

    [Fact] void should_compile_the_fixture() => PackageOnlyProject.Compilation.GetDiagnostics().Where(_ => _.Severity == Microsoft.CodeAnalysis.DiagnosticSeverity.Error).ShouldBeEmpty();
    [Fact] void should_succeed() => _result.IsSuccess.ShouldBeTrue();
    [Fact] void should_not_treat_fluent_validation_types_as_policy_activation() => _result.Diagnostics.Select(_ => _.Code).ShouldNotContain(WolverineDiagnosticCodes.ValidationPolicyOmitted);
    [Fact] void should_not_apply_fluent_validation_without_enablement() => _result.Diagnostics.Select(_ => _.Code).ShouldNotContain(WolverineDiagnosticCodes.ValidationOmitted);
    [Fact] void should_not_treat_data_annotation_attributes_as_policy_activation() => _result.Diagnostics.Select(_ => _.Code).ShouldNotContain(WolverineDiagnosticCodes.ValidationConfigurationUnresolved);
    [Fact] void should_not_invent_authorization_without_an_attribute_or_global_policy() => _result.Diagnostics.Select(_ => _.Code).ShouldNotContain(WolverineDiagnosticCodes.AuthorizationOmitted);
    [Fact] void should_not_invent_authorization_configuration_loss() => _result.Diagnostics.Select(_ => _.Code).ShouldNotContain(WolverineDiagnosticCodes.AuthorizationConfigurationUnresolved);
}
