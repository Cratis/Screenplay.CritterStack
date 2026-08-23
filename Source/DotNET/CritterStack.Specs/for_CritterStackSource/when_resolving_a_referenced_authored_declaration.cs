// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Cratis.CritterStack.Screenplay.for_CritterStackSource;

public class when_resolving_a_referenced_authored_declaration : given.a_critter_stack_source_context
{
    Evidence _evidence = null!;

    void Because()
    {
        var referencedTree = SourceTree(
            "namespace Referenced; public sealed class SharedType;",
            "/checkout/Referenced/SharedType.cs");
        var referencedCompilation = SourceCompilation("Referenced", [referencedTree]);
        var project = ReferencingProject(referencedCompilation, "/checkout");

        _evidence = EvidenceFor(ReferencedType(project, "Referenced.SharedType"), project);
    }

    [Fact] void should_supply_the_legacy_workspace_relative_display_range() =>
        _evidence.Source!.Path.ShouldEqual("Referenced/SharedType.cs");

    [Fact] void should_not_invent_a_source_file_identity() => _evidence.Source!.FileIdentity.ShouldBeNull();
}
