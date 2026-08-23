// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Cratis.CritterStack.Screenplay.for_CritterStackSource;

public class when_a_referenced_symbol_has_multiple_declarations : given.a_critter_stack_source_context
{
    Evidence _evidence = null!;

    void Because()
    {
        var lastTree = SourceTree(
            "namespace Referenced; public partial class SharedType;",
            "/checkout/Referenced/Z.cs");
        var firstTree = SourceTree(
            "namespace Referenced; public partial class SharedType;",
            "/checkout/Referenced/A.cs");
        var referencedCompilation = SourceCompilation("Referenced", [lastTree, firstTree]);
        var project = ReferencingProject(referencedCompilation, "/checkout");

        _evidence = EvidenceFor(ReferencedType(project, "Referenced.SharedType"), project);
    }

    [Fact] void should_select_the_first_safe_display_path_in_ordinal_order() =>
        _evidence.Source!.Path.ShouldEqual("Referenced/A.cs");
}
