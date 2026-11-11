// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Cratis.CritterStack.Screenplay.Canonical;

namespace Cratis.CritterStack.Screenplay.for_Expectations;

public class when_checking_grouping_and_fan_out_expectations : given.a_canonical_multi_stream_result
{
    void Because() => Evaluate(
        (ExpectationKind.Grouping, "CustomerOrdersProjection|CustomerAssigned|customerId|false"),
        (ExpectationKind.Grouping, "CustomerOrdersProjection|CustomersShared|customerIds|true"),
        (ExpectationKind.Grouping, "CustomerOrdersProjection|CustomerAssigned|customerId|true"),
        (ExpectationKind.Grouping, "CustomerOrdersProjection|CustomerAssigned|customerIds|false"),
        (ExpectationKind.Grouping, "UnsafeSelectorsProjection|CustomerAssigned|customerId|false"),
        (ExpectationKind.Grouping, "CustomerOrdersProjection|LineImported|customerId|false"),
        (ExpectationKind.Grouping, "CustomerOrdersProjection|CustomerAssigned|customerId|maybe"),
        (ExpectationKind.Grouping, "CustomerOrdersProjection|CustomerAssigned|customerId"),
        (ExpectationKind.FanOut, "OrderImported|LineImported|lines|after-grouping"),
        (ExpectationKind.FanOut, "RouteImported|StopImported|stops|before-grouping"),
        (ExpectationKind.FanOut, "RouteImported|StopImported|stops|after-grouping"),
        (ExpectationKind.FanOut, "OrderImported|LineImported|items|after-grouping"),
        (ExpectationKind.FanOut, "OrderImported|StopImported|lines|after-grouping"),
        (ExpectationKind.FanOut, "Missing|LineImported|lines|after-grouping"),
        (ExpectationKind.FanOut, "OrderImported|LineImported|lines"),
        (ExpectationKind.DiagnosticMessage, "MARTEN0001|Multi-stream projection 'TenantGroupingProjection' groups by tenant through RollUpByTenant"));

    [Fact] void should_meet_a_single_identity_grouping() => Met(ExpectationKind.Grouping, "CustomerOrdersProjection|CustomerAssigned|customerId|false").ShouldBeTrue();
    [Fact] void should_meet_a_one_to_many_identity_grouping() => Met(ExpectationKind.Grouping, "CustomerOrdersProjection|CustomersShared|customerIds|true").ShouldBeTrue();
    [Fact] void should_not_meet_a_grouping_with_the_wrong_cardinality() => Met(ExpectationKind.Grouping, "CustomerOrdersProjection|CustomerAssigned|customerId|true").ShouldBeFalse();
    [Fact] void should_not_meet_a_grouping_with_the_wrong_member() => Met(ExpectationKind.Grouping, "CustomerOrdersProjection|CustomerAssigned|customerIds|false").ShouldBeFalse();
    [Fact] void should_not_meet_a_grouping_for_another_reducer() => Met(ExpectationKind.Grouping, "UnsafeSelectorsProjection|CustomerAssigned|customerId|false").ShouldBeFalse();
    [Fact] void should_not_meet_a_grouping_for_an_event_without_an_identity() => Met(ExpectationKind.Grouping, "CustomerOrdersProjection|LineImported|customerId|false").ShouldBeFalse();
    [Fact] void should_not_meet_a_grouping_whose_cardinality_is_not_a_boolean() => Met(ExpectationKind.Grouping, "CustomerOrdersProjection|CustomerAssigned|customerId|maybe").ShouldBeFalse();
    [Fact] void should_not_meet_a_grouping_without_a_cardinality() => Met(ExpectationKind.Grouping, "CustomerOrdersProjection|CustomerAssigned|customerId").ShouldBeFalse();
    [Fact] void should_meet_a_fan_out_after_grouping() => Met(ExpectationKind.FanOut, "OrderImported|LineImported|lines|after-grouping").ShouldBeTrue();
    [Fact] void should_meet_a_fan_out_before_grouping() => Met(ExpectationKind.FanOut, "RouteImported|StopImported|stops|before-grouping").ShouldBeTrue();
    [Fact] void should_not_meet_a_fan_out_with_the_wrong_mode() => Met(ExpectationKind.FanOut, "RouteImported|StopImported|stops|after-grouping").ShouldBeFalse();
    [Fact] void should_not_meet_a_fan_out_with_the_wrong_member() => Met(ExpectationKind.FanOut, "OrderImported|LineImported|items|after-grouping").ShouldBeFalse();
    [Fact] void should_not_meet_a_fan_out_with_the_wrong_child() => Met(ExpectationKind.FanOut, "OrderImported|StopImported|lines|after-grouping").ShouldBeFalse();
    [Fact] void should_not_meet_a_fan_out_from_an_unknown_parent() => Met(ExpectationKind.FanOut, "Missing|LineImported|lines|after-grouping").ShouldBeFalse();
    [Fact] void should_not_meet_a_fan_out_without_a_mode() => Met(ExpectationKind.FanOut, "OrderImported|LineImported|lines").ShouldBeFalse();
    [Fact] void should_meet_a_diagnostic_message_naming_the_reported_projection() => Met(ExpectationKind.DiagnosticMessage, "MARTEN0001|Multi-stream projection 'TenantGroupingProjection' groups by tenant through RollUpByTenant").ShouldBeTrue();
}
