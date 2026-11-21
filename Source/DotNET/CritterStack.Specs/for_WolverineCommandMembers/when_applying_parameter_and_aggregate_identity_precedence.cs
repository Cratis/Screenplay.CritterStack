// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Cratis.CritterStack.Screenplay.for_WolverineCommandMembers;

public class when_applying_parameter_and_aggregate_identity_precedence : given.a_wolverine_command_members_application
{
    string? _attributed;
    string? _aggregate;
    string? _conventional;
    string? _none;
    string? _loaded;
    string? _direct;
    string? _two;

    void Because()
    {
        var loaded = Binding(WolverineStateBindingKind.LoadedEventStream, Member("Plain", "Title"), Type("Plain"));
        var direct = Binding(WolverineStateBindingKind.DirectEventStream, Member("Plain", "Title"), Type("Plain"));
        _attributed = Select(Input("Attributed", "Aggregate"));
        _aggregate = Select(Input("Conventional", "Aggregate"));
        _conventional = Select(Input("Conventional"));
        _none = Select(Input("Plain"));
        _loaded = Select(Input("Conventional", "Aggregate", [loaded]));
        _direct = Select(Input("Conventional", "Aggregate", [direct]));
        _two = Select(Input("Conventional", "Aggregate", [loaded, loaded]));
    }

    [Fact] void should_prefer_the_attributed_identity_without_bindings() => _attributed.ShouldEqual("key");
    [Fact] void should_use_the_aggregate_named_identity_without_bindings() => _aggregate.ShouldEqual("aggregateId");
    [Fact] void should_use_the_conventional_identity_without_an_aggregate() => _conventional.ShouldEqual("id");
    [Fact] void should_select_nothing_when_no_member_matches() => _none.ShouldBeNull();
    [Fact] void should_use_the_identity_of_a_single_loading_binding() => _loaded.ShouldEqual("title");
    [Fact] void should_select_nothing_for_a_single_non_loading_binding() => _direct.ShouldBeNull();
    [Fact] void should_select_nothing_for_several_bindings() => _two.ShouldBeNull();

    static string? Select(WolverineCommandIdentityInput input) => WolverineCommandMembers.SelectIdentifier(input).Name;
}
