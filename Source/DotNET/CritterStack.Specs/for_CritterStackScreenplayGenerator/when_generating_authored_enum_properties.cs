// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Cratis.CritterStack.Screenplay.for_CritterStackScreenplayGenerator;

public class when_generating_authored_enum_properties : given.an_enum_application
{
    GeneratedScreenplayDefinition _result = null!;

    void Because() => _result = Generate(Project);

    [Fact] void should_succeed() => _result.IsSuccess.ShouldBeTrue();
    [Fact] void should_declare_the_enum_concept() => _result.Source.ShouldContain("concept Priority : Enum");
    [Fact] void should_print_the_field_using_the_concept() => _result.Source.ShouldContain("priority Priority");
    [Fact] void should_keep_the_named_values_in_declaration_order() => (_result.Source.IndexOf("urgent", StringComparison.Ordinal) < _result.Source.IndexOf("normal", StringComparison.Ordinal) && _result.Source.IndexOf("normal", StringComparison.Ordinal) < _result.Source.IndexOf("alias", StringComparison.Ordinal)).ShouldBeTrue();
    [Fact] void should_not_declare_unused_enums() => _result.Source.ShouldNotContain("concept Unused");
    [Fact] void should_compile_without_undeclared_field_types() => new global::Cratis.Screenplay.ScreenplayCompiler().Compile(_result.Source).Diagnostics.Any(_ => _.Code == "PLAY0165").ShouldBeFalse();
}
