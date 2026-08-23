// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Cratis.CritterStack.Screenplay.for_CritterStackSource;

public class when_the_checkout_is_relocated : given.a_critter_stack_source_context
{
    SourceRange? _firstRange;
    SourceRange? _secondRange;

    void Because()
    {
        _firstRange = RangeAt("/checkout-one");
        _secondRange = RangeAt("/checkout-two");
    }

    [Fact] void should_keep_the_same_legacy_display_range() => _secondRange.ShouldEqual(_firstRange);
    [Fact] void should_not_retain_either_physical_checkout_root() => _firstRange!.Path.ShouldEqual("Referenced/SharedType.cs");

    static SourceRange? RangeAt(string checkoutRoot)
    {
        var referencedTree = SourceTree(
            "namespace Referenced; public sealed class SharedType;",
            $"{checkoutRoot}/Referenced/SharedType.cs");
        var referencedCompilation = SourceCompilation("Referenced", [referencedTree]);
        var project = ReferencingProject(referencedCompilation, checkoutRoot, checkoutRoot);

        return EvidenceFor(ReferencedType(project, "Referenced.SharedType"), project).Source;
    }
}
