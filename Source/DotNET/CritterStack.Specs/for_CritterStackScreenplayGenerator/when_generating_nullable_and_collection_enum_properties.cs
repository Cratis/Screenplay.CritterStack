// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Cratis.CritterStack.Screenplay.for_CritterStackScreenplayGenerator;

public class when_generating_nullable_and_collection_enum_properties : given.an_enum_application
{
    GeneratedScreenplayDefinition _nullable = null!;
    GeneratedScreenplayDefinition _array = null!;
    GeneratedScreenplayDefinition _collection = null!;

    void Because()
    {
        _nullable = Generate(CreateProject(propertyType: "Priority?"));
        _array = Generate(CreateProject(propertyType: "Priority[]"));
        _collection = Generate(CreateProject(propertyType: "System.Collections.Generic.IReadOnlyList<Priority>"));
    }

    [Fact] void should_preserve_nullable_enum_fields() => _nullable.Source.ShouldContain("priority Priority?");
    [Fact] void should_preserve_array_enum_fields() => _array.Source.ShouldContain("priority Priority[]");
    [Fact] void should_preserve_collection_enum_fields() => _collection.Source.ShouldContain("priority Priority[]");
    [Fact] void should_emit_a_concept_for_each_supported_shape() => Results.All(_ => _.Source.Contains("concept Priority : Enum", StringComparison.Ordinal)).ShouldBeTrue();
    [Fact] void should_succeed_for_each_supported_shape() => Results.All(_ => _.IsSuccess).ShouldBeTrue();
    [Fact] void should_compile_without_undeclared_enum_field_types() => Results.All(result => !new global::Cratis.Screenplay.ScreenplayCompiler().Compile(result.Source).Diagnostics.Any(_ => _.Code == "PLAY0165")).ShouldBeTrue();

    IReadOnlyList<GeneratedScreenplayDefinition> Results => [_nullable, _array, _collection];
}
