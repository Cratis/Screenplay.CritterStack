// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Cratis.CritterStack.Screenplay.for_CompiledQueryMemberVector;

/// <summary>
/// Facade characterization of the compiled-query parameter member transfer: the neutral-fact mapping that
/// would replace it is recorded in the cycle vector mapping. The facade behavior and bytes stay unchanged.
/// </summary>
public class when_a_compiled_query_shares_a_member_name_with_an_endpoint_parameter : given.a_compiled_query_member_vector_application
{
    IReadOnlyList<PropertyDefinition> _properties = null!;
    GeneratedScreenplayDefinition _result = null!;

    void Because()
    {
        _properties = [.. Contribution.Facts.OfType<ArtifactFact>().Single(_ => _.Definition.Key.Kind == ArtifactKind.Query).Definition.Properties];
        _result = new CritterStackScreenplayGenerator().Generate([Project], new CritterStackScreenplayOptions { Domain = "Vector" });
    }

    [Fact] void should_compile_the_fixture() => Project.Compilation.GetDiagnostics(TestContext.Current.CancellationToken).Where(_ => _.Severity == DiagnosticSeverity.Error).ShouldBeEmpty();
    [Fact] void should_generate_a_compiler_verified_candidate() => _result.IsSuccess.ShouldBeTrue();
    [Fact] void should_list_the_members_sorted_by_name() => _properties.Select(_ => _.Name).ToArray().ShouldEqualInOrder("name", "tag", "take");
    [Fact] void should_take_the_compiled_member_type_for_the_shared_name() => _properties.Single(_ => _.Name == "name").Type.ShouldEqual(TypeOf(SpecialType.System_Int32));
    [Fact] void should_keep_one_property_for_the_shared_name() => _properties.Count(_ => _.Name == "name").ShouldEqual(1);
    [Fact] void should_transfer_the_compiled_property() => _properties.Single(_ => _.Name == "tag").Type.ShouldEqual(TypeOf(SpecialType.System_String));
    [Fact] void should_transfer_the_compiled_field() => _properties.Single(_ => _.Name == "take").Type.ShouldEqual(TypeOf(SpecialType.System_Int32));
    [Fact] void should_not_mark_any_member_as_the_identifier() => _properties.Any(_ => _.IsIdentifier).ShouldBeFalse();
    [Fact] void should_print_the_transferred_members() => _result.Source.ShouldContain("take");

    TypeReferenceDefinition TypeOf(SpecialType type) => DotNetTypeShapes.TypeReferenceFor(Project.Compilation.GetSpecialType(type));
}
