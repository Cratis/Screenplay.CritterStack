// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Cratis.CritterStack.Screenplay.for_DiagnosticParity;

public class when_authored_abstraction_diagnostics_have_independent_owners : given.an_authored_abstraction_application
{
    protected override bool IncludeImplementation => false;

    GeneratedScreenplayDefinition _forward = null!;
    GeneratedScreenplayDefinition _reverse = null!;

    void Because()
    {
        Generate();
        var context = new DotNetAnalysisContext(Projects);
        var contribution = new CritterStackScreenplayAdapter().AnalyzeForComposition(
            context, new DotNetAdapterOptions(), useSharedPlacement: true, new AuthoredImplementationManifest(context));
        var first = given.diagnostic_parity.Reown(contribution, "first-abstraction-owner");
        var second = given.diagnostic_parity.Reown(contribution, "second-abstraction-owner");
        _forward = given.diagnostic_parity.GenerateIn("Authored", first, second);
        _reverse = given.diagnostic_parity.GenerateIn("Authored", second, first);
    }

    [Fact] void should_keep_the_compatibility_candidate_verified() => Result.IsSuccess.ShouldBeTrue();
    [Fact] void should_cover_the_opt_in_unresolved_abstraction_warning() => Result.Diagnostics.Any(diagnostic => diagnostic.Code == WolverineDiagnosticCodes.AuthoredAbstractionUnresolved).ShouldBeTrue();
    [Fact] void should_preserve_each_payload_location_and_count() => given.diagnostic_parity.Meaning(_forward.Diagnostics).ShouldEqual(given.diagnostic_parity.Meaning(Result.Diagnostics));
    [Fact] void should_not_depend_on_owner_order() => given.diagnostic_parity.Meaning(_reverse.Diagnostics).ShouldEqual(given.diagnostic_parity.Meaning(Result.Diagnostics));
    [Fact] void should_preserve_generated_source() => _forward.Source.ShouldEqual(Result.Source);
    [Fact] void should_preserve_verification() => _forward.IsSuccess.ShouldEqual(Result.IsSuccess);
    [Fact] void should_preserve_reverse_generated_source() => _reverse.Source.ShouldEqual(Result.Source);
}
