// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Cratis.CritterStack.Screenplay.for_CritterStackScreenplayGenerator;

public class when_generating_a_wolverine_application_with_unresolved_validation_and_authorization : given.a_wolverine_validation_authorization_application
{
    GeneratedScreenplayDefinition _result = null!;

    void Because() => _result = new CritterStackScreenplayGenerator().Generate(
        [UnresolvedProject],
        new CritterStackScreenplayOptions { Domain = "UnresolvedPolicies" });

    [Fact] void should_compile_the_fixture() => UnresolvedProject.Compilation.GetDiagnostics().Where(_ => _.Severity == Microsoft.CodeAnalysis.DiagnosticSeverity.Error).ShouldBeEmpty();
    [Fact] void should_succeed() => _result.IsSuccess.ShouldBeTrue();
    [Fact] void should_report_each_unresolved_validation_configuration() => _result.Diagnostics.Count(_ => _.Code == WolverineDiagnosticCodes.ValidationConfigurationUnresolved).ShouldEqual(4);
    [Fact] void should_identify_the_runtime_fluent_validation_callback() => _result.Diagnostics.Any(_ => _.Code == WolverineDiagnosticCodes.ValidationConfigurationUnresolved && _.Message.Contains("configuration callback", StringComparison.Ordinal)).ShouldBeTrue();
    [Fact] void should_identify_conditionally_enabled_validation() => _result.Diagnostics.Count(_ => _.Code == WolverineDiagnosticCodes.ValidationConfigurationUnresolved && _.Message.Contains("conditionally executed", StringComparison.Ordinal)).ShouldEqual(3);
    [Fact] void should_report_only_proven_unresolved_authorization_configuration() => _result.Diagnostics.Count(_ => _.Code == WolverineDiagnosticCodes.AuthorizationConfigurationUnresolved).ShouldEqual(1);
    [Fact] void should_identify_conditional_global_authorization() => _result.Diagnostics.Any(_ => _.Code == WolverineDiagnosticCodes.AuthorizationConfigurationUnresolved && _.Message.Contains("conditionally executed", StringComparison.Ordinal)).ShouldBeTrue();
    [Fact] void should_not_conflate_a_runtime_endpoint_policy_with_authorization() => _result.Diagnostics.Any(_ => _.Code == WolverineDiagnosticCodes.AuthorizationConfigurationUnresolved && _.Message.Contains("ConfigureEndpoints", StringComparison.Ordinal)).ShouldBeFalse();
    [Fact] void should_preserve_only_the_conditional_global_authorization_loss() => _result.Diagnostics.Where(_ => _.Code == WolverineDiagnosticCodes.AuthorizationConfigurationUnresolved).Select(_ => _.Message).ShouldContainOnly("Wolverine authorization configuration call 'RequireAuthorizeOnAll' was not applied because the global authorization call is conditionally executed at runtime");
    [Fact] void should_not_conflate_a_custom_http_policy_with_authorization() => _result.Diagnostics.Any(_ => _.Code == WolverineDiagnosticCodes.AuthorizationConfigurationUnresolved && _.Message.Contains("AddPolicy", StringComparison.Ordinal)).ShouldBeFalse();
    [Fact] void should_preserve_only_the_unconditional_validation_activation() => _result.Diagnostics.Count(_ => _.Code == WolverineDiagnosticCodes.ValidationPolicyOmitted).ShouldEqual(1);
    [Fact] void should_not_guess_applied_validation() => _result.Diagnostics.Select(_ => _.Code).ShouldNotContain(WolverineDiagnosticCodes.ValidationOmitted);
    [Fact] void should_not_guess_authorization_activation() => _result.Diagnostics.Select(_ => _.Code).ShouldNotContain(WolverineDiagnosticCodes.AuthorizationOmitted);
    [Fact] void should_retain_the_authored_configuration_file() => _result.Diagnostics.Where(_ => _.Code == WolverineDiagnosticCodes.ValidationConfigurationUnresolved || _.Code == WolverineDiagnosticCodes.AuthorizationConfigurationUnresolved).All(_ => _.Source?.Path == "UnresolvedPolicies/Program.cs").ShouldBeTrue();
}
