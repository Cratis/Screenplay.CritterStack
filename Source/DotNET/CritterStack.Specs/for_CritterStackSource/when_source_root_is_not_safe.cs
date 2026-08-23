// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Cratis.CritterStack.Screenplay.for_CritterStackSource;

public class when_source_root_is_not_safe : given.a_critter_stack_source_context
{
    Evidence _absentRootEvidence = null!;
    Evidence _relativeRootEvidence = null!;

    void Because()
    {
        var referencedTree = SourceTree(
            "namespace Referenced; public sealed class SharedType;",
            "/checkout/Referenced/SharedType.cs");
        var referencedCompilation = SourceCompilation("Referenced", [referencedTree]);
        var absentRootProject = ReferencingProject(referencedCompilation, null);
        var relativeRootProject = ReferencingProject(referencedCompilation, "checkout");

        _absentRootEvidence = EvidenceFor(ReferencedType(absentRootProject, "Referenced.SharedType"), absentRootProject);
        _relativeRootEvidence = EvidenceFor(ReferencedType(relativeRootProject, "Referenced.SharedType"), relativeRootProject);
    }

    [Fact] void should_omit_source_when_the_root_is_absent() => _absentRootEvidence.Source.ShouldBeNull();
    [Fact] void should_omit_source_when_the_root_is_relative() => _relativeRootEvidence.Source.ShouldBeNull();
}
