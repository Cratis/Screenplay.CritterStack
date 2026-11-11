// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Cratis.CritterStack.Screenplay.for_CritterStackScreenplayAdapter;

public class when_characterizing_wolverine_validator_option_resolution : given.a_wolverine_validator_option_scenarios
{
    const string ConfigurationSubject = "dotnet://Scenario/Scenario/Scenario.Configuration";
    const string HandlerSubject = "dotnet://Scenario/Scenario/Scenario.Handler";
    const string Tail = "but current generation contracts cannot represent validation without overloading an unrelated relationship";

    const string PublicValidation =
        $"WOLVERINE0005|Warning|Unsupported|{HandlerSubject}|Scenario/Program.cs:8:14-8:36|FluentValidation message handler validation for 'PublicRequest' is applied by validator 'Scenario.PublicRequestValidator' after 'UseFluentValidation' enabled the policy, {Tail}";

    const string InternalValidation =
        $"WOLVERINE0005|Warning|Unsupported|{HandlerSubject}|Scenario/Program.cs:9:16-9:40|FluentValidation message handler validation for 'InternalRequest' is applied by validator 'Scenario.InternalRequestValidator' after 'UseFluentValidation' enabled the policy, {Tail}";

    [Fact] void should_validate_only_public_validators_by_default() => Outcomes["default"].ShouldEqual([Activation(38), PublicValidation]);

    [Fact] void should_validate_internal_validators_when_the_positional_argument_includes_them() => Outcomes["positional-include-internal"].ShouldEqual([Activation(94), PublicValidation, InternalValidation]);

    [Fact] void should_validate_only_public_validators_when_the_positional_argument_excludes_internal_ones() => Outcomes["positional-exclude-internal"].ShouldEqual([Activation(95), PublicValidation]);

    [Fact] void should_not_validate_anything_when_the_positional_argument_requires_explicit_registration() => Outcomes["positional-explicit-registration"].ShouldEqual([Unresolved(85), Activation(85)]);

    [Fact] void should_validate_internal_validators_when_the_callback_includes_them() => Outcomes["callback-include-internal"].ShouldEqual([Activation(77), PublicValidation, InternalValidation]);

    [Fact] void should_let_the_later_callback_assignment_exclude_internal_validators() => Outcomes["callback-include-internal-then-exclude"].ShouldEqual([Activation(109), PublicValidation]);

    [Fact] void should_let_the_later_callback_assignment_include_internal_validators() => Outcomes["callback-exclude-internal-then-include"].ShouldEqual([Activation(109), PublicValidation, InternalValidation]);

    [Fact] void should_not_validate_anything_when_the_callback_requires_explicit_registration() => Outcomes["callback-explicit-registration"].ShouldEqual([Unresolved(109), Activation(109)]);

    [Fact] void should_let_the_later_callback_assignment_restore_validator_discovery() => Outcomes["callback-explicit-then-discover"].ShouldEqual([Activation(222), PublicValidation, InternalValidation]);

    [Fact] void should_let_the_later_callback_assignment_require_explicit_registration() => Outcomes["callback-discover-then-explicit"].ShouldEqual([Unresolved(222), Activation(222)]);

    static string Activation(int endColumn) =>
        $"WOLVERINE0008|Information|Unsupported|{ConfigurationSubject}|Scenario/Program.cs:21:9-21:{endColumn}|Wolverine FluentValidation message handler policy is enabled by 'UseFluentValidation', but current generation contracts cannot represent policy activation";

    static string Unresolved(int endColumn) =>
        $"WOLVERINE0009|Warning|Unknown|{ConfigurationSubject}|Scenario/Program.cs:21:9-21:{endColumn}|Wolverine validation call 'UseFluentValidation' was not applied because validator applicability depends on explicit runtime container registrations";
}
