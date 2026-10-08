// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Cratis.CritterStack.Screenplay.for_CritterStackDiagnosticCodes;

public class when_consumers_filter_by_diagnostic_code : Specification
{
    Type _codes = null!;

    void Because() => _codes = typeof(CritterStackDiagnosticCodes);

    [Fact] void should_expose_the_codes_publicly() => _codes.IsPublic.ShouldBeTrue();
    [Fact] void should_expose_the_flags_code_as_a_stable_constant() => _codes.GetField(nameof(CritterStackDiagnosticCodes.FlagsEnumOmitted))!.GetRawConstantValue().ShouldEqual("CRITTERSTACK0001");
    [Fact] void should_expose_the_enum_values_code_as_a_stable_constant() => _codes.GetField(nameof(CritterStackDiagnosticCodes.EnumValuesUnsupported))!.GetRawConstantValue().ShouldEqual("CRITTERSTACK0002");
    [Fact] void should_expose_the_enum_name_conflict_code_as_a_stable_constant() => _codes.GetField(nameof(CritterStackDiagnosticCodes.EnumConceptNameConflict))!.GetRawConstantValue().ShouldEqual("CRITTERSTACK0003");
}
