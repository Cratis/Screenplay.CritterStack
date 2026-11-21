// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Cratis.CritterStack.Screenplay.for_WolverineCommandMembers;

public class when_fetched_streams_do_not_identify_a_command_member : given.a_wolverine_command_members_application
{
    string? _fetchedWithoutAggregate;
    string? _fetchedWithAggregate;
    string? _fetchedBinding;

    void Because()
    {
        _fetchedWithoutAggregate = Select(Input("Conventional", hasFetchedStreams: true));
        _fetchedWithAggregate = Select(Input("Conventional", "Aggregate", hasFetchedStreams: true));
        _fetchedBinding = Select(Input(
            "Conventional",
            bindings: [Binding(WolverineStateBindingKind.FetchedEventStream, Member("Conventional", "Title"), Type("Plain"))],
            hasFetchedStreams: true));
    }

    [Fact] void should_select_nothing_for_fetched_streams_without_an_aggregate() => _fetchedWithoutAggregate.ShouldBeNull();
    [Fact] void should_keep_the_aggregate_identity_when_an_aggregate_exists() => _fetchedWithAggregate.ShouldEqual("aggregateId");
    [Fact] void should_use_the_identity_of_a_single_fetched_binding() => _fetchedBinding.ShouldEqual("title");

    static string? Select(WolverineCommandIdentityInput input) => WolverineCommandMembers.SelectIdentifier(input).Name;
}
