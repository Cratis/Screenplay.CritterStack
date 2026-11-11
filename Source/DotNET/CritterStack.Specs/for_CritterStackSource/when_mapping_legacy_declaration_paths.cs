// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Cratis.CritterStack.Screenplay.for_CritterStackSource;

public class when_mapping_legacy_declaration_paths : given.a_critter_stack_source_context
{
    const string Declaration = "namespace Referenced;\n\npublic sealed class SharedType;";

    SourceRange? _nested;
    SourceRange? _normalized;
    SourceRange? _escaping;
    SourceRange? _sibling;
    SourceRange? _blankRoot;
    SourceRange? _blankPath;
    SourceRange? _rootItself;

    void Because()
    {
        _nested = RangeFor("/checkout", "/checkout/Referenced/A/SharedType.cs");
        _normalized = RangeFor("/checkout/", "/checkout/Referenced/../Other/SharedType.cs");
        _escaping = RangeFor("/checkout/Sub", "/checkout/Other/SharedType.cs");
        _sibling = RangeFor("/checkout", "/checkout-other/SharedType.cs");
        _blankRoot = RangeFor("  ", "/checkout/SharedType.cs");
        _blankPath = RangeFor("/checkout", " ");
        _rootItself = RangeFor("/checkout/SharedType.cs", "/checkout/SharedType.cs");
    }

    [Fact] void should_map_a_nested_declaration_to_a_one_based_range() => AssertRange(_nested, "Referenced/A/SharedType.cs");
    [Fact] void should_normalize_dot_dot_segments_inside_the_root() => AssertRange(_normalized, "Other/SharedType.cs");
    [Fact] void should_omit_a_declaration_that_escapes_the_root() => _escaping.ShouldBeNull();
    [Fact] void should_omit_a_declaration_in_a_sibling_directory_with_the_root_as_prefix() => _sibling.ShouldBeNull();
    [Fact] void should_omit_source_for_a_blank_root() => _blankRoot.ShouldBeNull();
    [Fact] void should_omit_source_for_a_blank_path() => _blankPath.ShouldBeNull();
    [Fact] void should_omit_source_for_the_root_itself() => _rootItself.ShouldBeNull();

    static SourceRange? RangeFor(string sourceRoot, string path)
    {
        var referencedTree = SourceTree(Declaration, path);
        var referencedCompilation = SourceCompilation("Referenced", [referencedTree]);
        var project = ReferencingProject(referencedCompilation, sourceRoot);

        return EvidenceFor(ReferencedType(project, "Referenced.SharedType"), project).Source;
    }

    static void AssertRange(SourceRange? range, string path)
    {
        range.ShouldNotBeNull();
        range.Path.ShouldEqual(path);
        range.StartLine.ShouldEqual(3);
        range.StartColumn.ShouldEqual(1);
        range.EndLine.ShouldEqual(3);
        range.EndColumn.ShouldEqual(32);
    }
}
