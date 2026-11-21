// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Cratis.CritterStack.Screenplay.for_CritterStackDiagnosticCodes;

public class when_consumers_filter_by_diagnostic_code : Specification
{
    Type _codes = null!;
    Type _martenCodes = null!;
    Type _wolverineCodes = null!;

    void Because()
    {
        _codes = typeof(CritterStackDiagnosticCodes);
        _martenCodes = typeof(MartenDiagnosticCodes);
        _wolverineCodes = typeof(WolverineDiagnosticCodes);
    }

    [Fact] void should_expose_the_codes_publicly() => _codes.IsPublic.ShouldBeTrue();
    [Fact] void should_expose_the_flags_code_as_a_stable_constant() => _codes.GetField(nameof(CritterStackDiagnosticCodes.FlagsEnumOmitted)).GetRawConstantValue().ShouldEqual("CRITTERSTACK0001");
    [Fact] void should_expose_the_enum_values_code_as_a_stable_constant() => _codes.GetField(nameof(CritterStackDiagnosticCodes.EnumValuesUnsupported)).GetRawConstantValue().ShouldEqual("CRITTERSTACK0002");
    [Fact] void should_expose_the_enum_name_conflict_code_as_a_stable_constant() => _codes.GetField(nameof(CritterStackDiagnosticCodes.EnumConceptNameConflict)).GetRawConstantValue().ShouldEqual("CRITTERSTACK0003");
    [Fact] void should_expose_the_enum_numbers_code_as_a_stable_constant() => _codes.GetField(nameof(CritterStackDiagnosticCodes.EnumNumbersNotPreserved)).GetRawConstantValue().ShouldEqual("CRITTERSTACK0005");
    [Fact] void should_expose_the_composite_projection_code_as_a_stable_constant() => _martenCodes.GetField(nameof(MartenDiagnosticCodes.CompositeProjectionOmitted)).GetRawConstantValue().ShouldEqual("MARTEN0018");
    [Fact] void should_expose_the_flat_table_projection_code_as_a_stable_constant() => _martenCodes.GetField(nameof(MartenDiagnosticCodes.FlatTableProjectionOmitted)).GetRawConstantValue().ShouldEqual("MARTEN0019");
    [Fact] void should_expose_the_store_scope_code_as_a_stable_constant() => _martenCodes.GetField(nameof(MartenDiagnosticCodes.StoreScopeNotRepresented)).GetRawConstantValue().ShouldEqual("MARTEN0020");
    [Fact] void should_expose_the_event_forwarding_code_as_a_stable_constant() => _wolverineCodes.GetField(nameof(WolverineDiagnosticCodes.EventForwardingOmitted)).GetRawConstantValue().ShouldEqual("WOLVERINE0027");
    [Fact] void should_expose_the_endpoint_idempotency_code_as_a_stable_constant() => _wolverineCodes.GetField(nameof(WolverineDiagnosticCodes.EndpointIdempotencyOmitted)).GetRawConstantValue().ShouldEqual("WOLVERINE0028");
    [Fact] void should_expose_the_authored_abstraction_code_as_a_stable_constant() => _wolverineCodes.GetField(nameof(WolverineDiagnosticCodes.AuthoredAbstractionUnresolved)).GetRawConstantValue().ShouldEqual("WOLVERINE0029");
    [Fact] void should_expose_the_natural_key_identity_code_as_a_stable_constant() => _wolverineCodes.GetField(nameof(WolverineDiagnosticCodes.NaturalKeyIdentityOmitted)).GetRawConstantValue().ShouldEqual("WOLVERINE0030");
}
