// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Cratis.CritterStack.Screenplay.for_CritterStackSource;

public class when_a_declaration_is_outside_the_source_root : given.a_critter_stack_source_context
{
    Evidence _evidence = null!;

    void Because()
    {
        var referencedTree = SourceTree(
            "namespace Referenced; public sealed class SharedType;",
            "/outside/Referenced/SharedType.cs");
        var referencedCompilation = SourceCompilation("Referenced", [referencedTree]);
        var project = ReferencingProject(referencedCompilation, "/checkout");

        _evidence = EvidenceFor(ReferencedType(project, "Referenced.SharedType"), project);
    }

    [Fact] void should_omit_source_instead_of_falling_back_to_a_physical_path() => _evidence.Source.ShouldBeNull();
}
