// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Cratis.CritterStack.Screenplay.for_WolverineCommandMembers;

public class when_describing_members_without_persistence_roles : given.a_wolverine_command_members_application
{
    WolverineCommandMemberSurface _surface = null!;

    void Because() => _surface = WolverineCommandMembers.Describe(Type("Conventional"));

    [Fact] void should_keep_the_source_type() => SymbolEqualityComparer.Default.Equals(_surface.SourceType, Type("Conventional")).ShouldBeTrue();
    [Fact] void should_keep_the_declaration_order() => _surface.Properties.Select(_ => _.Name).ShouldContainOnly("title", "id", "aggregateId");
    [Fact] void should_not_flag_any_member_as_identifier() => _surface.Properties.ShouldEachConformTo(_ => !_.IsIdentifier);
}
