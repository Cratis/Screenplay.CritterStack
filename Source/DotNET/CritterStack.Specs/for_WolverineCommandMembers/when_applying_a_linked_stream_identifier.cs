// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Cratis.CritterStack.Screenplay.for_WolverineCommandMembers;

public class when_applying_a_linked_stream_identifier : given.a_wolverine_command_members_application
{
    WolverineCommandIdentifier _identifier = null!;

    void Because() => _identifier = WolverineCommandMembers.SelectIdentifier(Input(
        "Attributed",
        "Aggregate",
        [Binding(WolverineStateBindingKind.LoadedEventStream, Member("Attributed", "Key"), Type("Plain"))],
        linkedStreamIdentity: Member("Attributed", "Title")));

    [Fact] void should_prefer_the_linked_identity_over_every_other_rule() => _identifier.Member!.Name.ShouldEqual("Title");
    [Fact] void should_expose_the_lower_first_name() => _identifier.Name.ShouldEqual("title");
    [Fact] void should_flag_only_the_linked_member() => Identified(WolverineCommandMembers.Materialize(WolverineCommandMembers.Describe(Type("Attributed")), _identifier)).ShouldEqual("title");
}
