// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Cratis.CritterStack.Screenplay.for_WolverineCommandMembers;

public class when_materializing_compatibility_properties : given.a_wolverine_command_members_application
{
    WolverineCommandMemberSurface _surface = null!;
    IReadOnlyList<PropertyDefinition> _identified = [];
    IReadOnlyList<PropertyDefinition> _unidentified = [];
    IReadOnlyList<PropertyDefinition> _unmatched = [];

    void Because()
    {
        _surface = WolverineCommandMembers.Describe(Type("Plain"));
        _identified = WolverineCommandMembers.Materialize(_surface, new WolverineCommandIdentifier(Member("Plain", "Count")));
        _unidentified = WolverineCommandMembers.Materialize(_surface, new WolverineCommandIdentifier(null));
        _unmatched = WolverineCommandMembers.Materialize(_surface, new WolverineCommandIdentifier(Member("Conventional", "Id")));
    }

    [Fact] void should_keep_the_declaration_order() => _identified.Select(_ => _.Name).ShouldContainOnly("title", "count", "mode", "optional", "tags");
    [Fact] void should_keep_the_property_types() => _identified.Select(_ => _.Type).SequenceEqual(_surface.Properties.Select(property => property.Type)).ShouldBeTrue();
    [Fact] void should_flag_the_member_matching_by_lower_first_name() => Identified(_identified).ShouldEqual("count");
    [Fact] void should_flag_nothing_without_an_identifier() => Identified(_unidentified).ShouldEqual(string.Empty);
    [Fact] void should_flag_nothing_when_no_declared_member_matches() => Identified(_unmatched).ShouldEqual(string.Empty);
    [Fact] void should_leave_the_surface_unmodified() => _surface.Properties.ShouldEachConformTo(_ => !_.IsIdentifier);
}
